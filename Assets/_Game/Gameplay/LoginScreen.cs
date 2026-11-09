using System;
using System.Globalization;
using FluffyClimbers.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The first-launch login screen and the "choose your save" popup, built in code (docs/35). Pure view: callers pass the actions.
    /// Layout is anchored top (logo, hero) and bottom (buttons, note) in canvas units, sized for the 3:4 notched worst case and 130 percent text (CUX-3/5).
    /// </summary>
    public static class LoginScreen
    {
        public const string RootName = "Overlay";        // the layout audit treats a root named Overlay or Popup as modal
        public const string ConflictRootName = "Popup";
        public const string GuestNote = "Guests save only on this device.";

        const float ButtonW = 840f, GoogleH = 170f, GuestH = 150f, Gap = 24f;
        static readonly Color GoogleInk = Theme.Ink;

        /// <summary>Handles to the parts a caller updates after the screen is built.</summary>
        public sealed class LoginView
        {
            public RectTransform Root;
            public Button Google, Guest;
            public Text Status;
        }

        /// <summary>Full-screen login: sky, logo, the chosen hero, status line, the two buttons and the one-line note.</summary>
        public static LoginView BuildLogin(Transform canvas, ArtCatalog art, bool googleAvailable, Action onGoogle, Action onGuest)
        {
            var view = new LoginView();
            RectTransform root = UiKit.Rect(canvas, RootName);
            UiKit.Stretch(root);
            view.Root = root;
            Theme.SkyBackground(root);
            RectTransform safe = UiKit.SafeArea(root);

            Sprite logo = ToyKit.Get("logo");
            if (logo != null)
            {
                Image lg = UiKit.AddImage(safe, "Logo", logo, Color.white);
                UiKit.Place(lg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 327f));
                lg.rectTransform.pivot = new Vector2(0.5f, 1f);
                lg.raycastTarget = false;
            }
            else
            {
                Text title = UiKit.AddText(safe, "Title", "FLUFFY CLIMBERS\nTOWER JUMP", 90, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(840f, 260f));
            }
            if (art != null)
            {
                RectTransform doll = MomoDoll.Build(safe, "Hero", 400f, CharacterCatalog.Selected, art);
                doll.anchorMin = doll.anchorMax = new Vector2(0.5f, 1f);
                doll.pivot = new Vector2(0.5f, 1f);
                doll.anchoredPosition = new Vector2(0f, -430f);
            }

            // bottom stack, from the bottom: note, guest, Google, status
            float y = 20f;
            Text note = UiKit.AddText(safe, "Note", GuestNote, 40, Theme.Ink, TextAnchor.MiddleCenter);
            DisableOutline(note);
            UiKit.Place((RectTransform)note.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(840f, 140f));
            y += 140f + Gap;

            view.Guest = UiKit.AddButton(safe, "Guest", "Play as guest", ToyKit.Button(0), Color.white, new Vector2(ButtonW, GuestH), onGuest, 46);
            UiKit.Place((RectTransform)view.Guest.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(ButtonW, GuestH));
            y += GuestH + Gap;

            if (googleAvailable)
            {
                view.Google = BuildGoogleButton(safe, "Sign in with Google Play", onGoogle);
                UiKit.Place((RectTransform)view.Google.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(ButtonW, GoogleH));
                y += GoogleH + Gap;
            }

            view.Status = UiKit.AddText(safe, "Status", "", 40, Theme.Ink, TextAnchor.MiddleCenter);
            DisableOutline(view.Status);
            UiKit.Place((RectTransform)view.Status.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(840f, 120f));
            return view;
        }

        /// <summary>The Google-style button: white pill, a coloured G badge and dark text (the label wraps on two lines at 130 percent text).</summary>
        public static Button BuildGoogleButton(Transform parent, string label, Action onClick)
        {
            Button b = UiKit.AddButton(parent, "Google", null, UiKit.PillSprite, Color.white, new Vector2(ButtonW, GoogleH), onClick);
            b.GetComponent<Image>().color = Color.white;
            b.GetComponent<A11yLabel>().Text = label;
            Text g = UiKit.AddText(b.transform, "G", "<color=#4285F4>G</color>", 80, GoogleInk, TextAnchor.MiddleCenter, true);
            DisableOutline(g);
            var gr = (RectTransform)g.transform;
            gr.anchorMin = new Vector2(0f, 0f); gr.anchorMax = new Vector2(0f, 1f);
            gr.pivot = new Vector2(0f, 0.5f);
            gr.offsetMin = new Vector2(30f, 10f); gr.offsetMax = new Vector2(150f, -10f);
            Text t = UiKit.AddText(b.transform, "Label", label, 44, GoogleInk, TextAnchor.MiddleLeft, true);
            DisableOutline(t);
            var tr = (RectTransform)t.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(170f, 10f); tr.offsetMax = new Vector2(-30f, -10f);
            return b;
        }

        // ---------------- choose your save ----------------

        public static string N0(int v) { return v.ToString("N0", CultureInfo.InvariantCulture); }

        /// <summary>The summary as the card shows it: four short lines (levels, gems, coins, heroes and date) (none can wrap at 130 percent text).</summary>
        public static string SummaryText(SaveSummary s)
        {
            return N0(s.LevelsCleared) + (s.LevelsCleared == 1 ? " level cleared" : " levels cleared") + "\n"
                 + "Gems: " + N0(s.Gems) + "\n"
                 + "Coins: " + N0(s.Coins) + "\n"
                 + N0(s.HeroCopies) + (s.HeroCopies == 1 ? " hero, " : " heroes, ") + SaveSync.LastPlayedShort(s);
        }

        /// <summary>Height a block of text needs: lines times the scaled font size times a generous line factor (measured: Unity's line is about 1.4 times the font size).</summary>
        static float Lh(int font, int lines)
        {
            return lines * Mathf.Ceil(Mathf.Max(font, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.5f);
        }

        /// <summary>A modal popup with two cards (this device, Google account), each with its summary and its own button. Every height comes from the text size, so it holds at 130 percent.</summary>
        public static RectTransform BuildConflict(Transform canvas, SaveSummary device, SaveSummary google, string googleName, Action keepDevice, Action useGoogle)
        {
            RectTransform root = UiKit.Rect(canvas, ConflictRootName);
            UiKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.05f, 0.14f, 0.82f);
            RectTransform safe = UiKit.SafeArea(root);

            const float W = 880f, Sp = 16f, Pad = 16f, BtnH = 130f;
            const int HeadFont = 44, TitleFont = 44, BodyFont = 36;
            float headH = Lh(HeadFont, 3) + 8f;
            float cardH = Pad + Lh(TitleFont, 1) + Lh(BodyFont, 4) + Pad + BtnH + Pad;
            RectTransform block = UiKit.Rect(safe, "Block");
            block.anchorMin = block.anchorMax = new Vector2(0.5f, 0.5f);
            block.pivot = new Vector2(0.5f, 0.5f);
            block.sizeDelta = new Vector2(W, headH + Sp + cardH + Sp + cardH);
            block.anchoredPosition = Vector2.zero;

            Text head = UiKit.AddText(block, "Header", "Which save do you want to keep? The other one is replaced.", HeadFont, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)head.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(W, headH));

            float top = headH + Sp;
            BuildCard(block, "DeviceCard", "This device", device, "Keep this device", top, W, cardH, TitleFont, BodyFont, BtnH, Pad, 0, keepDevice);
            top += cardH + Sp;
            BuildCard(block, "GoogleCard", "Google account", google, "Use Google save", top, W, cardH, TitleFont, BodyFont, BtnH, Pad, 1, useGoogle);
            top += cardH + Sp;
            return root;
        }

        static void BuildCard(RectTransform block, string name, string title, SaveSummary s, string buttonLabel, float top, float w, float h, int titleFont, int bodyFont, float btnH, float pad, int role, Action onPick)
        {
            RectTransform card = UiKit.Rect(block, name);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.sizeDelta = new Vector2(w, h);
            card.anchoredPosition = new Vector2(0f, -top);
            Theme.CardBacking(card);

            float titleH = Lh(titleFont, 1), bodyH = Lh(bodyFont, 4);
            Text t = UiKit.AddText(card, "Title", title, titleFont, Theme.Ink, TextAnchor.MiddleCenter, true);
            DisableOutline(t);
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(0f, -pad), new Vector2(w - 60f, titleH));

            Text body = UiKit.AddText(card, "Summary", SummaryText(s), bodyFont, Theme.Ink, TextAnchor.UpperCenter);
            DisableOutline(body);
            UiKit.Place((RectTransform)body.transform, new Vector2(0.5f, 1f), new Vector2(0f, -(pad + titleH)), new Vector2(w - 60f, bodyH));

            Button b = UiKit.AddButton(card, buttonLabel, buttonLabel, ToyKit.Button(role == 1 ? 1 : 0), Color.white, new Vector2(w - 120f, btnH), onPick, 46);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0f, pad), new Vector2(w - 120f, btnH));
        }

        static string Shorten(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max - 1) + "...";
        }

        static void DisableOutline(Text t)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }
    }
}
