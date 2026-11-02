using System;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>The rectangle every character sits in: a rounded box in the rarity colour with a seam (border) around it (docs/21).</summary>
    public static class RarityUi
    {
        /// <summary>Common light grey, Uncommon light green, Rare blue, Epic purple, Legendary yellow gold.</summary>
        public static Color Fill(CharacterRarity r)
        {
            switch (r)
            {
                case CharacterRarity.Uncommon: return new Color(0.70f, 0.90f, 0.62f, 1f);
                case CharacterRarity.Rare: return new Color(0.33f, 0.58f, 0.98f, 1f);
                case CharacterRarity.Epic: return new Color(0.62f, 0.36f, 0.88f, 1f);
                case CharacterRarity.Legendary: return new Color(1f, 0.82f, 0.22f, 1f);
                default: return new Color(0.84f, 0.85f, 0.87f, 1f);
            }
        }

        /// <summary>White seam for Common, Uncommon and Rare; golden seam for Epic and Legendary.</summary>
        public static Color Seam(CharacterRarity r)
        {
            return r >= CharacterRarity.Epic ? new Color(0.86f, 0.60f, 0.06f, 1f) : Color.white;
        }

        /// <summary>A box with its seam; returns the inner area (children go there). <paramref name="size"/> is the outer size.</summary>
        public static RectTransform Box(Transform parent, string name, CharacterRarity rarity, bool locked, Vector2 size)
        {
            Image outer = UiKit.AddImage(parent, name, Theme.RoundSprite, locked ? new Color(0.45f, 0.48f, 0.60f, 1f) : Seam(rarity));
            outer.type = Image.Type.Sliced;
            outer.preserveAspect = false;
            outer.pixelsPerUnitMultiplier = 1.4f;
            outer.rectTransform.sizeDelta = size;
            Image inner = UiKit.AddImage(outer.transform, "Fill", Theme.RoundSprite, locked ? new Color(0.11f, 0.14f, 0.28f, 0.96f) : Fill(rarity));
            inner.type = Image.Type.Sliced;
            inner.preserveAspect = false;
            inner.pixelsPerUnitMultiplier = 1.6f;
            inner.raycastTarget = false;
            inner.rectTransform.anchorMin = Vector2.zero;
            inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = new Vector2(8f, 8f);
            inner.rectTransform.offsetMax = new Vector2(-8f, -8f);
            return inner.rectTransform;
        }
    }

    /// <summary>
    /// The one-time "New hero joins you!" card (docs/21): hero picture, name, rarity and one OK button (added by the caller under the returned "Panel").
    /// Background: ONE place to swap in art, <see cref="BackgroundResource"/> (Resources/Art/Ui/hero_reveal_bg); until the file exists the card_yellow
    /// panel of the Rewards art is used (or a plain rounded shape). The only motion is a short pop-in, skipped with reduced motion; nothing flashes (CUX-6).
    /// </summary>
    public static class HeroRevealCard
    {
        public const string BackgroundResource = "Art/Ui/hero_reveal_bg";
        public const string Title = "New hero joins you!";
        public const float CardW = 860f, CardH = 1300f, DollHeight = 520f;
        /// <summary>Card width when the picture is used, for every aspect: the narrowest supported screen (9:21 at the 1080 x 1920 reference, match 0.5) is about 942 px wide, so 860 keeps 40 px of margin. The height follows the picture's aspect.</summary>
        public const float PictureCardW = CardW;
        // Layout on the hero_reveal_bg picture (1024 x 1135, fractions of its size): the ribbon at 0.24 to 0.365 of the height, the purple panel (empty centre glow) at 0.35 to 0.80.
        // The OK button (AddOk, 160 px: the card is shrunk up to about 13 percent on narrow aspects and the button must stay above the 48 dp minimum) sits just under the purple panel on the sparse bottom rays.
        public const float OkSize = 148f;
        const float RibbonCenterY = 0.305f, RibbonTextW = 420f, RibbonTextH = 112f;
        const float PicDollTop = 362f, PicDollH = 220f, PicNameTop = 588f, PicGap = 24f;
        static readonly Color PillInk = new Color(0.09f, 0.11f, 0.24f, 0.92f);

        /// <summary>Pop-in: scale 0.88 to 1 in 0.25 s on unscaled time. One-shot, no flashing.</summary>
        sealed class PopIn : MonoBehaviour
        {
            float t;
            void Update()
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.25f);
                transform.localScale = Vector3.one * (0.88f + 0.12f * (1f - (1f - k) * (1f - k)));
                if (k >= 1f) { transform.localScale = Vector3.one; Destroy(this); }
            }
        }

        /// <summary>Builds the card on the (dim) overlay and returns its "Panel"; the caller adds the OK button.</summary>
        public static RectTransform Build(RectTransform overlay, CharacterDef who, ArtCatalog art)
        {
            RectTransform panel = UiKit.Rect(overlay, "Panel");
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));
            panel.pivot = new Vector2(0.5f, 0.5f);

            // the swap point for the OpenArt background
            Image bg = UiKit.AddImage(panel, "Fill", null, Color.white);
            Sprite custom = Resources.Load<Sprite>(BackgroundResource);
            if (custom != null)
            {
                // the whole picture (transparent around the sunburst) is the card: aspect preserved, so the card takes the picture's shape
                float aspect = custom.rect.height / Mathf.Max(1f, custom.rect.width);
                UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PictureCardW, Mathf.Round(PictureCardW * aspect)));
                panel.pivot = new Vector2(0.5f, 0.5f);
                bg.sprite = custom;
                bg.color = Color.white;
                bg.preserveAspect = true;
                bg.type = Image.Type.Simple;
            }
            else RewardArt.Skin(bg, "card_yellow", new Color(1f, 0.93f, 0.62f, 1f), 56f);
            UiKit.Stretch(bg.rectTransform);

            CharacterSpec info = who.Info;
            CharacterRarity rarity = info != null ? info.Rarity : CharacterRarity.Common;
            string rarityName = CharacterDefs.RarityNames[(int)rarity];

            if (custom != null) BuildOnPicture(panel, who, art, rarity, rarityName);
            else BuildOnPanel(panel, who, art, rarity, rarityName);

            panel.gameObject.AddComponent<A11yLabel>().Text = Title + " " + who.Name + ", " + rarityName;
            if (!GameSettings.ReducedMotion) panel.gameObject.AddComponent<PopIn>();
            return panel;
        }

        /// <summary>The single OK button of the card, a child of the "Panel" (name "OK"), centred at the bottom; with the picture it sits right under the purple panel.</summary>
        public static Button AddOk(RectTransform panel, Action action)
        {
            bool pic = panel.sizeDelta.y < CardH - 1f;
            Button b = ToyKit.IconButton(panel, "OK", "OK", 1, Glyphs.Check, new Vector2(OkSize, OkSize), action, 0.56f);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0f, pic ? 14f : 50f), new Vector2(OkSize, OkSize));
            return b;
        }

        /// <summary>The plain card (no picture): the original stacked layout on the card_yellow panel.</summary>
        static void BuildOnPanel(RectTransform panel, CharacterDef who, ArtCatalog art, CharacterRarity rarity, string rarityName)
        {
            Text title = UiKit.OnPanel(UiKit.AddText(panel, "Title", Title, 64, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
            UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(780f, 210f));

            RectTransform doll = MomoDoll.Build(panel, "Hero", DollHeight, who, art);
            UiKit.Place(doll, new Vector2(0.5f, 1f), new Vector2(0f, -270f), doll.sizeDelta);

            Text name = UiKit.OnPanel(UiKit.AddText(panel, "HeroName", who.Name, 64, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
            UiKit.Place((RectTransform)name.transform, new Vector2(0.5f, 1f), new Vector2(0f, -810f), new Vector2(780f, 110f));

            // rarity chip: the Common colour of the Heroes screen, with the word (never colour alone)
            Image chip = UiKit.AddPanel(panel, "Fill", RarityUi.Fill(rarity));
            UiKit.Place(chip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -940f), new Vector2(400f, 90f));
            Text r = UiKit.OnPanel(UiKit.AddText(chip.transform, "Rarity", rarityName, 44, UiKit.PanelInk));
            UiKit.Stretch(r.rectTransform);
        }

        /// <summary>
        /// The card on hero_reveal_bg: title on the gold ribbon (dark ink on gold), hero picture on the purple centre glow, name on a dark pill (white text, high contrast on any glow),
        /// rarity chip below it. Base sizes are the 36 px body minimum or a little more; the text scale (up to 130 percent) is applied by AddText, and the boxes are sized for that worst case.
        /// </summary>
        static void BuildOnPicture(RectTransform panel, CharacterDef who, ArtCatalog art, CharacterRarity rarity, string rarityName)
        {
            float cardH = panel.sizeDelta.y;

            Text title = UiKit.OnPanel(UiKit.AddText(panel, "Title", Title, 38, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
            UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -(RibbonCenterY * cardH - RibbonTextH * 0.5f)), new Vector2(RibbonTextW, RibbonTextH));

            RectTransform doll = MomoDoll.Build(panel, "Hero", PicDollH, who, art);
            UiKit.Place(doll, new Vector2(0.5f, 1f), new Vector2(0f, -PicDollTop), doll.sizeDelta);

            // name on a dark pill: readable on the bright glow in the middle (CUX-5)
            // box heights follow the REAL rendered text size (font size x text scale x 1.3 line height), so nothing overlaps at 130 percent
            const int nameFont = 44, rarityFont = 36;
            float nameH = Mathf.Ceil(Mathf.Max(nameFont, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.3f);
            float chipH = Mathf.Ceil(Mathf.Max(rarityFont, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.3f);
            Image pill = UiKit.AddPanel(panel, "NamePill", PillInk);
            UiKit.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -PicNameTop), new Vector2(340f, nameH));
            Text name = UiKit.AddText(pill.transform, "HeroName", who.Name, nameFont, Color.white, TextAnchor.MiddleCenter, true);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(name.rectTransform);

            // rarity chip: the Common colour of the Heroes screen, with the word (never colour alone)
            Image chip = UiKit.AddPanel(panel, "Fill", RarityUi.Fill(rarity));
            UiKit.Place(chip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(PicNameTop + nameH + PicGap)), new Vector2(300f, chipH));
            Text r = UiKit.OnPanel(UiKit.AddText(chip.transform, "Rarity", rarityName, rarityFont, UiKit.PanelInk));
            r.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(r.rectTransform);
        }
    }

    /// <summary>
    /// The character screen (docs/21): the chosen character on the green land stage on top, and below it a scrolling list of characters in rarity boxes,
    /// five to a row, four rows in the first view. Only owned characters are shown (a tap only selects; the (i) button opens the stat sheet). Capi appears once region 2 is unlocked. No words.
    /// </summary>
    public sealed class CharactersScreen
    {
        const int Columns = 5, VisibleRows = 4;
        /// <summary>
        /// Roster: 5 cells per row and 4 rows fill the list area end to end (owner 2026-11-02, 20 slots); more heroes scroll. The cell is as wide as the safe width
        /// allows (<see cref="EdgeMargin"/> at both edges) and as high as lets 4 rows fit; the hero picture is 90 percent of the smaller side.
        /// </summary>
        const float Gap = 10f, TopPad = 16f, EdgeMargin = 16f;
        static float MinTile { get { return UiKit.MinTouchRef; } }   // 48 dp: a tile is a touch target (CUX-4); when 4 rows do not fit, the list scrolls
        /// <summary>Margin between the roster / the (i) button and the screen (safe area) edge; at least 16.</summary>
        public const float SideMargin = 28f;
        /// <summary>Character height on the stage.</summary>
        public const float DollH = 420f;
        /// <summary>The (i) button right of the hero on the stage (at least 48 dp).</summary>
        public const float InfoSize = 132f;

        /// <summary>What the screen needs from the menu: the land stage it stands on.</summary>
        public sealed class Host
        {
            public Stage Stage;
            /// <summary>The (i) button opens the shown hero's sheet (docs/21); the sheet has the "Use this hero" button. Tapping a tile only selects it.</summary>
            public Action<CharacterDef> OpenHero;
        }

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly Host host;
        RectTransform body;

        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }

        public CharactersScreen(RectTransform screen, ProgressService progress, ArtCatalog art, Host host)
        {
            this.screen = screen;
            this.progress = progress;
            this.art = art;
            this.host = host;
        }

        public void Build()
        {
            if (body != null) UnityEngine.Object.Destroy(body.gameObject);
            ClearStage();
            body = UiKit.Rect(screen, "CharactersBody");
            UiKit.Stretch(body);

            // the dark panel under the stage holds the list
            // It is a child of the full-screen stage root (not of the safe-area body), so it starts exactly where the grass ends, whatever the notch:
            // no dark band between the stage and the roster, and it reaches the bottom edge.
            Image panel = UiKit.AddImage(host.Stage.Root, "ListPanel", null, new Color(0.17f, 0.23f, 0.42f, 1f));
            panel.preserveAspect = false;
            panel.raycastTarget = false;
            panel.rectTransform.anchorMin = Vector2.zero;
            panel.rectTransform.anchorMax = Vector2.one;
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = new Vector2(0f, -host.Stage.GroundBottom);

            RectTransform row = UiKit.Rect(body, "Wallet");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(190f, -118f);
            row.offsetMax = new Vector2(-16f, -18f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            CoinText = ToyKit.WalletPillFlex(row, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, 100f);
            GemText = ToyKit.WalletPillFlex(row, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), 100f);
            UiKit.SetText(CoinText, progress.Coins.ToString());
            UiKit.SetText(GemText, progress.Gems.ToString());

            BuildStage();
            BuildList();
        }

        /// <summary>The doll lives on the stage (not in the screen body), so it is removed when the screen is rebuilt.</summary>
        void ClearStage()
        {
            if (host.Stage == null || host.Stage.Root == null) return;
            for (int i = host.Stage.Root.childCount - 1; i >= 0; i--)
            {
                string n = host.Stage.Root.GetChild(i).name;
                if (n == "Doll" || n == "ListPanel") UnityEngine.Object.Destroy(host.Stage.Root.GetChild(i).gameObject);
            }
        }

        /// <summary>The chosen character on the dirt oval.</summary>
        void BuildStage()
        {
            RectTransform doll = MomoDoll.Build(host.Stage.Root, "Doll", Mathf.Round(DollH * host.Stage.Scale), CharacterCatalog.Selected, art);
            host.Stage.Stand(doll);

            // (i) opens the shown hero's sheet (docs/21). It sits in the bottom right corner of the stage, clear of the hero and the dirt oval
            // (the oval is 600 px wide centred, the button is in the outer 24 to 156 px of the screen edge), never in the middle of the stage.
            if (host.OpenHero != null)
            {
                Button info = UiKit.AddButton(body, "HeroInfo", "i", ToyKit.Button(2), Color.white, new Vector2(InfoSize, InfoSize), () => host.OpenHero(CharacterCatalog.Selected), 64);
                info.GetComponent<A11yLabel>().Text = "Hero details";
                // body top = screen top + safe inset, so the stage coordinates are shifted by the inset
                float y = -(host.Stage.GroundBottom - UiKit.TopInsetRef() - InfoSize - 28f);
                UiKit.Place((RectTransform)info.transform, new Vector2(1f, 1f), new Vector2(-SideMargin, y), new Vector2(InfoSize, InfoSize));
            }
        }

        void BuildList()
        {
            RectTransform vp = UiKit.Rect(body, "ListViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, ToyKit.TabBarReserve + 8f);
            vp.offsetMax = new Vector2(0f, -(host.Stage.GroundBottom - UiKit.TopInsetRef() + 18f));   // body top is the safe-area top; the stage counts from the screen top
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            CharacterDef[] owned = CharacterCatalog.Available(progress);
            int rows = Mathf.Max(1, (owned.Length + Columns - 1) / Columns);
            // End to end: 5 cells fill the whole width and 4 rows fill the list height (owner 2026-11-02); more heroes scroll. Everything is anchored by
            // fractions of the list viewport (no pixel maths), so it fits every aspect and safe area by construction.
            int layoutRows = Mathf.Max(VisibleRows, rows);
            RectTransform content = UiKit.Rect(vp, "ListContent");
            content.anchorMin = new Vector2(0f, 1f - layoutRows / (float)VisibleRows);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(EdgeMargin, 0f);
            content.offsetMax = new Vector2(-EdgeMargin, 0f);
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = vp;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 60f;

            // only the characters the player has are shown (no locked or placeholder slots, no lock icons)
            CharacterDef chosen = CharacterCatalog.Selected;
            for (int i = 0; i < owned.Length; i++)
            {
                CharacterDef who = owned[i];
                CharacterRarity rarity = who.Rarity;
                RectTransform inner = RarityUi.Box(content, "Char_" + who.Id, rarity, false, new Vector2(100f, 100f));
                RectTransform outer = (RectTransform)inner.parent;
                int col = i % Columns, row = i / Columns;
                outer.anchorMin = new Vector2(col / (float)Columns, 1f - (row + 1) / (float)layoutRows);
                outer.anchorMax = new Vector2((col + 1) / (float)Columns, 1f - row / (float)layoutRows);
                outer.pivot = new Vector2(0.5f, 0.5f);
                outer.offsetMin = new Vector2(Gap * 0.5f, Gap * 0.5f);
                outer.offsetMax = new Vector2(-Gap * 0.5f, -Gap * 0.5f);

                RectTransform face = MomoDoll.Build(inner, "Face", 100f, who, art);
                face.anchorMin = new Vector2(0.04f, 0.04f);
                face.anchorMax = new Vector2(0.96f, 0.96f);
                face.offsetMin = face.offsetMax = Vector2.zero;
                var fit = face.gameObject.AddComponent<AspectRatioFitter>();   // the hero keeps its proportions inside the cell
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = who.Rig.CanvasW / (float)who.Rig.CanvasH;
                if (who == chosen)
                {
                    Image tick = UiKit.AddImage(outer, "Chosen", Glyphs.Check, Color.white);
                    tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(1f, 1f);
                    tick.rectTransform.pivot = new Vector2(1f, 1f);
                    tick.rectTransform.anchoredPosition = new Vector2(-2f, -2f);
                    tick.rectTransform.sizeDelta = new Vector2(48f, 48f);
                }

                var b = outer.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                outer.GetComponent<Image>().raycastTarget = true;
                CharacterDef pick = who;
                // a tap only selects the hero (the (i) button on the stage opens its stat sheet)
                b.onClick.AddListener(() => { CharacterCatalog.Selected = pick; Build(); });
                outer.gameObject.AddComponent<A11yLabel>().Text = who.Name + ", " + CharacterDefs.RarityNames[(int)rarity] + (who == chosen ? ", chosen" : "");
            }
        }

        /// <summary>Picks a character as the tests do (the same as a tap on its tile).</summary>
        public void Choose(CharacterDef who)
        {
            if (!CharacterCatalog.Owns(progress, who)) return;
            CharacterCatalog.Selected = who;
            Build();
        }
    }
}
