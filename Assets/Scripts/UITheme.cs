// ============================================================================================================
// UITheme.cs
// A ScriptableObject that stores every colour used by the UI and the board (Assets/Data/UITheme.asset).
// Re-skinning the game = editing this asset. Nothing about colours is hard-coded in the UI scripts.
// ============================================================================================================

using UnityEngine;

namespace BlocXplode
{
    [CreateAssetMenu(fileName = "UITheme", menuName = "BlocXplode/UI Theme")]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Screen & panels")]
        public Color background = new Color(0.10f, 0.07f, 0.24f, 1f);       // menu background
        public Color panel = new Color(0.20f, 0.15f, 0.46f, 1f);            // cards and HUD chips
        public Color panelDark = new Color(0.13f, 0.10f, 0.32f, 1f);        //  inner panels
        public Color dim = new Color(0f, 0f, 0f, 0.78f);                    // full-screen overlay behind pop-ups

        [Header("Text")]
        public Color textPrimary = Color.white;                             // main text
        public Color textDim = new Color(0.72f, 0.70f, 0.95f, 1f);          // small captions
        public Color accent = new Color(1f, 0.82f, 0.25f, 1f);              // highlighted numbers and the logo
        public Color accentAlt = new Color(1f, 0.45f, 0.20f, 1f);           // second logo colour

        [Header("Buttons")]
        public Color buttonPrimary = new Color(0.30f, 0.80f, 0.35f, 1f);    // play / next
        public Color buttonSecondary = new Color(0.27f, 0.55f, 0.97f, 1f);  // menu
        public Color buttonRetry = new Color(1f, 0.58f, 0.18f, 1f);         // restart
        public Color buttonQuiet = new Color(0.42f, 0.38f, 0.70f, 1f);      // small actions

        [Header("Result screens")]
        public Color win = new Color(0.30f, 0.80f, 0.35f, 1f);              // victory banner
        public Color lose = new Color(0.92f, 0.30f, 0.30f, 1f);             // defeat banner

        [Header("Board")]
        public Color boardFrame = new Color(0.98f, 0.68f, 0.20f, 1f);       // outer golden frame
        public Color boardInner = new Color(0.12f, 0.09f, 0.30f, 1f);       // board background behind the blocks
    }
}
