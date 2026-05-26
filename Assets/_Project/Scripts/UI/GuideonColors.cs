using UnityEngine;

namespace Guideon.UI
{
    public static class GuideonColors
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 컨셉 A — 한지 라이트 (Step 8 이후 기준 팔레트)
        // UNITY_DESIGN_SPEC.md §1.1 참조
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // ── 배경 ──────────────────────────────────────────
        public static readonly Color Bg         = new Color32(245, 237, 224, 255); // #F5EDE0 한지
        public static readonly Color BgDeep     = new Color32(237, 224, 204, 255); // #EDE0CC 보조 배경
        public static readonly Color BgCard     = new Color(1f, 252f/255f, 245f/255f, 0.92f); // 카드 반투명

        // ── 텍스트 ────────────────────────────────────────
        public static readonly Color Ink        = new Color32(26, 18, 8, 255);    // #1A1208 본문
        public static readonly Color InkMid     = new Color32(74, 53, 32, 255);   // #4A3520 보조
        public static readonly Color InkLight   = new Color32(138, 112, 96, 255); // #8A7060 캡션

        // ── 액센트 ────────────────────────────────────────
        public static readonly Color DancheongAccent   = new Color32(177, 74, 46, 255);   // #B14A2E 단청 주홍 ★
        public static readonly Color DancheongBlue     = new Color32(42, 75, 122, 255);   // #2A4B7A 청단청
        public static readonly Color DancheongGold     = new Color32(196, 149, 48, 255);  // #C49530 금

        // ── 테두리 / 그림자 ───────────────────────────────
        public static readonly Color Border     = new Color(90f/255f, 60f/255f, 20f/255f, 0.15f);
        public static readonly Color CardShadow = new Color(90f/255f, 60f/255f, 20f/255f, 0.08f);

        // ── 단청 띠 색 ────────────────────────────────────
        public static readonly Color DancheongStripe = new Color32(200, 64, 48, 255); // #C84030

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 레거시 — Step 7 이하 (오렌지 테마), _legacy 패널에서만 참조
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [System.Obsolete("컨셉A 한지 라이트로 전환됨. DancheongAccent 사용.")]
        public static readonly Color Primary      = HexToColor("#FF6B2C");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨.")]
        public static readonly Color PrimaryLight = HexToColor("#FF9A6C");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨.")]
        public static readonly Color PrimaryDark  = HexToColor("#E55A1B");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨.")]
        public static readonly Color PrimaryBg    = HexToColor("#FFF4EE");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨. DancheongBlue 사용.")]
        public static readonly Color Accent       = HexToColor("#0EA5E9");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨.")]
        public static readonly Color AccentLight  = HexToColor("#7DD3FC");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨.")]
        public static readonly Color AccentBg     = HexToColor("#F0F9FF");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨. Bg 사용.")]
        public static readonly Color BgMain       = HexToColor("#F8FAFC");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨. BgCard 사용.")]
        public static readonly Color BgWarm       = HexToColor("#FFFBF7");
        public static readonly Color BgOverlay    = new(0f, 0f, 0f, 0.35f);
        [System.Obsolete("컨셉A 한지 라이트로 전환됨. Border 사용.")]
        public static readonly Color BgDivider    = HexToColor("#E2E8F0");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨. Ink 사용.")]
        public static readonly Color TextPrimary  = HexToColor("#1E293B");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨. InkMid 사용.")]
        public static readonly Color TextSecondary = HexToColor("#64748B");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨. InkLight 사용.")]
        public static readonly Color TextMuted    = HexToColor("#94A3B8");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨.")]
        public static readonly Color TextOnPrimary = HexToColor("#FFFFFF");
        [System.Obsolete("컨셉A 한지 라이트로 전환됨. CardShadow 사용.")]
        public static readonly Color Shadow       = new(0.07f, 0.09f, 0.15f, 0.08f);
        [System.Obsolete("컨셉A 한지 라이트로 전환됨.")]
        public static readonly Color ShadowStrong = new(0.07f, 0.09f, 0.15f, 0.16f);
        public static readonly Color Success = HexToColor("#22C55E");
        public static readonly Color Warning = HexToColor("#F59E0B");
        public static readonly Color Error   = HexToColor("#EF4444");

        private static Color HexToColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
