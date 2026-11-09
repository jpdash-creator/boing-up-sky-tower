using System;
using System.Collections.Generic;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The Powers tab of the Heroes screen (Bug List 6 item 3a): ALL 7 power-ups as cards in the look of the All Heroes grid (3 to a row, scrolling, the Merge screen's background and header),
    /// each with the NUMBER THE PLAYER OWNS at the bottom right of the card (<see cref="ProgressService.PowerUpCount"/>, the same counter the shop used to show). Nothing is sold here: there is no
    /// buy button; a tap on a card only opens a small popup with the power-up's name and description (the catalog text). Built once per change; no Update, no per-frame work.
    /// </summary>
    public sealed class PowersScreen
    {
        public const string ScreenName = "Powers";
        public const string InfoPopupName = "PowerInfo";
        /// <summary>Name of the owned-number text on each card ("Owned").</summary>
        public const string OwnedName = "Owned";
        const int Columns = 3, BodyFont = 40;
        const float Margin = 16f, Gap = 12f, IconS = 150f, Pad = 10f;
        static readonly Color PillInk = new Color(0.07f, 0.06f, 0.20f, 0.94f);

        /// <summary>The seven power-ups in screen order (the owner's list).</summary>
        public static readonly PowerUp[] Order =
        {
            PowerUp.Flight, PowerUp.Rocket, PowerUp.DoubleJump, PowerUp.Magnet, PowerUp.Shield, PowerUp.SlowTime, PowerUp.WallStick,
        };

        /// <summary>What the screen needs from the menu.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            /// <summary>Back returns to the Heroes screen.</summary>
            public Action Back;
            /// <summary>The other two tabs of the top row. A tab without an action is inert.</summary>
            public Action OpenAllHeroes, OpenMerge;
        }

        readonly RectTransform screen, root;
        readonly ProgressService progress;
        readonly Host host;
        RectTransform body, bg;
        ScrollRect scroll;

        /// <summary>The power-ups shown, in screen order.</summary>
        public List<PowerUpDef> Cards { get; private set; }
        /// <summary>The scroll area (tests measure it and scroll it).</summary>
        public ScrollRect Scroll { get { return scroll; } }
        /// <summary>The power-up whose name and description popup is open, or null.</summary>
        public PowerUpDef OpenInfoFor { get; private set; }

        public PowersScreen(RectTransform screen, RectTransform screenRoot, ProgressService progress, Host host)
        {
            this.screen = screen;
            this.root = screenRoot;
            this.progress = progress;
            this.host = host;
        }

        static float Line(int font) { return Mathf.Ceil(Mathf.Max(font, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.3f); }

        static RectTransform TopRow(Transform parent, string name, float y, float h, float inset)
        {
            RectTransform rt = UiKit.Rect(parent, name);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(inset, -(y + h));
            rt.offsetMax = new Vector2(-inset, -y);
            return rt;
        }

        /// <summary>Height of one card at the current text size: icon, a one-line name pill, and the row of the owned number (bottom right).</summary>
        public static float CardHeight { get { return Pad + IconS + 8f + Line(BodyFont) + 16f + CountH + Pad; } }

        static float CountH { get { return Line(BodyFont) + 8f; } }

        /// <summary>The owned count of one power-up (what the card shows).</summary>
        public int OwnedOf(PowerUpDef def)
        {
            return def != null && progress != null ? progress.PowerUpCount(def.Id) : 0;
        }

        // ------------------------------------------------------------ build

        public void Build()
        {
            if (body != null)
            {
                body.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(body.gameObject);
                else UnityEngine.Object.DestroyImmediate(body.gameObject);
                body = null;
            }
            if (bg == null) bg = MergeArt.AddBackground(root, "PowersBackground");   // the Merge screen's background (Bug List 6 item 3a)

            Cards = new List<PowerUpDef>(Order.Length);
            PowerUpCatalog cat = progress != null ? progress.PowerUpDefs : PowerUpCatalog.BuiltIn();
            for (int i = 0; i < Order.Length; i++)
            {
                PowerUpDef d = cat.Get(Order[i]);
                if (d != null && d.Implemented) Cards.Add(d);
            }

            body = UiKit.Rect(screen, "PowersBody");
            UiKit.Stretch(body);

            float bs = Mathf.Max(140f, UiKit.MinTouchRef);
            Button back = ToyKit.GlyphButton(body, "Back", "Back", 2, Glyphs.Left, new Vector2(bs, bs), () => { if (host != null && host.Back != null) host.Back(); });
            UiKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(bs, bs));
            Text title = UiKit.AddText(body, "Title", "POWERS", 72, Color.white, TextAnchor.MiddleCenter, true);
            RectTransform tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.offsetMin = new Vector2(bs + 40f, -(16f + bs));
            tr.offsetMax = new Vector2(-(bs + 40f), -16f);

            float tabsTop = 16f + bs + 8f;
            HeroTabs.Build(body, tabsTop, HeroTabs.Powers, host != null ? host.OpenAllHeroes : null, host != null ? host.OpenMerge : null, null, false);
            BuildList(tabsTop + HeroTabs.RowH + 8f);
        }

        void BuildList(float top)
        {
            RectTransform vp = UiKit.Rect(body, "ListViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(Margin, 12f);
            vp.offsetMax = new Vector2(-Margin, -top);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            RectTransform content = UiKit.Rect(vp, "ListContent");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;

            float cardH = CardHeight;
            int n = Cards.Count;
            for (int i = 0; i < n; i++)
            {
                int row = i / Columns, col = i % Columns;
                int inRow = Mathf.Min(Columns, n - row * Columns);
                float offsetCols = (Columns - inRow) * 0.5f;   // a short last row is centred, like the shop
                BuildCard(content, Cards[i], col + offsetCols, 4f + row * (cardH + Gap), cardH);
            }
            int rows = (n + Columns - 1) / Columns;
            content.sizeDelta = new Vector2(0f, 4f + rows * (cardH + Gap) + 8f);
        }

        void BuildCard(RectTransform content, PowerUpDef def, float colPos, float y, float cardH)
        {
            RectTransform inner = RarityUi.Box(content, "Power_" + def.Id, CharacterRarity.Common, false, new Vector2(100f, cardH));
            RectTransform outer = (RectTransform)inner.parent;
            outer.anchorMin = new Vector2(colPos / Columns, 1f);
            outer.anchorMax = new Vector2((colPos + 1f) / Columns, 1f);
            outer.pivot = new Vector2(0.5f, 1f);
            outer.sizeDelta = new Vector2(-Gap, cardH);
            outer.anchoredPosition = new Vector2(0f, -y);

            float lh = Line(BodyFont);
            int owned = OwnedOf(def);

            // the power-up picture, centred at the top (the code badge shows when the art is missing)
            Image icon = PowerUpUi.AddIcon(inner, "Icon", def.Type, new Vector2(0.5f, 1f), new Vector2(0f, -Pad), IconS, false);
            icon.raycastTarget = false;

            // name pill: white on dark, one line
            Image pill = UiKit.AddPanel(inner, "NamePill", PillInk);
            pill.raycastTarget = false;
            RectTransform pr = pill.rectTransform;
            pr.anchorMin = new Vector2(0.03f, 1f);
            pr.anchorMax = new Vector2(0.97f, 1f);
            pr.pivot = new Vector2(0.5f, 1f);
            pr.anchoredPosition = new Vector2(0f, -(Pad + IconS + 8f));
            pr.sizeDelta = new Vector2(0f, lh);
            Text name = UiKit.AddText(pill.transform, "PowerName", def.Name, BodyFont, Color.white, TextAnchor.MiddleCenter, true);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline no = name.GetComponent<Outline>();
            if (no != null) no.enabled = false;
            UiKit.Stretch(name.rectTransform);

            // the owned number at the bottom CENTRE of the card, full width, on its own dark badge (never over the name)
            float ch = CountH;
            Image badge = UiKit.AddPanel(inner, "OwnedBadge", PillInk);
            badge.raycastTarget = false;
            RectTransform br = badge.rectTransform;
            // Bug List 8 item 1d: as wide as the card (same 3 to 97 percent span as the name pill), centred horizontally at the bottom
            br.anchorMin = new Vector2(0.03f, 0f);
            br.anchorMax = new Vector2(0.97f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.anchoredPosition = new Vector2(0f, Pad);
            br.sizeDelta = new Vector2(0f, ch);
            Text count = UiKit.AddText(badge.transform, OwnedName, owned.ToString(), BodyFont, Color.white, TextAnchor.MiddleCenter, true);
            count.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline co = count.GetComponent<Outline>();
            if (co != null) co.enabled = false;
            UiKit.Stretch(count.rectTransform);

            // the whole card is the touch target: it opens the name and description popup
            outer.GetComponent<Image>().raycastTarget = true;
            var b = outer.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            PowerUpDef pick = def;
            b.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); ShowInfo(pick); });
            outer.gameObject.AddComponent<A11yLabel>().Text = def.Name + ", " + owned + " owned";
        }

        // ------------------------------------------------------------ details popup

        /// <summary>Opens the popup of the power-up with this id (a tap on its card); false when the id is not on the screen (tests).</summary>
        public bool OpenInfo(string id)
        {
            if (Cards == null) return false;
            foreach (PowerUpDef d in Cards)
                if (d.Id == id) { ShowInfo(d); return true; }
            return false;
        }

        /// <summary>Scrolls the list: 0 = top, 1 = bottom (tests).</summary>
        public void ScrollTo(float t)
        {
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(t);
        }

        void ShowInfo(PowerUpDef def)
        {
            if (host == null || host.NewOverlay == null || def == null) return;
            OpenInfoFor = def;
            RectTransform overlay = host.NewOverlay();
            float lTitle = Line(52), lBody = Line(BodyFont);
            const float panelW = 900f, okS = 148f;
            const int descLines = 8;   // the longest description (Rocket) wraps to about 6 lines at 130 percent text
            float h = 36f + lTitle + 14f + descLines * lBody + 14f + okS + 28f;
            RectTransform panel = UiKit.Rect(overlay, InfoPopupName);
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelW, h));
            panel.pivot = new Vector2(0.5f, 0.5f);
            Theme.CardBacking(panel);

            float y = 36f;
            Text nm = UiKit.OnPanel(UiKit.AddText(TopRow(panel, "TitleRow", y, lTitle, 24f), "PowerName", def.Name, 52, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
            nm.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(nm.rectTransform);
            y += lTitle + 14f;

            Text desc = UiKit.OnPanel(UiKit.AddText(TopRow(panel, "DescRow", y, descLines * lBody, 36f), "Description", def.Description, BodyFont, UiKit.PanelInk, TextAnchor.UpperCenter));
            desc.lineSpacing = 1f;
            UiKit.Stretch(desc.rectTransform);

            Button ok = ToyKit.IconButton(panel, "OK", "Close details", 1, Glyphs.Check, new Vector2(okS, okS), CloseInfo, 0.56f);
            UiKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(okS, okS));
            panel.gameObject.AddComponent<A11yLabel>().Text = def.Name + ". " + def.Description;
        }

        /// <summary>Closes the popup.</summary>
        public void CloseInfo()
        {
            OpenInfoFor = null;
            if (host != null && host.CloseOverlay != null) host.CloseOverlay();
        }
    }
}
