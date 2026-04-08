namespace Guideon.Network
{
    /// <summary>
    /// Kiosk BFF API 엔드포인트 경로 상수.
    /// BaseUrl 뒤에 붙는 경로만 정의 (예: "/kiosk/auth/verify").
    /// </summary>
    public static class KioskApiEndpoints
    {
        // ── Pairing (인증 불필요) ─────────────────────────
        public const string PairingRequest = "/kiosk/pairing/request";
        public static string PairingStatus(string code) => $"/kiosk/pairing/{code}/status";
        public static string PairingClaim(string code) => $"/kiosk/pairing/{code}/claim";

        // ── Auth ──────────────────────────────────────────
        public const string AuthVerify = "/kiosk/auth/verify";

        // ── Bootstrap ─────────────────────────────────────
        public const string Bootstrap = "/kiosk/bootstrap";

        // ── Mascot ────────────────────────────────────────
        public const string Mascot = "/kiosk/mascot";

        // ── Heartbeat ─────────────────────────────────────
        public const string Heartbeat = "/kiosk/heartbeat";

        // ── Chat ──────────────────────────────────────────
        public const string ChatSessions = "/kiosk/chat/sessions";
        public static string ChatMessages(string sessionId) => $"/kiosk/chat/sessions/{sessionId}/messages";
    }
}
