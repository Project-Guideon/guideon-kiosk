using Guideon.Core;

namespace Guideon.Mascot
{
    /// <summary>
    /// 마스코트 애니메이터 공통 인터페이스.
    /// ProceduralMascotAnimator(프로시저럴) / GltfClipAnimator(GLB 내장 클립) 두 구현이 존재.
    /// MascotLoader가 이 인터페이스를 통해 애니메이터를 제어한다.
    /// </summary>
    public interface IMascotAnimator
    {
        void SetState(MascotState state);
    }
}
