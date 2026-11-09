using System;
using System.Globalization;
using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Bug List 1, B1.c: the small pop-up that asks a guest to connect a Google account in Settings (Google sign-in lives only in Settings now).
    /// Timing (owner answers): only after the player completed a level (first test run), only while still a guest (not signed in with Google; also not in a build
    /// without sign-in), at most once every 3 calendar days (UTC date of <see cref="SaveData.LastGooglePromptUtc"/>), at most once per app session, and only on Home
    /// (never during a run). The rules are pure and unit-tested; the view is built in code: a Google-style G badge, one short line and two icon buttons.
    /// </summary>
    public static class GoogleConnectPrompt
    {
        public const string RootName = "Popup";
        public const string Message = "Keep your progress safe: connect Google in Settings.";
        public const string OpenSettingsName = "OpenSettings", LaterName = "Later";
        /// <summary>Calendar days that must pass between two prompts (the day of the last prompt does not count).</summary>
        public const int MinDaysBetween = 3;

        /// <summary>True from the first prompt on until the app restarts: one prompt per session.</summary>
        public static bool ShownThisSession;

        /// <summary>Tests turn the automatic prompt on Home off so the Home audit sees the plain Home screen.</summary>
        public static bool AutoShow = true;

        public static void ResetForTests() { ShownThisSession = false; AutoShow = true; }

        /// <summary>True once any level has been completed (the first finished run).</summary>
        public static bool HasCompletedALevel(SaveData data)
        {
            if (data == null || data.Levels == null) return false;
            foreach (LevelProgress p in data.Levels.Values)
                if (p != null && p.Clears > 0) return true;
            return false;
        }

        /// <summary>Whole UTC calendar days from the last prompt to now; a very large number when it was never shown or the stamp is unreadable.</summary>
        public static int DaysSinceLastPrompt(string lastUtc, DateTime utcNow)
        {
            DateTime last;
            if (string.IsNullOrEmpty(lastUtc) || !DateTime.TryParse(lastUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out last)) return int.MaxValue;
            return (int)(utcNow.ToUniversalTime().Date - last.ToUniversalTime().Date).TotalDays;
        }

        /// <summary>The full rule. <paramref name="state"/> must be Guest; <paramref name="shownThisSession"/> is the session flag.</summary>
        public static bool ShouldShow(SaveData data, AccountPopup.State state, bool shownThisSession, DateTime utcNow)
        {
            if (data == null || shownThisSession) return false;
            if (state != AccountPopup.State.Guest) return false;
            if (!HasCompletedALevel(data)) return false;
            int days = DaysSinceLastPrompt(data.LastGooglePromptUtc, utcNow);
            return days >= MinDaysBetween || days < 0;   // a clock set back never locks the prompt away for good
        }

        /// <summary>Builds the pop-up on its own dim layer. Settings and Later close it through the callbacks; the back key (Escape) acts as Later.</summary>
        public static RectTransform Build(Transform canvas, Action onSettings, Action onLater)
        {
            RectTransform root = UiKit.Rect(canvas, RootName);
            UiKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            root.gameObject.AddComponent<BackKey>().OnBack = onLater;

            const float W = 820f, pad = 48f, badge = 150f, gap = 24f, btn = 170f;
            float lineH = 3f * Mathf.Ceil(Mathf.Max(44f, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.5f);
            float btnSize = Mathf.Max(btn, UiKit.MinTouchRef);
            float height = pad + badge + gap + lineH + gap + 12f + btnSize + pad;

            RectTransform panel = UiKit.Rect(root, "Panel");
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, height));
            panel.pivot = new Vector2(0.5f, 0.5f);
            Theme.CardBacking(panel);

            // Google-style badge: a white disc with the blue G (the same mark as the Google sign-in button).
            Image disc = UiKit.AddImage(panel, "GoogleIcon", Theme.RoundSprite, Color.white);
            disc.type = Image.Type.Sliced;
            disc.preserveAspect = false;
            UiKit.Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -pad), new Vector2(badge, badge));
            Image ring = UiKit.AddImage(disc.transform, "Ring", Theme.RingSprite, UiKit.ButtonInk);
            ring.type = Image.Type.Sliced;
            ring.preserveAspect = false;
            UiKit.Stretch(ring.rectTransform);
            Text g = UiKit.AddText(disc.transform, "G", "<color=#4285F4>G</color>", 96, Color.black, TextAnchor.MiddleCenter, true);
            var gout = g.GetComponent<Outline>();
            if (gout != null) gout.enabled = false;
            g.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch((RectTransform)g.transform);

            float y = pad + badge + gap;
            Text line = UiKit.OnPanel(UiKit.AddText(panel, "Line", Message, 44, Color.white, TextAnchor.MiddleCenter));
            UiKit.Place((RectTransform)line.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(W - 2f * pad, lineH));
            y += lineH + gap + 12f;

            // Two icon buttons (no words): the gear opens Settings (primary, green), the cross is Later.
            Button open = ToyKit.IconButton(panel, OpenSettingsName, "Open Settings", 1, ToyKit.Get("icon_settings"), new Vector2(btnSize, btnSize), onSettings, 0.62f);
            UiKit.Place((RectTransform)open.transform, new Vector2(0.5f, 1f), new Vector2(-(btnSize * 0.5f + 30f), -y), new Vector2(btnSize, btnSize));
            Button later = ToyKit.GlyphButton(panel, LaterName, "Later", 3, Glyphs.Cross, new Vector2(btnSize, btnSize), onLater, false, 0.56f);
            UiKit.Place((RectTransform)later.transform, new Vector2(0.5f, 1f), new Vector2(btnSize * 0.5f + 30f, -y), new Vector2(btnSize, btnSize));
            return root;
        }

        /// <summary>Back key (Android back = Escape in the Input System) dismisses the pop-up like Later.</summary>
        sealed class BackKey : MonoBehaviour
        {
            public Action OnBack;

            void Update()
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame && OnBack != null) OnBack();
            }
        }
    }
}
