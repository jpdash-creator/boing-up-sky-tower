using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The sprites of the Merge screen drawn in code (docs/34). Each one has a REPLACEABLE SLOT: drop a picture at Resources/Art/Ui/MergeBackground or
    /// Resources/Art/Ui/MergeIcon (made later with OpenArt) and it is used instead. All generated sprites are named glyph_merge_* so the layout audit treats them as decoration.
    /// </summary>
    public static class MergeArt
    {
        public const string BackgroundResource = "Art/Ui/MergeBackground";
        public const string IconResource = "Art/Ui/MergeIcon";

        static Sprite gradient, glow, icon;

        /// <summary>Vertical gradient, dark navy at the bottom to deep purple at the top.</summary>
        public static Sprite BackgroundGradient
        {
            get
            {
                if (gradient != null) return gradient;
                const int h = 128;
                var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                Color bottom = new Color(0.03f, 0.05f, 0.16f, 1f), top = new Color(0.17f, 0.09f, 0.34f, 1f);
                for (int y = 0; y < h; y++)
                {
                    Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y / (h - 1f)));
                    tex.SetPixel(0, y, c);
                    tex.SetPixel(1, y, c);
                }
                tex.Apply();
                gradient = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
                gradient.name = "glyph_merge_bg";
                return gradient;
            }
        }

        /// <summary>White radial glow (opaque centre, soft edge): tint it.</summary>
        public static Sprite Glow
        {
            get
            {
                if (glow != null) return glow;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3f - 2f * a);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                glow.name = "glyph_merge_glow";
                return glow;
            }
        }

        /// <summary>The Merge icon: the painted one when Resources/Art/Ui/MergeIcon exists, else a gold four-point sparkle in a ring.</summary>
        public static Sprite Icon
        {
            get
            {
                if (icon != null) return icon;
                Sprite custom = Resources.Load<Sprite>(IconResource);
                if (custom != null) return icon = custom;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float ring = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.9f) / 0.06f);                       // thin ring
                        float star = Mathf.Pow(Mathf.Abs(dx), 0.6667f) + Mathf.Pow(Mathf.Abs(dy), 0.6667f);   // astroid: four-point star
                        float s = 1f - Mathf.Clamp01((star - 0.62f) / 0.06f);
                        float a = Mathf.Max(ring, s);
                        tex.SetPixel(x, y, new Color(1f, 0.82f, 0.28f, a));
                    }
                tex.Apply();
                icon = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                icon.name = "glyph_merge_icon";
                return icon;
            }
        }

        /// <summary>The Merge look (painted MergeBackground or the purple gradient with glows), shared by every page of the Heroes screen (Bug List 6 item 3a). Returns the full-screen background, already the first sibling of <paramref name="root"/>.</summary>
        public static RectTransform AddBackground(RectTransform root, string name)
        {
            RectTransform bg = UiKit.Rect(root, name);
            UiKit.Stretch(bg);
            bg.SetAsFirstSibling();
            Sprite custom = Resources.Load<Sprite>(MergeArt.BackgroundResource);
            Image baseImg = UiKit.AddImage(bg, "Bg", custom ?? MergeArt.BackgroundGradient, Color.white);
            baseImg.preserveAspect = false;
            UiKit.Stretch(baseImg.rectTransform);
            if (custom != null)
            {
                var fit = baseImg.gameObject.AddComponent<AspectRatioFitter>();   // cover-fit like every painted background
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = custom.rect.width / custom.rect.height;
                return bg;
            }

            // the glowing circle behind the result slot, a faint outer ring, and still sparkles; nothing moves or flashes (CUX-6)
            Image halo = UiKit.AddImage(bg, "Bg", MergeArt.Glow, new Color(0.56f, 0.30f, 1f, 0.55f));
            UiKit.Place(halo.rectTransform, new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(1300f, 1300f));
            halo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image halo2 = UiKit.AddImage(bg, "Bg", MergeArt.Glow, new Color(0.25f, 0.55f, 1f, 0.28f));
            UiKit.Place(halo2.rectTransform, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(1100f, 1100f));
            halo2.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image ring = UiKit.AddImage(bg, "Bg", Glyphs.Ring, new Color(0.80f, 0.62f, 1f, 0.22f));
            UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(1000f, 1000f));
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            uint seed = 12345u;
            for (int i = 0; i < 22; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float fx = (seed >> 8 & 0xFFFF) / 65535f;
                seed = seed * 1664525u + 1013904223u;
                float fy = (seed >> 8 & 0xFFFF) / 65535f;
                float size = 14f + (i % 4) * 7f;
                Image dot = UiKit.AddImage(bg, "Bg", MergeArt.Glow, new Color(1f, 0.92f, 0.70f, 0.55f));
                UiKit.Place(dot.rectTransform, new Vector2(fx, fy), Vector2.zero, new Vector2(size, size));
                dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            return bg;
        }
    }

    /// <summary>
    /// Character merge (docs/34 section 10): the big result slot on top (the next tier of the first selected hero with its rarity change and stat gains), three input slots that fill with the
    /// selected cards, a green arrow, the gold MERGE button (enabled only for 3 selected cards of one hero type), and under it the list of heroes that CAN merge: one card per owned copy of
    /// every hero type with 3 or more copies, no numbers. A tap selects a card (green tick).
    /// Everything is built once per change (no Update, no per-frame work). Layout is anchored by fractions and computed from the real text sizes, so it holds at 130 percent text.
    /// </summary>
    public sealed class MergeScreen
    {
        public const string ScreenName = "Merge";
        const float Margin = 28f, TileH = 232f, TileGap = 10f, SlotH = 190f, ArrowH = 64f, Pad = 18f;
        const int Columns = 5, NameFont = 46, BodyFont = 40, GainLines = 6, RarityLines = 2;
        static readonly Color Gold = new Color(1f, 0.82f, 0.24f, 1f);
        static readonly Color ArrowGreen = new Color(0.36f, 0.95f, 0.52f, 1f);

        /// <summary>What the screen needs from the menu.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            /// <summary>Back returns to the Heroes screen.</summary>
            public Action Back;
            /// <summary>The other two tabs of the top row (Bug List 6 item 3a): All Heroes and Powers. A tab without an action is inert.</summary>
            public Action OpenAllHeroes, OpenPowers;
        }

        readonly RectTransform screen, root;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly Host host;
        RectTransform body, bg, content;
        // Adaptive layout (Bug List 6 item 3a): the 3-tab row made the top block taller, so on a short safe area (3:4 with a notch at 130 percent text) the pieces are compacted in a fixed order
        // until the button and the hint fit with room for the grid. See PlanLayout. A Sizer rebuilds the body when the safe area changes size (aspect or text size).
        Sizer sizer;
        float slotH = SlotH, headerBs = 140f;
        bool compactTabs, showArrow = true;
        int gainLinesUsed = GainLines;
        const float MinGridReserve = 150f, CompactSlotH = 130f;
        /// <summary>The cards of the Merge list: every copy of every hero type that can merge (3 or more copies and a next tier).</summary>
        List<HeroCard> cards = new List<HeroCard>();
        /// <summary>The selected cards, in the order they were tapped (the three slots show them).</summary>
        readonly List<HeroCard> picked = new List<HeroCard>(3);
        float scroll;

        /// <summary>The cards of the list under the slots (one per copy of every mergeable hero type, no counts).</summary>
        public IList<HeroCard> Cards { get { return cards; } }
        /// <summary>The selected cards (0 to 3).</summary>
        public IList<HeroCard> Picked { get { return picked; } }
        /// <summary>The MERGE button (interactable only when exactly 3 cards of one hero type are selected). Tests tap it through <see cref="TapMerge"/>.</summary>
        public Button MergeButton { get; private set; }
        /// <summary>The character the result slot previews (next tier of the first selected card), or null.</summary>
        public CharacterDef Preview { get; private set; }
        /// <summary>True when Merge is enabled: exactly 3 selected cards of the same hero type.</summary>
        public bool CanMerge { get { return HeroCards.IsMergeSet(picked); } }

        public MergeScreen(RectTransform screen, RectTransform screenRoot, ProgressService progress, ArtCatalog art, Host host)
        {
            this.screen = screen;
            this.root = screenRoot;
            this.progress = progress;
            this.art = art;
            this.host = host;
        }

        static float Line(int font) { return Mathf.Ceil(Mathf.Max(font, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.3f); }

        /// <summary>Lives under the safe-area rect for the life of the screen; when its height changes it rebuilds the Merge body once (next LateUpdate). No per-frame work besides one flag test.</summary>
        sealed class Sizer : UnityEngine.EventSystems.UIBehaviour
        {
            public Action Rebuild;
            public float BuiltFor;
            bool dirty;
            public float Height { get { return ((RectTransform)transform).rect.height; } }

            protected override void OnRectTransformDimensionsChange()
            {
                if (Mathf.Abs(Height - BuiltFor) > 1f) dirty = true;
            }

            void LateUpdate()
            {
                if (!dirty) return;
                dirty = false;
                if (Rebuild != null) Rebuild();
            }
        }

        float Need()
        {
            float lName = Line(NameFont), lBody = Line(BodyFont);
            float resultH = Pad + 2f * lName + 6f + RarityLines * lBody + 6f + gainLinesUsed * lBody + Pad + 8f;
            return 16f + headerBs + 8f + HeroTabs.RowHFor(compactTabs) + 8f + resultH + (showArrow ? ArrowH + 8f : 0f) + slotH + 14f + 140f + 6f + lBody + 12f + 12f;
        }

        /// <summary>
        /// Chooses the layout variant for the safe-area height. Order: the gain rows shrink to the real number of lines, the slots get shorter, the tab row goes compact, the header button
        /// is trimmed to the touch minimum, and last the green arrow goes. Each step is skipped when the grid already has at least <see cref="MinGridReserve"/> left.
        /// </summary>
        void PlanLayout(int gainCount)
        {
            slotH = SlotH;
            headerBs = Mathf.Max(140f, UiKit.MinTouchRef);
            compactTabs = false;
            showArrow = true;
            gainLinesUsed = GainLines;
            float avail = sizer != null ? sizer.Height : 0f;
            if (avail <= 1f || Need() + MinGridReserve <= avail) return;
            gainLinesUsed = Mathf.Clamp(gainCount, 1, GainLines);
            if (Need() + MinGridReserve <= avail) return;
            slotH = Mathf.Max(CompactSlotH, UiKit.MinTouchRef);
            if (Need() + MinGridReserve <= avail) return;
            compactTabs = true;
            if (Need() + MinGridReserve <= avail) return;
            headerBs = Mathf.Max(UiKit.MinTouchRef, 126f);
            if (Need() + MinGridReserve <= avail) return;
            showArrow = false;
        }

        // ------------------------------------------------------------ build

        /// <summary>Builds (or rebuilds) the screen. <paramref name="preselectId"/> (tests and shortcuts) selects the first three copies of that hero type; null keeps the current selection.</summary>
        public void Build(string preselectId = null)
        {
            if (preselectId != null) PreselectType(preselectId);
            BuildNow();
        }

        void PreselectType(string id)
        {
            cards = CharacterCatalog.MergeCards(progress);
            picked.Clear();
            foreach (HeroCard c in cards)
                if (c.Id == id && picked.Count < MergeRules.CopiesNeeded) picked.Add(c);
        }

        void BuildNow()
        {
            if (body != null)
            {
                body.gameObject.SetActive(false);   // never shows or counts as a stale copy while the destroy is deferred
                if (Application.isPlaying) UnityEngine.Object.Destroy(body.gameObject);
                else UnityEngine.Object.DestroyImmediate(body.gameObject);   // edit mode (tests): Destroy is not allowed and would leave the old tree
                body = null;
            }
            if (bg == null) BuildBackground();
            if (sizer == null)
            {
                RectTransform sr = UiKit.Rect(screen, "MergeSizer");
                UiKit.Stretch(sr);
                sizer = sr.gameObject.AddComponent<Sizer>();
                sizer.Rebuild = BuildNow;
            }
            sizer.BuiltFor = sizer.Height;

            cards = CharacterCatalog.MergeCards(progress);
            List<HeroCard> kept = HeroCards.Prune(picked, cards);   // copies that were merged away are gone from the selection
            picked.Clear();
            picked.AddRange(kept);

            body = UiKit.Rect(screen, "MergeBody");
            UiKit.Stretch(body);

            CharacterSpec spec = picked.Count > 0 ? picked[0].Spec : null;
            CharacterSpec next = spec != null ? MergeRules.NextTier(spec) : null;
            Preview = next != null ? CharacterCatalog.Get(next.Id) : null;
            PlanLayout(next != null ? MergeRules.ShownGainLines(spec.ShownValues(), next.ShownValues()).Count : (spec != null ? 1 : 0));
            float y = BuildTopBar();

            y = BuildResultCard(y, spec, next);
            y = BuildSlots(y);
            y = BuildMergeButton(y);
            BuildGrid(y);
        }

        // ------------------------------------------------------------ background (slot: MergeBackground)

        void BuildBackground()
        {
            bg = MergeArt.AddBackground(root, "MergeBackground");
        }

        // ------------------------------------------------------------ top bar

        float BuildTopBar()
        {
            float bs = headerBs;
            Button back = ToyKit.GlyphButton(body, "Back", "Back", 2, Glyphs.Left, new Vector2(bs, bs), () => { if (host != null && host.Back != null) host.Back(); });
            UiKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(bs, bs));

            Text title = UiKit.AddText(body, "Title", "MERGE", 72, Color.white, TextAnchor.MiddleCenter, true);
            RectTransform tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.offsetMin = new Vector2(bs + 40f, -(16f + bs));
            tr.offsetMax = new Vector2(-(bs + 40f), -16f);

            Image icon = UiKit.AddImage(body, "MergeIcon", MergeArt.Icon, Color.white);
            float isz = Mathf.Min(bs, 112f);
            UiKit.Place(icon.rectTransform, new Vector2(1f, 1f), new Vector2(-24f - (bs - isz) * 0.5f, -16f - (bs - isz) * 0.5f), new Vector2(isz, isz));
            float top = 16f + bs + 8f;
            HeroTabs.Build(body, top, HeroTabs.Merge, host != null ? host.OpenAllHeroes : null, null, host != null ? host.OpenPowers : null, compactTabs, HeroTabs.Counts(progress));
            return top + HeroTabs.RowHFor(compactTabs) + 8f;
        }

        // ------------------------------------------------------------ result slot

        /// <summary>A row anchored to the top edge of <paramref name="parent"/>, full width minus the insets.</summary>
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

        float BuildResultCard(float y, CharacterSpec spec, CharacterSpec next)
        {
            float lName = Line(NameFont), lBody = Line(BodyFont);
            float cardH = Pad + 2f * lName + 6f + RarityLines * lBody + 6f + gainLinesUsed * lBody + Pad;   // fixed: 2 name lines, 2 rarity lines (may wrap), up to 6 gain lines, so the screen never jumps
            RectTransform card = TopRow(body, "ResultCard", y, cardH, Margin);
            Image panel = UiKit.AddImage(card, "Panel", Theme.RoundSprite, new Color(0.10f, 0.08f, 0.26f, 0.88f));
            panel.type = Image.Type.Sliced;
            panel.preserveAspect = false;
            panel.pixelsPerUnitMultiplier = 1.4f;
            UiKit.Stretch(panel.rectTransform);
            Image rim = UiKit.AddImage(card, "Rim", Theme.RingSprite, new Color(Gold.r, Gold.g, Gold.b, 0.85f));
            rim.type = Image.Type.Sliced;
            rim.preserveAspect = false;
            rim.pixelsPerUnitMultiplier = 1.4f;
            UiKit.Stretch(rim.rectTransform);

            // left: the glowing circle with the next-tier hero (the placeholder tint shows while its art is not made)
            RectTransform column = UiKit.Rect(card, "ResultColumn");
            column.anchorMin = new Vector2(0.02f, 0f);
            column.anchorMax = new Vector2(0.32f, 1f);
            column.offsetMin = new Vector2(0f, Pad * 0.5f);
            column.offsetMax = new Vector2(0f, -Pad * 0.5f);
            RectTransform circle = UiKit.Rect(column, "Circle");
            UiKit.Stretch(circle);
            var cf = circle.gameObject.AddComponent<AspectRatioFitter>();
            cf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            cf.aspectRatio = 1f;
            CharacterSpec shown = next ?? spec;
            Color glowColor = shown != null ? RarityUi.Fill(shown.Rarity) : new Color(0.7f, 0.5f, 1f);
            Image glowImg = UiKit.AddImage(circle, "Glow", MergeArt.Glow, new Color(glowColor.r, glowColor.g, glowColor.b, 0.85f));
            UiKit.Stretch(glowImg.rectTransform);
            Image ringImg = UiKit.AddImage(circle, "Ring", Glyphs.Ring, Gold);
            UiKit.Stretch(ringImg.rectTransform);
            if (shown != null)
            {
                CharacterDef def = CharacterCatalog.Get(shown.Id);
                RectTransform area = UiKit.Rect(circle, "FaceArea");
                area.anchorMin = new Vector2(0.12f, 0.10f);
                area.anchorMax = new Vector2(0.88f, 0.90f);
                area.offsetMin = area.offsetMax = Vector2.zero;
                RectTransform face = MomoDoll.Build(area, "Face", 100f, def, art);
                UiKit.Stretch(face);
                var fit = face.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = def.Rig.CanvasW / def.Rig.CanvasH;
                if (next == null)
                    foreach (Image im in face.GetComponentsInChildren<Image>()) im.color = new Color(im.color.r, im.color.g, im.color.b, 0.55f);   // top tier / nothing to merge: shown dimmed
            }

            // right: name, "Rarity A > Rarity B", the stat gains
            RectTransform info = UiKit.Rect(card, "ResultInfo");
            info.anchorMin = new Vector2(0.35f, 0f);
            info.anchorMax = new Vector2(0.98f, 1f);
            info.offsetMin = info.offsetMax = Vector2.zero;
            string nameText, rarityText, gains;
            if (spec == null && cards.Count == 0) { nameText = "Nothing to merge"; rarityText = "Collect 3 of the same hero"; gains = ""; }
            else if (spec == null) { nameText = "Pick 3 of a hero"; rarityText = "Tap heroes below"; gains = ""; }
            else if (next == null) { nameText = spec.Name; rarityText = "Top tier"; gains = "Cannot merge further"; }
            else
            {
                nameText = next.Name;
                rarityText = CharacterDefs.RarityNames[(int)spec.Rarity] + " > " + CharacterDefs.RarityNames[(int)next.Rarity];
                gains = string.Join("\n", MergeRules.ShownGainLines(spec.ShownValues(), next.ShownValues()).ToArray());
            }
            float cy = Pad;
            Text nm = UiKit.AddText(TopRow(info, "NameRow", cy, 2f * lName, 0f), "ResultName", nameText, NameFont, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Stretch(nm.rectTransform);
            cy += 2f * lName + 6f;
            Text rt = UiKit.AddText(TopRow(info, "RarityRow", cy, RarityLines * lBody, 0f), "ResultRarity", rarityText, BodyFont, Gold, TextAnchor.UpperLeft);
            UiKit.Stretch(rt.rectTransform);
            cy += RarityLines * lBody + 6f;
            Text gn = UiKit.AddText(TopRow(info, "GainsRow", cy, gainLinesUsed * lBody, 0f), "ResultGains", gains, BodyFont, new Color(0.62f, 1f, 0.72f, 1f), TextAnchor.UpperLeft);
            UiKit.Stretch(gn.rectTransform);
            gn.lineSpacing = 1f;
            card.gameObject.AddComponent<A11yLabel>().Text = "Result: " + nameText + ", " + rarityText + (gains.Length > 0 ? ", " + gains.Replace("\n", ", ") : "");
            return y + cardH + 8f;
        }

        // ------------------------------------------------------------ input slots

        /// <summary>The three slots fill with the selected cards in tap order; a tap on a filled slot removes that card. Empty slots are dim.</summary>
        float BuildSlots(float y)
        {
            // green arrow pointing from the slots up to the result
            if (showArrow)
            {
                Image arrow = UiKit.AddImage(body, "Arrow", Glyphs.Up, ArrowGreen);
                UiKit.Place(arrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(ArrowH, ArrowH));
                y += ArrowH + 8f;
            }

            RectTransform row = TopRow(body, "Slots", y, slotH, Margin);
            for (int i = 0; i < MergeRules.CopiesNeeded; i++)
            {
                bool filled = i < picked.Count;
                HeroCard card = filled ? picked[i] : null;
                CharacterRarity rarity = filled ? card.Spec.Rarity : CharacterRarity.Common;
                RectTransform inner = RarityUi.Box(row, "Slot_" + (i + 1), rarity, !filled, new Vector2(100f, 100f));
                RectTransform outer = (RectTransform)inner.parent;
                float x0 = 0.03f + i * 0.33f;
                outer.anchorMin = new Vector2(x0, 0f);
                outer.anchorMax = new Vector2(x0 + 0.28f, 1f);
                outer.offsetMin = outer.offsetMax = Vector2.zero;
                outer.pivot = new Vector2(0.5f, 0.5f);
                if (filled)
                {
                    CharacterDef who = CharacterCatalog.Get(card.Id);
                    RectTransform area = UiKit.Rect(inner, "FaceArea");
                    area.anchorMin = new Vector2(0.06f, 0.05f);
                    area.anchorMax = new Vector2(0.94f, 0.95f);
                    area.offsetMin = area.offsetMax = Vector2.zero;
                    RectTransform face = MomoDoll.Build(area, "Face", 100f, who, art);
                    UiKit.Stretch(face);
                    var fit = face.gameObject.AddComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fit.aspectRatio = who.Rig.CanvasW / who.Rig.CanvasH;
                    var b = outer.gameObject.AddComponent<Button>();   // tap = take the card out of the slot
                    b.transition = Selectable.Transition.None;
                    outer.GetComponent<Image>().raycastTarget = true;
                    HeroCard pick = card;
                    b.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); Toggle(pick); });
                }
                outer.gameObject.AddComponent<A11yLabel>().Text = "Merge slot " + (i + 1) + (filled ? ", " + CharacterCatalog.Get(card.Id).Name + ", tap to remove" : ", empty");
            }
            return y + slotH + 14f;
        }

        // ------------------------------------------------------------ MERGE button

        /// <summary>The hint under the button, in words and without numbers of copies.</summary>
        public string HintText()
        {
            if (cards.Count == 0) return "No hero to merge yet";
            if (picked.Count == 0) return "Pick 3 of the same hero";
            if (HeroCards.IsMergeSet(picked)) return "Ready to merge";
            if (HeroCards.IsMixed(picked)) return picked.Count >= MergeRules.CopiesNeeded ? "Wrong mix: remove a card" : "Wrong mix: pick the same hero";
            return "Pick more of the same hero";
        }

        float BuildMergeButton(float y)
        {
            bool can = HeroCards.IsMergeSet(picked);
            const float bh = 140f;
            Color tint = can ? Color.white : new Color(0.62f, 0.62f, 0.68f, 1f);
            MergeButton = UiKit.AddButton(body, "MergeButton", "MERGE", ToyKit.Button(0), tint, new Vector2(560f, bh), OnMergeTapped, 64);
            MergeButton.interactable = can;
            UiKit.Place((RectTransform)MergeButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(560f, bh));
            string hint = HintText();
            MergeButton.GetComponent<A11yLabel>().Text = "Merge. " + hint;
            y += bh + 6f;
            float lh = Line(BodyFont);
            Text t = UiKit.AddText(TopRow(body, "HintRow", y, lh, Margin), "Hint", hint, BodyFont, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(t.rectTransform);
            return y + lh + 12f;
        }

        void OnMergeTapped()
        {
            if (!HeroCards.IsMergeSet(picked)) return;
            string id = picked[0].Id;
            MergeOutcome o = progress.TryMerge(id);   // consumes exactly 3 copies of that hero and adds 1 of the next tier (the save keeps counts)
            picked.Clear();
            if (o.Result != MergeResult.Ok) { BuildNow(); return; }
            AudioManager.Play(Sfx.Win);
            Haptics.Pulse();
            BuildNow();   // the list is rebuilt from the save: the three consumed copies are gone, a type with fewer than 3 left vanishes
            ShowResult(o);
        }

        /// <summary>Taps MERGE as a player would (tests). Returns the outcome; NotEnoughCopies when the button is not usable.</summary>
        public MergeOutcome TapMerge()
        {
            string from = picked.Count > 0 ? picked[0].Id : null;
            if (MergeButton == null || !MergeButton.interactable) return new MergeOutcome { Result = MergeResult.NotEnoughCopies, FromId = from };
            string to = MergeRules.NextTier(picked[0].Spec).Id;
            OnMergeTapped();
            return new MergeOutcome { Result = MergeResult.Ok, FromId = from, ToId = to };
        }

        // ------------------------------------------------------------ hero grid

        /// <summary>Card tile name: "Tile_{id}" for the first copy, "Tile_{id}_{n}" for the n-th.</summary>
        public static string TileName(HeroCard c) { return c.Copy == 0 ? "Tile_" + c.Id : "Tile_" + c.Id + "_" + (c.Copy + 1); }

        void BuildGrid(float y)
        {
            RectTransform vp = UiKit.Rect(body, "GridViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(Margin, 12f);
            vp.offsetMax = new Vector2(-Margin, -y);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            int rows = Mathf.Max(1, (cards.Count + Columns - 1) / Columns);
            content = UiKit.Rect(vp, "GridContent");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, rows * TileH);
            content.anchoredPosition = Vector2.zero;
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = vp;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 60f;

            if (cards.Count == 0)
            {
                float lh = Line(BodyFont);
                Text none = UiKit.AddText(TopRow(content, "EmptyNote", 12f, 2f * lh, 12f), "Empty", "Hold 3 of the same hero to merge it", BodyFont, Color.white, TextAnchor.UpperCenter);
                UiKit.Stretch(none.rectTransform);
                content.sizeDelta = new Vector2(0f, 2f * lh + 24f);
            }

            for (int i = 0; i < cards.Count; i++)
            {
                HeroCard card = cards[i];
                CharacterDef who = CharacterCatalog.Get(card.Id);
                CharacterSpec s = card.Spec;
                int col = i % Columns, row = i / Columns;
                bool isPicked = HeroCards.IndexOf(picked, card) >= 0;
                RectTransform inner = RarityUi.Box(content, TileName(card), s.Rarity, false, new Vector2(100f, TileH));
                RectTransform outer = (RectTransform)inner.parent;
                outer.anchorMin = new Vector2(col / (float)Columns, 1f);
                outer.anchorMax = new Vector2((col + 1) / (float)Columns, 1f);
                outer.pivot = new Vector2(0.5f, 1f);
                outer.sizeDelta = new Vector2(-TileGap, TileH - TileGap);
                outer.anchoredPosition = new Vector2(0f, -(row * TileH + TileGap * 0.5f));

                RectTransform area = UiKit.Rect(inner, "FaceArea");
                area.anchorMin = new Vector2(0.04f, 0f);
                area.anchorMax = new Vector2(0.96f, 1f);
                area.offsetMin = new Vector2(0f, 6f);
                area.offsetMax = new Vector2(0f, -6f);
                RectTransform face = MomoDoll.Build(area, "Face", 100f, who, art);
                UiKit.Stretch(face);
                var fit = face.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = who.Rig.CanvasW / who.Rig.CanvasH;

                if (isPicked)
                {
                    // a selected card: gold ring AND the green tick (SS19 style), so it never relies on colour alone
                    Image ring = UiKit.AddImage(outer, "PickedRing", Theme.RingSprite, Gold);
                    ring.type = Image.Type.Sliced;
                    ring.preserveAspect = false;
                    ring.pixelsPerUnitMultiplier = 1.0f;
                    ring.rectTransform.anchorMin = Vector2.zero;
                    ring.rectTransform.anchorMax = Vector2.one;
                    ring.rectTransform.offsetMin = new Vector2(-3f, -3f);
                    ring.rectTransform.offsetMax = new Vector2(3f, 3f);
                    CharactersScreen.AddTick(outer, "Picked", 64f);
                }

                var b = outer.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                outer.GetComponent<Image>().raycastTarget = true;
                HeroCard pick = card;
                b.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); Toggle(pick); });
                outer.gameObject.AddComponent<A11yLabel>().Text = who.Name + ", " + CharacterDefs.RarityNames[(int)s.Rarity] + (isPicked ? ", selected" : "");
            }
            if (sizer != null && sizer.Height > 1f && sizer.Height - y - 12f < UiKit.MinTouchPx) vp.gameObject.SetActive(false);   // no room left at all: never a negative-size area
            if (scroll > 0f) content.anchoredPosition = new Vector2(0f, scroll);
            sr.onValueChanged.AddListener(_ => scroll = content.anchoredPosition.y);
        }

        /// <summary>Selects or deselects a card (a tap on its tile or on its slot). A full selection ignores more cards until one is removed.</summary>
        public void Toggle(HeroCard card)
        {
            if (HeroCards.Toggle(picked, card)) BuildNow();
        }

        /// <summary>Taps the card at this position of <see cref="Cards"/> (tests).</summary>
        public void TapCard(int index)
        {
            if (index >= 0 && index < cards.Count) Toggle(cards[index]);
        }

        /// <summary>Selects the first three cards of a hero type (tests and shortcuts) and rebuilds.</summary>
        public void SelectType(string id)
        {
            PreselectType(id);
            BuildNow();
        }

        // ------------------------------------------------------------ result popup: "New hero: <name>"

        /// <summary>The short result card after a successful merge: the new hero, its rarity and its stat gains over the hero it came from. One OK button.</summary>
        public void ShowResult(MergeOutcome o)
        {
            if (host == null || host.NewOverlay == null || o == null || o.Result != MergeResult.Ok) return;
            CharacterSpec to = CharacterDefs.Get(o.ToId), from = CharacterDefs.Get(o.FromId);
            if (to == null) return;
            CharacterDef who = CharacterCatalog.Get(to.Id);
            RectTransform overlay = host.NewOverlay();

            float lTitle = Line(64), lName = Line(52), lBody = Line(BodyFont);
            const float dollH = 330f, okS = 148f;
            List<string> gain = MergeRules.ShownGainLines(from != null ? from.ShownValues() : null, to.ShownValues());
            float h = 36f + lTitle + 10f + dollH + 12f + 2f * lName + 8f + lBody + 8f + GainLines * lBody + 14f + okS + 28f;
            RectTransform panel = UiKit.Rect(overlay, "Panel");
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, h));
            panel.pivot = new Vector2(0.5f, 0.5f);
            Theme.CardBacking(panel);

            float y = 36f;
            Text title = UiKit.OnPanel(UiKit.AddText(TopRow(panel, "TitleRow", y, lTitle, 24f), "Title", "New hero:", 64, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
            UiKit.Stretch(title.rectTransform);
            y += lTitle + 10f;

            RectTransform stage = TopRow(panel, "DollRow", y, dollH, 0f);
            Image glowImg = UiKit.AddImage(stage, "Glow", MergeArt.Glow, new Color(0.62f, 0.38f, 1f, 0.55f));
            UiKit.Place(glowImg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(dollH * 1.15f, dollH * 1.15f));
            glowImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            RectTransform doll = MomoDoll.Build(stage, "Hero", dollH, who, art);
            UiKit.Place(doll, new Vector2(0.5f, 0.5f), Vector2.zero, doll.sizeDelta);
            doll.pivot = new Vector2(0.5f, 0.5f);
            y += dollH + 12f;

            Text nm = UiKit.OnPanel(UiKit.AddText(TopRow(panel, "NameRow", y, 2f * lName, 24f), "HeroName", to.Name, 52, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
            UiKit.Stretch(nm.rectTransform);
            y += 2f * lName + 8f;

            Image chip = UiKit.AddPanel(panel, "Fill", RarityUi.Fill(to.Rarity));
            string rarityText = (from != null ? CharacterDefs.RarityNames[(int)from.Rarity] + " > " : "") + CharacterDefs.RarityNames[(int)to.Rarity];
            UiKit.Place(chip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(560f, lBody));
            Text rt = UiKit.OnPanel(UiKit.AddText(chip.transform, "Rarity", rarityText, BodyFont, UiKit.PanelInk));
            rt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(rt.rectTransform);
            y += lBody + 8f;

            Text gn = UiKit.OnPanel(UiKit.AddText(TopRow(panel, "GainsRow", y, GainLines * lBody, 24f), "Gains", string.Join("\n", gain.ToArray()), BodyFont, new Color(0.05f, 0.38f, 0.16f, 1f), TextAnchor.UpperCenter));
            gn.color = new Color(0.05f, 0.38f, 0.16f, 1f);
            UiKit.Stretch(gn.rectTransform);

            Button ok = ToyKit.IconButton(panel, "OK", "OK", 1, Glyphs.Check, new Vector2(okS, okS), () => { if (host.CloseOverlay != null) host.CloseOverlay(); }, 0.56f);
            UiKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(okS, okS));
            panel.gameObject.AddComponent<A11yLabel>().Text = "New hero: " + to.Name + ", " + rarityText;
        }
    }
}
