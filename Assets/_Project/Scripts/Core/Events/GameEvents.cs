using Guideon.Network.Models;

namespace Guideon.Core
{
    // ── 마스코트 ──────────────────────────────────────────
    public enum MascotState { Idle, Greeting, Listening, Thinking, Speaking }

    public struct MascotStateEvent
    {
        public MascotState State;
    }

    // ── STT (음성 인식) ───────────────────────────────────
    public struct SttResultEvent
    {
        public string Transcript;
        public bool IsFinal;
    }

    // ── TTS (음성 합성) ───────────────────────────────────
    public struct TtsChunkReadyEvent
    {
        public UnityEngine.AudioClip Clip;
        public string Sentence;
        public int Seq;
        public bool IsLast;
    }

    public struct TtsDoneEvent
    {
        public string SessionId;
    }

    // ── 채팅 ──────────────────────────────────────────────
    public struct ChatResponseEvent
    {
        public string SessionId;
        public string Answer;
        public string Emotion;
        public string Language;
        public DisplayHint Display; // null이면 장소 언급 없음
    }

    // ── 페어링 ────────────────────────────────────────────
    public struct PairingCodeIssuedEvent
    {
        public string PairingCode;
        public string ExpiresAt;
    }

    public struct PairingCompletedEvent
    {
        public string DeviceId;
    }

    // ── 네트워크 ──────────────────────────────────────────
    public struct NetworkErrorEvent
    {
        public string Message;
        public int StatusCode;
    }

    // ── 인증/부트스트랩 ───────────────────────────────────
    public struct AuthVerifiedEvent
    {
        public string DeviceId;
        public long SiteId;
        public long? ZoneId;
    }

    public struct BootstrapLoadedEvent
    {
        public BootstrapResponse Data;
    }

    // ── UI / 앱 상태 ──────────────────────────────────────
    public struct IdleTimeoutEvent { }

    public struct UserTouchedEvent { }

    public struct SceneReadyEvent
    {
        public string SceneName;
    }
}
