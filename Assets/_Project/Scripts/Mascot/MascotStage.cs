using Guideon.Core;
using UnityEngine;

namespace Guideon.Mascot
{
    /// <summary>
    /// 3D 마스코트를 UI Toolkit에 표시하기 위한 RenderTexture 브릿지.
    ///
    /// 역할:
    ///   1. 전용 카메라(_mascotCamera)가 렌더링할 RenderTexture를 Awake에서 생성.
    ///   2. MascotStateEvent를 구독해 MascotLoader로 상태 전달.
    ///   3. 외부(MainSceneController)에서 Texture를 가져다 VisualElement에 주입.
    ///
    /// 씬 배치:
    ///   Tools > GUIDEON > Setup Mascot Stage 메뉴로 자동 생성.
    ///   MascotStage(루트) → MascotCamera(자식), MascotLight(자식), MascotLoader(자식)
    /// </summary>
    [DisallowMultipleComponent]
    public class MascotStage : MonoBehaviour
    {
        [Header("3D 스테이지")]
        [SerializeField] private Camera _mascotCamera;
        [SerializeField] private MascotLoader _loader;

        [Header("RenderTexture 해상도")]
        [SerializeField] private int _rtWidth  = 1024;
        [SerializeField] private int _rtHeight = 1024;

        /// <summary>UI Toolkit VisualElement.backgroundImage에 주입할 텍스처.</summary>
        public RenderTexture Texture { get; private set; }

        /// <summary>
        /// 현재 씬의 MascotStage 인스턴스.
        /// 화면 컨트롤러가 Inspector 배선 없이 텍스처를 가져갈 때 사용.
        /// </summary>
        public static MascotStage Active { get; private set; }

        // ── 생명주기 ─────────────────────────────────────────

        private void Awake()
        {
            Active = this;
            CreateRenderTexture();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<MascotStateEvent>(OnMascotState);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MascotStateEvent>(OnMascotState);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (Texture != null)
            {
                Texture.Release();
                Destroy(Texture);
                Texture = null;
            }
        }

        // ── RT 생성 ──────────────────────────────────────────

        private void CreateRenderTexture()
        {
            Texture = new RenderTexture(_rtWidth, _rtHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2,
                name = "MascotRT"
            };
            Texture.Create();

            if (_mascotCamera != null)
            {
                _mascotCamera.targetTexture = Texture;
                // SolidColor + 알파 0 → 배경이 투명. URP에서는 추가 설정이 필요할 수 있음
                // (가이드 문서의 URP 투명 배경 섹션 참고)
                _mascotCamera.clearFlags    = CameraClearFlags.SolidColor;
                _mascotCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                Debug.LogWarning("[MascotStage] _mascotCamera가 연결되지 않았습니다. Inspector를 확인하세요.");
            }
        }

        // ── 상태 이벤트 포워딩 ───────────────────────────────

        private void OnMascotState(MascotStateEvent e)
        {
            _loader?.SetState(e.State);
        }

        // ── Public API ───────────────────────────────────────

        /// <summary>GLB 로드 후 Greeting 애니메이션 1회 재생.</summary>
        public void PlayGreeting()
        {
            _loader?.SetState(MascotState.Greeting);
        }

        /// <summary>포즈를 Idle로 리셋 (화면 복귀 시 호출).</summary>
        public void ResetToIdle()
        {
            _loader?.SetState(MascotState.Idle);
        }

        // ── Editor 배선용 헬퍼 ───────────────────────────────

#if UNITY_EDITOR
        /// <summary>Editor 스크립트에서 참조를 주입할 때 사용.</summary>
        public void Editor_SetRefs(Camera cam, MascotLoader loader)
        {
            _mascotCamera = cam;
            _loader       = loader;
        }
#endif
    }
}
