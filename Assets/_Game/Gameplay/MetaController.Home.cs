using System;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Home screen buttons (Bug List 1, B1.a to B1.e): one end-to-end Play icon button, the Settings button just above it and the Google connect pop-up.</summary>
    public sealed partial class MetaController
    {
        /// <summary>Gap to the screen (safe area) edges and between the stacked buttons, reference px. The side margin is the normal 16 dp-ish 16 units used by every screen.</summary>
        const float HomeSideMargin = 16f, HomeBottomMargin = 24f, HomeGap = 16f;
        const float HomePlayHeight = 170f, HomeSettingsSize = 150f;
        /// <summary>Distance from the screen bottom to the underside of the Settings button (Play height + margins).</summary>
        const float HomeSettingsBottom = HomeBottomMargin + HomePlayHeight + HomeGap;

        /// <summary>Play: a green icon-only button across the whole safe width (minus the side margins) at the bottom; Settings: a blue plate with the gear above it at the right.</summary>
        void AddHomeButtons()
        {
            float h = Mathf.Max(HomePlayHeight, UiKit.MinTouchRef);
            Button play = ToyKit.IconButton(screen, "Play", "Play", 1, ToyKit.Get("icon_play"), new Vector2(900f, h), ShowMap, 0.6f);
            var prt = (RectTransform)play.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.offsetMin = new Vector2(HomeSideMargin, HomeBottomMargin);
            prt.offsetMax = new Vector2(-HomeSideMargin, HomeBottomMargin + h);
            RenameGlyphToIcon(play);

            float s = Mathf.Max(HomeSettingsSize, UiKit.MinTouchRef);
            Button settings = ToyKit.IconButton(screen, "Settings", "Settings", 2, ToyKit.Get("icon_settings"), new Vector2(s, s), ShowSettings, 0.62f);
            var srt = (RectTransform)settings.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(1f, 0f);
            srt.sizeDelta = new Vector2(s, s);
            srt.anchoredPosition = new Vector2(-HomeSideMargin, HomeBottomMargin + h + HomeGap);
            RenameGlyphToIcon(settings);
        }

        /// <summary>The painted symbol on a plate button is called "Icon" (the final OpenArt art can replace the sprite without code changes).</summary>
        static void RenameGlyphToIcon(Button b)
        {
            Transform g = b != null ? b.transform.Find("Glyph") : null;
            if (g != null) g.name = "Icon";
        }

        /// <summary>Shows the Google connect pop-up on Home when the rules allow (guest, a level completed, 3 calendar days, once per session) and no other pop-up is open.</summary>
        void MaybeShowGooglePrompt()
        {
            if (!GoogleConnectPrompt.AutoShow || popup != null || progress == null) return;
            if (!GoogleConnectPrompt.ShouldShow(progress.Data, AccountPopup.Current, GoogleConnectPrompt.ShownThisSession, DateTime.UtcNow)) return;
            ShowGooglePrompt();
            GoogleConnectPrompt.ShownThisSession = true;
            progress.MarkGooglePromptShown(DateTime.UtcNow);
        }

        /// <summary>Opens the pop-up itself (no rules): the gear goes to Settings, the cross and the back key just close it.</summary>
        public void ShowGooglePrompt()
        {
            ClosePopup();
            popup = GoogleConnectPrompt.Build(canvas.transform, () => { ClosePopup(); ShowSettings(); }, ClosePopup);
        }
    }
}
