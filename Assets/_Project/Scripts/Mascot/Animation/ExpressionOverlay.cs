using System.Collections.Generic;
using UnityEngine;

namespace Guideon.Mascot
{
    /// <summary>
    /// Head bone 앞에 2D 표정 스프라이트를 표시하는 오버레이 시스템.
    /// BlendShape 없는 모델에서 표정을 표현하기 위한 대안.
    ///
    /// 동작 방식:
    /// - Head bone에 빌보드 Quad를 부착
    /// - 상태(MascotState)에 따라 눈/입 텍스처를 교체
    /// - 눈 깜빡임 자동 처리
    /// </summary>
    public class ExpressionOverlay : MonoBehaviour
    {
        [Header("Overlay Settings")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.05f, 0.15f);
        [SerializeField] private float overlayScale = 0.3f;
        [SerializeField] private float eyeBlinkInterval = 3.5f;
        [SerializeField] private float eyeBlinkDuration = 0.12f;

        private Transform _headBone;
        private GameObject _overlayQuad;
        private MeshRenderer _overlayRenderer;
        private Material _overlayMaterial;

        private MascotState _currentState = MascotState.Idle;
        private Dictionary<string, Texture2D> _expressionTextures = new();

        private float _blinkTimer;
        private bool _isBlinking;
        private float _blinkEndTime;

        // 말하기 입 애니메이션
        private float _mouthTimer;

        public void Initialize(Transform headBone)
        {
            _headBone = headBone;
            if (_headBone == null)
            {
                Debug.LogWarning("[ExpressionOverlay] Head bone is null, overlay disabled.");
                return;
            }

            CreateOverlayQuad();
            GenerateAllExpressionTextures();
            ApplyExpression(MascotState.Idle);
        }

        public void SetState(MascotState state)
        {
            _currentState = state;
            _mouthTimer = 0f;
            ApplyExpression(state);
        }

        void Update()
        {
            if (_headBone == null || _overlayQuad == null) return;

            UpdateBillboard();
            UpdateEyeBlink();
            UpdateMouthAnimation();
        }

        private void CreateOverlayQuad()
        {
            _overlayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _overlayQuad.name = "ExpressionOverlay";
            _overlayQuad.transform.SetParent(_headBone, false);
            _overlayQuad.transform.localPosition = localOffset;
            _overlayQuad.transform.localScale = Vector3.one * overlayScale;

            // Collider 제거
            var collider = _overlayQuad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // 반투명 Unlit 머티리얼
            _overlayRenderer = _overlayQuad.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _overlayMaterial = new Material(shader);
            _overlayMaterial.SetFloat("_Surface", 1); // Transparent (URP)
            _overlayMaterial.SetFloat("_Blend", 0); // Alpha
            _overlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _overlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _overlayMaterial.SetInt("_ZWrite", 0);
            _overlayMaterial.renderQueue = 3000;
            _overlayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            _overlayRenderer.material = _overlayMaterial;
        }

        private void UpdateBillboard()
        {
            // 항상 카메라를 바라보도록
            if (Camera.main != null)
            {
                _overlayQuad.transform.LookAt(
                    _overlayQuad.transform.position + Camera.main.transform.forward,
                    Camera.main.transform.up
                );
            }
        }

        #region Eye Blink

        private void UpdateEyeBlink()
        {
            if (_currentState == MascotState.Speaking) return; // 말할 때는 별도 처리

            _blinkTimer += Time.deltaTime;

            if (_isBlinking)
            {
                if (Time.time > _blinkEndTime)
                {
                    _isBlinking = false;
                    ApplyExpression(_currentState);
                }
            }
            else if (_blinkTimer >= eyeBlinkInterval)
            {
                _blinkTimer = 0f;
                _isBlinking = true;
                _blinkEndTime = Time.time + eyeBlinkDuration;

                // 눈 감은 텍스처 적용
                string blinkKey = $"{_currentState}_blink";
                if (_expressionTextures.ContainsKey(blinkKey))
                {
                    _overlayMaterial.mainTexture = _expressionTextures[blinkKey];
                }
            }
        }

        #endregion

        #region Mouth Animation

        private void UpdateMouthAnimation()
        {
            if (_currentState != MascotState.Speaking) return;

            _mouthTimer += Time.deltaTime;

            // 말하는 동안 입 열림/닫힘 텍스처 교체
            float mouthPhase = Mathf.Sin(_mouthTimer * 8f);
            bool mouthOpen = mouthPhase > 0f;

            string key = mouthOpen ? "Speaking_open" : "Speaking";
            if (_expressionTextures.ContainsKey(key))
            {
                _overlayMaterial.mainTexture = _expressionTextures[key];
            }
        }

        #endregion

        #region Expression Texture Generation

        private void ApplyExpression(MascotState state)
        {
            string key = state.ToString();
            if (_expressionTextures.ContainsKey(key) && _overlayMaterial != null)
            {
                _overlayMaterial.mainTexture = _expressionTextures[key];
            }
        }

        private void GenerateAllExpressionTextures()
        {
            int size = 256;

            // Idle: 보통 눈, 보통 입
            _expressionTextures["Idle"] = GenerateExpressionTexture(size,
                EyeShape.Normal, MouthShape.Neutral);
            _expressionTextures["Idle_blink"] = GenerateExpressionTexture(size,
                EyeShape.Closed, MouthShape.Neutral);

            // Greeting: 웃는 눈, 웃는 입
            _expressionTextures["Greeting"] = GenerateExpressionTexture(size,
                EyeShape.Happy, MouthShape.Smile);
            _expressionTextures["Greeting_blink"] = GenerateExpressionTexture(size,
                EyeShape.Closed, MouthShape.Smile);

            // Listening: 큰 눈, 작은 입
            _expressionTextures["Listening"] = GenerateExpressionTexture(size,
                EyeShape.Wide, MouthShape.Small);
            _expressionTextures["Listening_blink"] = GenerateExpressionTexture(size,
                EyeShape.Closed, MouthShape.Small);

            // Thinking: 올려다보는 눈, 일자 입
            _expressionTextures["Thinking"] = GenerateExpressionTexture(size,
                EyeShape.LookUp, MouthShape.Hmm);
            _expressionTextures["Thinking_blink"] = GenerateExpressionTexture(size,
                EyeShape.Closed, MouthShape.Hmm);

            // Speaking: 보통 눈, 열린 입 / 닫힌 입
            _expressionTextures["Speaking"] = GenerateExpressionTexture(size,
                EyeShape.Normal, MouthShape.Neutral);
            _expressionTextures["Speaking_open"] = GenerateExpressionTexture(size,
                EyeShape.Normal, MouthShape.Open);
            _expressionTextures["Speaking_blink"] = GenerateExpressionTexture(size,
                EyeShape.Closed, MouthShape.Open);
        }

        private enum EyeShape { Normal, Closed, Happy, Wide, LookUp }
        private enum MouthShape { Neutral, Smile, Small, Open, Hmm }

        private Texture2D GenerateExpressionTexture(int size, EyeShape eyes, MouthShape mouth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            // 투명 배경
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // 눈 그리기
            DrawEyes(pixels, size, eyes);

            // 입 그리기
            DrawMouth(pixels, size, mouth);

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private void DrawEyes(Color[] pixels, int size, EyeShape shape)
        {
            int eyeY = (int)(size * 0.6f);
            int leftEyeX = (int)(size * 0.3f);
            int rightEyeX = (int)(size * 0.7f);
            Color eyeColor = Color.black;

            switch (shape)
            {
                case EyeShape.Normal:
                    DrawOval(pixels, size, leftEyeX, eyeY, 14, 18, eyeColor);
                    DrawOval(pixels, size, rightEyeX, eyeY, 14, 18, eyeColor);
                    // 하이라이트
                    DrawOval(pixels, size, leftEyeX + 4, eyeY + 5, 5, 5, Color.white);
                    DrawOval(pixels, size, rightEyeX + 4, eyeY + 5, 5, 5, Color.white);
                    break;

                case EyeShape.Closed:
                    // 눈 감은 상태 - 가로선
                    DrawLine(pixels, size, leftEyeX - 12, eyeY, leftEyeX + 12, eyeY, 3, eyeColor);
                    DrawLine(pixels, size, rightEyeX - 12, eyeY, rightEyeX + 12, eyeY, 3, eyeColor);
                    break;

                case EyeShape.Happy:
                    // 웃는 눈 - 아래로 볼록한 곡선 (∪ 모양 뒤집기 → ∩)
                    DrawArc(pixels, size, leftEyeX, eyeY, 12, true, eyeColor);
                    DrawArc(pixels, size, rightEyeX, eyeY, 12, true, eyeColor);
                    break;

                case EyeShape.Wide:
                    // 큰 눈
                    DrawOval(pixels, size, leftEyeX, eyeY, 18, 22, eyeColor);
                    DrawOval(pixels, size, rightEyeX, eyeY, 18, 22, eyeColor);
                    DrawOval(pixels, size, leftEyeX + 4, eyeY + 6, 6, 6, Color.white);
                    DrawOval(pixels, size, rightEyeX + 4, eyeY + 6, 6, 6, Color.white);
                    break;

                case EyeShape.LookUp:
                    // 위를 보는 눈 - 눈동자가 위로
                    DrawOval(pixels, size, leftEyeX, eyeY, 14, 18, eyeColor);
                    DrawOval(pixels, size, rightEyeX, eyeY, 14, 18, eyeColor);
                    DrawOval(pixels, size, leftEyeX, eyeY + 8, 6, 6, Color.white);
                    DrawOval(pixels, size, rightEyeX, eyeY + 8, 6, 6, Color.white);
                    break;
            }
        }

        private void DrawMouth(Color[] pixels, int size, MouthShape shape)
        {
            int mouthX = size / 2;
            int mouthY = (int)(size * 0.3f);
            Color mouthColor = new Color(0.2f, 0.1f, 0.1f, 1f);

            switch (shape)
            {
                case MouthShape.Neutral:
                    DrawLine(pixels, size, mouthX - 10, mouthY, mouthX + 10, mouthY, 2, mouthColor);
                    break;

                case MouthShape.Smile:
                    DrawArc(pixels, size, mouthX, mouthY, 14, false, mouthColor);
                    break;

                case MouthShape.Small:
                    DrawOval(pixels, size, mouthX, mouthY, 6, 6, mouthColor);
                    break;

                case MouthShape.Open:
                    DrawOval(pixels, size, mouthX, mouthY, 12, 16, mouthColor);
                    // 입 안 빨간색
                    DrawOval(pixels, size, mouthX, mouthY, 8, 12, new Color(0.6f, 0.15f, 0.15f, 1f));
                    break;

                case MouthShape.Hmm:
                    // ~ 모양 입
                    for (int x = -12; x <= 12; x++)
                    {
                        int wavY = (int)(Mathf.Sin(x * 0.3f) * 3f);
                        DrawPixelSafe(pixels, size, mouthX + x, mouthY + wavY, mouthColor);
                        DrawPixelSafe(pixels, size, mouthX + x, mouthY + wavY + 1, mouthColor);
                    }
                    break;
            }
        }

        #endregion

        #region Drawing Primitives

        private void DrawOval(Color[] pixels, int size, int cx, int cy, int rx, int ry, Color color)
        {
            for (int y = -ry; y <= ry; y++)
            {
                for (int x = -rx; x <= rx; x++)
                {
                    float dx = (float)x / rx;
                    float dy = (float)y / ry;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        DrawPixelSafe(pixels, size, cx + x, cy + y, color);
                    }
                }
            }
        }

        private void DrawLine(Color[] pixels, int size, int x0, int y0, int x1, int y1, int thickness, Color color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (steps == 0) steps = 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                int px = (int)Mathf.Lerp(x0, x1, t);
                int py = (int)Mathf.Lerp(y0, y1, t);
                for (int dy = -thickness / 2; dy <= thickness / 2; dy++)
                {
                    for (int dx = -thickness / 2; dx <= thickness / 2; dx++)
                    {
                        DrawPixelSafe(pixels, size, px + dx, py + dy, color);
                    }
                }
            }
        }

        private void DrawArc(Color[] pixels, int size, int cx, int cy, int radius, bool flipY, Color color)
        {
            // 반원 곡선
            for (int x = -radius; x <= radius; x++)
            {
                float normX = (float)x / radius;
                float yVal = Mathf.Sqrt(Mathf.Max(0, 1f - normX * normX)) * radius * 0.5f;
                int py = flipY ? (int)(cy + yVal) : (int)(cy - yVal);
                int thickness = 3;
                for (int t = 0; t < thickness; t++)
                {
                    DrawPixelSafe(pixels, size, cx + x, py + t, color);
                }
            }
        }

        private void DrawPixelSafe(Color[] pixels, int size, int x, int y, Color color)
        {
            if (x < 0 || x >= size || y < 0 || y >= size) return;
            pixels[y * size + x] = color;
        }

        #endregion

        void OnDestroy()
        {
            foreach (var tex in _expressionTextures.Values)
            {
                if (tex != null) Destroy(tex);
            }
            _expressionTextures.Clear();

            if (_overlayMaterial != null) Destroy(_overlayMaterial);
            if (_overlayQuad != null) Destroy(_overlayQuad);
        }
    }
}
