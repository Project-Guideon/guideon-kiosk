using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Guideon.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Guideon.Audio
{
    /// <summary>
    /// tts_chunk 스트림을 seq 순서로 디코드해 AudioSource로 순차 재생.
    /// audio_format에 따라 pcm_s16le(Cartesia 정상) / mp3(Google TTS 폴백) 분기 처리.
    /// SttManager에서 직접 메소드 호출로 제어된다.
    /// </summary>
    public class TtsManager : MonoSingleton<TtsManager>
    {
        private AudioSource _audio;

        private readonly Dictionary<int, AudioClip> _ready = new();
        private string _sessionId;
        private int    _nextPlaySeq;
        private int    _highestSeqSeen = -1;
        private bool   _serverDone;
        private bool   _isPlaying;
        private int    _activeDecodes;
        private bool   _finalized;
        private bool   _playbackHeld;

        // pcm_s16le: Cartesia 정상 동작 시 사용하는 샘플레이트
        private const int PcmSampleRate = 24000;

        protected override void OnInitialize()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
        }

        // ── 공개 API (SttManager가 호출) ──────────────────────

        public void BeginSession(string sessionId)
        {
            if (_isPlaying) _audio.Stop();
            _ready.Clear();

            _sessionId       = sessionId;
            _nextPlaySeq     = 0;
            _highestSeqSeen  = -1;
            _serverDone      = false;
            _isPlaying       = false;
            _activeDecodes   = 0;
            _finalized       = false;
            _playbackHeld    = false;

            Debug.Log($"[TtsManager] 세션 시작 — {sessionId}");
        }

        /// <summary>
        /// tts_chunk 수신 시 호출. audio_format에 따라 pcm_s16le / mp3 경로로 분기.
        /// </summary>
        public void EnqueueChunk(int seq, string audioFormat, string audioB64)
        {
            if (string.IsNullOrEmpty(audioB64)) return;

            byte[] rawBytes;
            try { rawBytes = Convert.FromBase64String(audioB64); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TtsManager] base64 디코드 실패 seq={seq}: {ex.Message}");
                return;
            }

            _highestSeqSeen = Mathf.Max(_highestSeqSeen, seq);
            _activeDecodes++;
            Debug.Log($"[TtsManager] EnqueueChunk seq={seq} format={audioFormat} ({rawBytes.Length} bytes)");

            switch (audioFormat)
            {
                case "pcm_s16le":
                    // 파일 I/O 없이 메인 스레드에서 직접 AudioClip 생성
                    DecodePcm(seq, rawBytes, _sessionId);
                    break;

                case "mp3":
                    // 임시 파일 → UnityWebRequestMultimedia 경로 (Cartesia 장애 시 폴백)
                    DecodeMp3Async(seq, rawBytes, _sessionId).Forget();
                    break;

                default:
                    Debug.LogWarning($"[TtsManager] 지원하지 않는 포맷: {audioFormat}, seq={seq} 스킵");
                    _activeDecodes--;
                    break;
            }
        }

        public void MarkServerDone()
        {
            _serverDone = true;
            Debug.Log("[TtsManager] 서버 done 마킹");
            TryFinalize();
        }

        public void HoldPlayback()
        {
            _playbackHeld = true;
        }

        public void ReleasePlayback()
        {
            if (!_playbackHeld) return;
            _playbackHeld = false;
            TryPlayNext();
        }

        public void AbortSession()
        {
            if (_isPlaying) _audio.Stop();
            _ready.Clear();
            _isPlaying     = false;
            _serverDone    = true;  // 더 이상 청크가 오지 않도록
            _finalized     = true;
            Debug.Log("[TtsManager] 세션 강제 종료");
        }

        // ── PCM 디코드 (pcm_s16le) ────────────────────────────

        /// <summary>
        /// pcm_s16le: 16-bit 부호형 PCM, 24000Hz, mono.
        /// 파일 I/O 없이 메인 스레드에서 즉시 AudioClip을 생성한다.
        /// </summary>
        private void DecodePcm(int seq, byte[] pcmBytes, string sessionId)
        {
            // 세션 변경 시 결과 버림
            if (sessionId != _sessionId)
            {
                _activeDecodes--;
                return;
            }

            try
            {
                int sampleCount = pcmBytes.Length / 2;
                float[] samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    short s = BitConverter.ToInt16(pcmBytes, i * 2);
                    samples[i] = s / 32768.0f;
                }

                AudioClip clip = AudioClip.Create($"tts_{seq}", sampleCount, 1, PcmSampleRate, false);
                clip.SetData(samples, 0);
                _ready[seq] = clip;
                Debug.Log($"[TtsManager] PCM 디코드 완료 seq={seq}, samples={sampleCount}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TtsManager] PCM 디코드 예외 seq={seq}: {ex.Message}");
                // 디코드 실패 시 해당 seq 건너뜀
                if (_nextPlaySeq == seq)
                {
                    Debug.LogWarning($"[TtsManager] seq={seq} PCM 실패, 스킵");
                    _nextPlaySeq++;
                }
            }
            finally
            {
                _activeDecodes--;
            }

            TryPlayNext();
            TryFinalize();
        }

        // ── MP3 디코드 (폴백) ─────────────────────────────────

        /// <summary>
        /// mp3: 임시 파일 경유 UnityWebRequestMultimedia 경로.
        /// Cartesia 장애 시 Google TTS 폴백으로 내려오는 포맷.
        /// </summary>
        private async UniTaskVoid DecodeMp3Async(int seq, byte[] mp3, string sessionId)
        {
            string path = Path.Combine(Application.temporaryCachePath, $"tts_{sessionId}_{seq}.mp3");
            AudioClip clip = null;

            try
            {
                await File.WriteAllBytesAsync(path, mp3);

                using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.MPEG);
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;

                await req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[TtsManager] MP3 디코드 요청 실패 seq={seq}: {req.error}");
                }
                else
                {
                    clip = DownloadHandlerAudioClip.GetContent(req);
                    if (clip == null)
                        Debug.LogWarning($"[TtsManager] AudioClip null seq={seq}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TtsManager] MP3 디코드 예외 seq={seq}: {ex.Message}");
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
            }

            // 세션 변경 시 결과 버림
            if (sessionId != _sessionId)
            {
                clip?.UnloadAudioData();
                _activeDecodes--;
                return;
            }

            if (clip != null)
            {
                _ready[seq] = clip;
            }
            else
            {
                // 디코드 실패 시 해당 seq 건너뜀
                if (_nextPlaySeq == seq)
                {
                    Debug.LogWarning($"[TtsManager] seq={seq} MP3 디코드 실패, 스킵");
                    _nextPlaySeq++;
                }
            }

            _activeDecodes--;
            TryPlayNext();
            TryFinalize();
        }

        // ── 재생 ──────────────────────────────────────────────

        private void TryPlayNext()
        {
            if (_playbackHeld) return;
            if (_isPlaying) return;
            if (!_ready.TryGetValue(_nextPlaySeq, out var clip)) return;

            bool isLast = _serverDone && _nextPlaySeq >= _highestSeqSeen;
            _audio.clip = clip;
            _audio.Play();
            _isPlaying = true;

            Debug.Log($"[TtsManager] 재생 seq={_nextPlaySeq}, isLast={isLast}");
            EventBus.Publish(new TtsChunkReadyEvent
            {
                Clip     = clip,
                Sentence = null,
                Seq      = _nextPlaySeq,
                IsLast   = isLast,
            });
        }

        private void Update()
        {
            if (!_isPlaying) return;
            if (_audio.isPlaying) return;

            // 클립 재생 완료
            _ready.Remove(_nextPlaySeq);
            _nextPlaySeq++;
            _isPlaying = false;

            TryPlayNext();
            TryFinalize();
        }

        // ── 종료 ──────────────────────────────────────────────

        /// <summary>
        /// 완료 판단은 tts_chunk.is_final이 아닌 done 메시지 기준(MarkServerDone)으로 처리.
        /// </summary>
        private void TryFinalize()
        {
            if (_finalized) return;
            if (!_serverDone) return;
            if (_isPlaying) return;
            if (_activeDecodes > 0) return;
            if (_ready.Count > 0) return;
            // 청크가 전혀 없었거나 모두 소진된 경우
            if (_highestSeqSeen >= 0 && _nextPlaySeq <= _highestSeqSeen) return;

            _finalized = true;
            string sid = _sessionId;
            Debug.Log("[TtsManager] TTS 완료 — TtsDoneEvent 발행");
            EventBus.Publish(new TtsDoneEvent { SessionId = sid });
        }

        protected override void OnDestroy()
        {
            AbortSession();
            base.OnDestroy();
        }
    }
}
