using System.IO;
using UnityEngine;

namespace Guideon.Mascot
{
    /// <summary>
    /// 모델의 메인 텍스처를 직접 수정하여 표정을 변경하는 시스템.
    /// 원본 텍스처의 눈/입 영역 픽셀을 수정하여 표정을 표현한다.
    ///
    /// 사용 흐름:
    /// 1. Initialize()로 SkinnedMeshRenderer 전달
    /// 2. UV 좌표로 눈/입 영역 설정 (SetFaceRegion) 또는 자동 탐색
    /// 3. SetState()로 표정 변경
    /// </summary>
    public class FaceTextureModifier : MonoBehaviour
    {
        private SkinnedMeshRenderer _renderer;
        private Material _material;
        private Texture2D _originalTexture;   // 원본 (읽기용 복사본)
        private Texture2D _workingTexture;    // 수정용 복사본 (실제 머티리얼에 적용)

        // 얼굴 영역 (UV 좌표 0~1 → 픽셀 좌표로 변환)
        private Rect _leftEyeRegion;
        private Rect _rightEyeRegion;
        private Rect _mouthRegion;
        private bool _regionsSet;

        // 원본 영역 픽셀 백업
        private Color[] _originalLeftEyePixels;
        private Color[] _originalRightEyePixels;
        private Color[] _originalMouthPixels;

        private MascotState _currentState = MascotState.Idle;
        private float _mouthTimer;
        private float _blinkTimer;
        private bool _isBlinking;
        private float _blinkEndTime;

        public bool IsInitialized => _workingTexture != null;
        public bool RegionsSet => _regionsSet;
        public Texture2D OriginalTexture => _originalTexture;

        public void Initialize(SkinnedMeshRenderer renderer)
        {
            _renderer = renderer;
            if (_renderer == null || _renderer.material == null) return;

            _material = _renderer.material;
            var srcTex = _material.mainTexture as Texture2D;
            if (srcTex == null)
            {
                Debug.LogWarning("[FaceTextureModifier] Main texture is not Texture2D.");
                return;
            }

            // 원본 텍스처를 readable 복사본으로 만들기
            _originalTexture = MakeReadable(srcTex);
            _workingTexture = MakeReadable(srcTex);

            // 머티리얼에 working 텍스처 적용
            _material.mainTexture = _workingTexture;

            Debug.Log($"[FaceTextureModifier] Initialized. Texture: {_workingTexture.width}x{_workingTexture.height}");
        }

        /// <summary>
        /// UV 좌표 기반으로 얼굴 영역 설정 (0~1 범위)
        /// </summary>
        public void SetFaceRegions(Rect leftEyeUV, Rect rightEyeUV, Rect mouthUV)
        {
            int w = _workingTexture.width;
            int h = _workingTexture.height;

            _leftEyeRegion = UVToPixelRect(leftEyeUV, w, h);
            _rightEyeRegion = UVToPixelRect(rightEyeUV, w, h);
            _mouthRegion = UVToPixelRect(mouthUV, w, h);

            // 원본 픽셀 백업
            _originalLeftEyePixels = _originalTexture.GetPixels(
                (int)_leftEyeRegion.x, (int)_leftEyeRegion.y,
                (int)_leftEyeRegion.width, (int)_leftEyeRegion.height);
            _originalRightEyePixels = _originalTexture.GetPixels(
                (int)_rightEyeRegion.x, (int)_rightEyeRegion.y,
                (int)_rightEyeRegion.width, (int)_rightEyeRegion.height);
            _originalMouthPixels = _originalTexture.GetPixels(
                (int)_mouthRegion.x, (int)_mouthRegion.y,
                (int)_mouthRegion.width, (int)_mouthRegion.height);

            _regionsSet = true;
            Debug.Log($"[FaceTextureModifier] Face regions set. LEye:{_leftEyeRegion} REye:{_rightEyeRegion} Mouth:{_mouthRegion}");
        }

        public void SetState(MascotState state)
        {
            _currentState = state;
            _mouthTimer = 0f;

            if (!_regionsSet) return;
            ApplyExpression(state);
        }

        void Update()
        {
            if (!_regionsSet || _workingTexture == null) return;

            UpdateBlink();
            UpdateSpeakingMouth();
        }

        #region Blink

        private void UpdateBlink()
        {
            _blinkTimer += Time.deltaTime;

            if (_isBlinking)
            {
                if (Time.time > _blinkEndTime)
                {
                    _isBlinking = false;
                    ApplyExpression(_currentState); // 눈 다시 열기
                }
            }
            else if (_blinkTimer >= 3.5f)
            {
                _blinkTimer = 0f;
                _isBlinking = true;
                _blinkEndTime = Time.time + 0.12f;
                DrawClosedEyes();
                _workingTexture.Apply();
            }
        }

        #endregion

        #region Speaking Mouth

        private void UpdateSpeakingMouth()
        {
            if (_currentState != MascotState.Speaking) return;

            _mouthTimer += Time.deltaTime;
            float phase = Mathf.Sin(_mouthTimer * 8f);
            bool open = phase > 0f;

            // 매 프레임 입 그리기는 비효율적이므로 상태 전환 시에만
            // → 간단히 0.1초마다 토글
            if (Mathf.Abs(phase) < 0.15f)
            {
                RestoreRegion(_mouthRegion, _originalMouthPixels);
                if (open) DrawOpenMouth();
                else DrawNeutralMouth();
                _workingTexture.Apply();
            }
        }

        #endregion

        #region Expression Drawing

        private void ApplyExpression(MascotState state)
        {
            if (_workingTexture == null || !_regionsSet) return;

            // 원본으로 리셋
            RestoreRegion(_leftEyeRegion, _originalLeftEyePixels);
            RestoreRegion(_rightEyeRegion, _originalRightEyePixels);
            RestoreRegion(_mouthRegion, _originalMouthPixels);

            switch (state)
            {
                case MascotState.Idle:
                    // 원본 그대로 (기본 표정)
                    break;
                case MascotState.Greeting:
                    DrawHappyEyes();
                    DrawSmileMouth();
                    break;
                case MascotState.Listening:
                    DrawWideEyes();
                    break;
                case MascotState.Thinking:
                    DrawLookUpEyes();
                    DrawHmmMouth();
                    break;
                case MascotState.Speaking:
                    DrawOpenMouth();
                    break;
            }

            _workingTexture.Apply();
        }

        // --- Eyes ---

        private void DrawClosedEyes()
        {
            DrawLineOnRegion(_leftEyeRegion, _originalLeftEyePixels, true);
            DrawLineOnRegion(_rightEyeRegion, _originalRightEyePixels, true);
        }

        private void DrawHappyEyes()
        {
            DrawArcOnRegion(_leftEyeRegion, true);
            DrawArcOnRegion(_rightEyeRegion, true);
        }

        private void DrawWideEyes()
        {
            // 원본 눈 위에 하이라이트 강조
            AddHighlightOnRegion(_leftEyeRegion);
            AddHighlightOnRegion(_rightEyeRegion);
        }

        private void DrawLookUpEyes()
        {
            // 눈동자 위치를 위로 이동 (원본 픽셀 시프트)
            ShiftRegionUp(_leftEyeRegion, _originalLeftEyePixels, 4);
            ShiftRegionUp(_rightEyeRegion, _originalRightEyePixels, 4);
        }

        // --- Mouth ---

        private void DrawNeutralMouth()
        {
            // 원본 그대로
        }

        private void DrawSmileMouth()
        {
            DrawArcOnRegion(_mouthRegion, false);
        }

        private void DrawOpenMouth()
        {
            DrawOvalOnRegion(_mouthRegion, new Color(0.2f, 0.1f, 0.1f));
        }

        private void DrawHmmMouth()
        {
            DrawWaveOnRegion(_mouthRegion);
        }

        #endregion

        #region Pixel Drawing Utilities

        private void RestoreRegion(Rect region, Color[] originalPixels)
        {
            if (originalPixels == null) return;
            _workingTexture.SetPixels(
                (int)region.x, (int)region.y,
                (int)region.width, (int)region.height,
                originalPixels);
        }

        /// <summary>
        /// 영역 중앙에 가로선 그리기 (눈 감기)
        /// </summary>
        private void DrawLineOnRegion(Rect region, Color[] bgPixels, bool restore)
        {
            if (restore) RestoreRegion(region, bgPixels);

            int cx = (int)(region.x + region.width / 2);
            int cy = (int)(region.y + region.height / 2);
            int halfW = (int)(region.width * 0.35f);
            Color color = Color.black;

            for (int x = cx - halfW; x <= cx + halfW; x++)
            {
                for (int t = -1; t <= 1; t++)
                {
                    SetPixelSafe(x, cy + t, color);
                }
            }
            _workingTexture.Apply();
        }

        /// <summary>
        /// 영역에 곡선 (웃는 눈 또는 웃는 입)
        /// </summary>
        private void DrawArcOnRegion(Rect region, bool flipY)
        {
            int cx = (int)(region.x + region.width / 2);
            int cy = (int)(region.y + region.height / 2);
            int radius = (int)(Mathf.Min(region.width, region.height) * 0.3f);
            Color color = Color.black;

            for (int x = -radius; x <= radius; x++)
            {
                float normX = (float)x / radius;
                float yVal = Mathf.Sqrt(Mathf.Max(0, 1f - normX * normX)) * radius * 0.5f;
                int py = flipY ? (int)(cy + yVal) : (int)(cy - yVal);
                for (int t = -1; t <= 1; t++)
                {
                    SetPixelSafe(cx + x, py + t, color);
                }
            }
        }

        /// <summary>
        /// 영역에 타원 그리기 (열린 입)
        /// </summary>
        private void DrawOvalOnRegion(Rect region, Color color)
        {
            int cx = (int)(region.x + region.width / 2);
            int cy = (int)(region.y + region.height / 2);
            int rx = (int)(region.width * 0.25f);
            int ry = (int)(region.height * 0.3f);

            for (int y = -ry; y <= ry; y++)
            {
                for (int x = -rx; x <= rx; x++)
                {
                    float dx = (float)x / rx;
                    float dy = (float)y / ry;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        SetPixelSafe(cx + x, cy + y, color);
                    }
                }
            }
        }

        /// <summary>
        /// 물결 입 (~)
        /// </summary>
        private void DrawWaveOnRegion(Rect region)
        {
            int cx = (int)(region.x + region.width / 2);
            int cy = (int)(region.y + region.height / 2);
            int halfW = (int)(region.width * 0.3f);
            Color color = new Color(0.2f, 0.1f, 0.1f);

            for (int x = -halfW; x <= halfW; x++)
            {
                int wavY = (int)(Mathf.Sin(x * 0.15f) * 3f);
                for (int t = -1; t <= 1; t++)
                {
                    SetPixelSafe(cx + x, cy + wavY + t, color);
                }
            }
        }

        /// <summary>
        /// 하이라이트 추가 (큰 눈 효과)
        /// </summary>
        private void AddHighlightOnRegion(Rect region)
        {
            int cx = (int)(region.x + region.width * 0.6f);
            int cy = (int)(region.y + region.height * 0.6f);
            int r = (int)(Mathf.Min(region.width, region.height) * 0.12f);

            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y <= r * r)
                    {
                        SetPixelSafe(cx + x, cy + y, Color.white);
                    }
                }
            }
        }

        /// <summary>
        /// 영역 픽셀을 위로 시프트 (위를 보는 눈)
        /// </summary>
        private void ShiftRegionUp(Rect region, Color[] originalPixels, int shiftAmount)
        {
            int w = (int)region.width;
            int h = (int)region.height;

            var shifted = new Color[originalPixels.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcY = y - shiftAmount;
                    if (srcY >= 0 && srcY < h)
                        shifted[y * w + x] = originalPixels[srcY * w + x];
                    else
                        shifted[y * w + x] = _originalTexture.GetPixel(
                            (int)region.x + x, (int)region.y + y);
                }
            }

            _workingTexture.SetPixels((int)region.x, (int)region.y, w, h, shifted);
        }

        private void SetPixelSafe(int x, int y, Color color)
        {
            if (x >= 0 && x < _workingTexture.width && y >= 0 && y < _workingTexture.height)
            {
                _workingTexture.SetPixel(x, y, color);
            }
        }

        #endregion

        #region UV Utilities

        private Rect UVToPixelRect(Rect uvRect, int texWidth, int texHeight)
        {
            return new Rect(
                uvRect.x * texWidth,
                uvRect.y * texHeight,
                uvRect.width * texWidth,
                uvRect.height * texHeight
            );
        }

        /// <summary>
        /// 3D 모델 위 클릭 지점의 UV 좌표를 반환
        /// </summary>
        public static bool RaycastUV(Camera cam, Vector2 screenPos, out Vector2 uv, out SkinnedMeshRenderer hitRenderer)
        {
            uv = Vector2.zero;
            hitRenderer = null;

            var ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit)) return false;

            hitRenderer = hit.collider.GetComponent<SkinnedMeshRenderer>();
            if (hitRenderer == null)
            {
                hitRenderer = hit.collider.GetComponentInChildren<SkinnedMeshRenderer>();
            }

            uv = hit.textureCoord;
            return true;
        }

        #endregion

        #region Debug

        /// <summary>
        /// 원본 텍스처를 PNG로 저장 (UV 레이아웃 확인용)
        /// </summary>
        public string SaveOriginalTextureToDisk()
        {
            if (_originalTexture == null) return null;

            string dir = Path.Combine(Application.dataPath, "_Project/Art/Models/Mascot");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "mascot_texture_debug.png");
            File.WriteAllBytes(path, _originalTexture.EncodeToPNG());
            Debug.Log($"[FaceTextureModifier] Texture saved to: {path}");
            return path;
        }

        /// <summary>
        /// 설정된 영역을 빨간 테두리로 표시 (디버그용)
        /// </summary>
        public void DebugDrawRegions()
        {
            if (!_regionsSet) return;

            RestoreRegion(_leftEyeRegion, _originalLeftEyePixels);
            RestoreRegion(_rightEyeRegion, _originalRightEyePixels);
            RestoreRegion(_mouthRegion, _originalMouthPixels);

            DrawRegionBorder(_leftEyeRegion, Color.red);
            DrawRegionBorder(_rightEyeRegion, Color.blue);
            DrawRegionBorder(_mouthRegion, Color.green);

            _workingTexture.Apply();
        }

        private void DrawRegionBorder(Rect region, Color color)
        {
            int x0 = (int)region.x;
            int y0 = (int)region.y;
            int x1 = (int)(region.x + region.width);
            int y1 = (int)(region.y + region.height);

            for (int x = x0; x <= x1; x++)
            {
                SetPixelSafe(x, y0, color);
                SetPixelSafe(x, y0 + 1, color);
                SetPixelSafe(x, y1, color);
                SetPixelSafe(x, y1 - 1, color);
            }
            for (int y = y0; y <= y1; y++)
            {
                SetPixelSafe(x0, y, color);
                SetPixelSafe(x0 + 1, y, color);
                SetPixelSafe(x1, y, color);
                SetPixelSafe(x1 - 1, y, color);
            }
        }

        #endregion

        #region Texture Copy

        private Texture2D MakeReadable(Texture2D source)
        {
            // RenderTexture를 통해 readable 복사본 생성
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return copy;
        }

        #endregion

        void OnDestroy()
        {
            if (_originalTexture != null) Destroy(_originalTexture);
            if (_workingTexture != null) Destroy(_workingTexture);
        }
    }
}
