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
    public struct TtsReadyEvent
    {
        public UnityEngine.AudioClip Clip;
        public string Text;
    }

    // ── 채팅 ──────────────────────────────────────────────
    public struct ChatResponseEvent
    {
        public string Message;
        public string SessionId;
        public string Emotion;
        public string MapUrl;
    }

    // ── 네트워크 ──────────────────────────────────────────
    public struct NetworkErrorEvent
    {
        public string Message;
        public int StatusCode;
    }

    // ── UI / 앱 상태 ──────────────────────────────────────
    public struct IdleTimeoutEvent { }

    public struct SceneReadyEvent
    {
        public string SceneName;
    }
}
