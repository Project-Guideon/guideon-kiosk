using UnityEngine;

namespace Guideon.UI
{
    /// <summary>
    /// GUIDEON 키오스크 브랜드 컬러 팔레트.
    /// 밝은 테마 + 오렌지 메인 (웹과 통일).
    /// 색 조합: 오렌지(Primary) + 스카이 블루(보색 Accent) + 밝은 배경.
    /// </summary>
    public static class GuideonColors
    {
        // ── Primary (메인 브랜드 — 오렌지 계열) ───────────
        public static readonly Color Primary = HexToColor("#FF6B2C");       // 메인 오렌지
        public static readonly Color PrimaryLight = HexToColor("#FF9A6C");  // 연한 오렌지 (호버/하이라이트)
        public static readonly Color PrimaryDark = HexToColor("#E55A1B");   // 진한 오렌지 (버튼 눌림)
        public static readonly Color PrimaryBg = HexToColor("#FFF4EE");     // 오렌지 틴트 배경 (카드 강조)

        // ── Accent (보색 계열 — 스카이 블루) ──────────────
        public static readonly Color Accent = HexToColor("#0EA5E9");        // 포인트 블루
        public static readonly Color AccentLight = HexToColor("#7DD3FC");   // 연한 블루
        public static readonly Color AccentBg = HexToColor("#F0F9FF");      // 블루 틴트 배경

        // ── Background (밝은 테마) ────────────────────────
        public static readonly Color BgMain = HexToColor("#F8FAFC");        // 최상위 배경 (쿨 화이트)
        public static readonly Color BgCard = HexToColor("#FFFFFF");        // 카드/패널 배경 (순백)
        public static readonly Color BgWarm = HexToColor("#FFFBF7");        // 따뜻한 배경 (크림)
        public static readonly Color BgOverlay = new(0f, 0f, 0f, 0.35f);   // 전환 오버레이 (밝은 테마용)
        public static readonly Color BgDivider = HexToColor("#E2E8F0");     // 구분선

        // ── Text ──────────────────────────────────────────
        public static readonly Color TextPrimary = HexToColor("#1E293B");   // 본문 (다크 슬레이트)
        public static readonly Color TextSecondary = HexToColor("#64748B"); // 보조 텍스트
        public static readonly Color TextMuted = HexToColor("#94A3B8");     // 비활성/힌트 텍스트
        public static readonly Color TextOnPrimary = HexToColor("#FFFFFF"); // 오렌지 버튼 위 흰 텍스트

        // ── Shadow (밝은 테마에서 깊이감) ─────────────────
        public static readonly Color Shadow = new(0.07f, 0.09f, 0.15f, 0.08f);     // 카드 그림자
        public static readonly Color ShadowStrong = new(0.07f, 0.09f, 0.15f, 0.16f); // 강한 그림자

        // ── Status ────────────────────────────────────────
        public static readonly Color Success = HexToColor("#22C55E");
        public static readonly Color Warning = HexToColor("#F59E0B");
        public static readonly Color Error = HexToColor("#EF4444");

        // ── Util ──────────────────────────────────────────
        private static Color HexToColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
