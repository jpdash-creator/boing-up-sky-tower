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
    }

    /// <summary>
    /// Character merge (docs/34): the big result slot on top (the next tier of the chosen hero with its rarity change and stat gains), three input slots that fill with the
    /// copies the player holds, a green arrow, the gold MERGE button, and under it the grid of heroes that have copies (tap one to choose it, "x2/3" shows its stack).
    /// Everything is built once per change (no Update, no per-frame work). Layout is anchored by fractions and computed from the real text sizes, so it holds at 130 percent text.
    /// </summary>
    public sealed class MergeScreen
    {
        public const string ScreenName = "Merge";
        const float Margin = 28f, TileH = 232f, TileGap = 10f, SlotH = 190f, ArrowH = 64f, Pad = 18f;
        const int Columns = 5, NameFont = 46, BodyFont = 40, GainLines = 6, RarityLines = 2;
        static readonly Color PillInk = new Color(0.07f, 0.06f, 0.20f, 0.94f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.24f, 1f);
        static readonly Color ArrowGreen = new Color(0.36f, 0.95f, 0.52f, 1f);

        /// <summary>What the screen needs from the menu.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            /// <summary>Back returns to the Heroes screen.</summary>
            public Action Back;
        }

        readonly RectTransform screen, root;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly Host host;
        RectTransform body, bg, content;
        CharacterDef selected;
        float scroll;

        /// <summary>The hero whose copies are shown in the slots (null when the player has no copies at all).</summary>
        public CharacterDef Selected { get { return selected; } }
        /// <summary>The MERGE button (interactable only when a merge would succeed). Tests tap it through <see cref="TapMerge"/>.</summary>
        public Button MergeButton { get; private set; }
        /// <summary>The character the result slot previews, or null.</summary>
        public CharacterDef Preview { get; private set; }

        public MergeScreen(RectTransform screen, RectTransform screenRoot, ProgressService progress, ArtCatalog art, Host host)
        {
            this.screen = screen;
            this.root = screenRoot;
            this.progress = progress;
            this.art = art;
            this.host = host;
        }

        static float Line(int font) { return Mathf.Ceil(Mathf.Max(font, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.3f); }

        // ------------------------------------------------------------ build

        /// <summary>Builds (or rebuilds) the screen. <paramref name="preselectId"/> picks the hero shown first.</summary>
        public void Build(string preselectId = null)
        {
            if (body != null) UnityEngine.Object.Destroy(body.gameObject);
            if (bg == null) BuildBackground();

            List<CharacterDef> owned = OwnedWithCopies();
            if (preselectId != null) selected = Find(owned, preselectId) ?? selected;
            if (selected == null || !owned.Contains(selected)) selected = DefaultPick(owned);

            body = UiKit.Rect(screen, "MergeBody");
            UiKit.Stretch(body);

            float y = BuildTopBar();
            CharacterSpec spec = selected != null ? selected.Info : null;
            CharacterSpec next = spec != null ? MergeRules.NextTier(spec) : null;
            int copies = selected != null ? progress.Copies(selected.Id) : 0;
            Preview = next != null ? CharacterCatalog.Get(next.Id) : null;

            y = BuildResultCard(y, spec, next);
            y = BuildSlots(y, spec, copies, next != null);
            y = BuildMergeButton(y, spec, copies, next);
            BuildGrid(y, owned);
        }

        /// <summary>The heroes the player may see and holds at least one copy of, in list order.</summary>
        List<CharacterDef> OwnedWithCopies()
        {
            var list = new List<CharacterDef>(CharacterCatalog.All.Length);
            foreach (CharacterDef c in CharacterCatalog.Available(progress))
                if (progress.Copies(c.Id) > 0) list.Add(c);
            return list;
        }

        static CharacterDef Find(List<CharacterDef> list, string id)
        {
            foreach (CharacterDef c in list) if (c.Id == id) return c;
            return null;
        }

        /// <summary>A hero that can merge now, else the one with the biggest stack that has a next tier, else the first.</summary>
        CharacterDef DefaultPick(List<CharacterDef> owned)
        {
            CharacterDef best = null;
            int bestCopies = -1;
            foreach (CharacterDef c in owned)
            {
                if (progress.CanMerge(c.Id)) return c;
                int n = progress.Copies(c.Id);
                if (progress.NextTierOf(c.Id) != null && n > bestCopies) { best = c; bestCopies = n; }
            }
            return best ?? (owned.Count > 0 ? owned[0] : null);
        }

        // ------------------------------------------------------------ background (slot: MergeBackground)

        void BuildBackground()
        {
            bg = UiKit.Rect(root, "MergeBackground");
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
                return;
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
        }

        // ------------------------------------------------------------ top bar

        float BuildTopBar()
        {
            float bs = Mathf.Max(140f, UiKit.MinTouchRef);
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
            return 16f + bs + 8f;
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
            float cardH = Pad + 2f * lName + 6f + RarityLines * lBody + 6f + GainLines * lBody + Pad;   // fixed: 2 name lines, 2 rarity lines (may wrap), up to 6 gain lines, so the screen never jumps
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
            if (spec == null) { nameText = "Nothing to merge"; rarityText = "Collect copies of a hero"; gains = ""; }
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
            Text gn = UiKit.AddText(TopRow(info, "GainsRow", cy, GainLines * lBody, 0f), "ResultGains", gains, BodyFont, new Color(0.62f, 1f, 0.72f, 1f), TextAnchor.UpperLeft);
            UiKit.Stretch(gn.rectTransform);
            gn.lineSpacing = 1f;
            card.gameObject.AddComponent<A11yLabel>().Text = "Result: " + nameText + ", " + rarityText + (gains.Length > 0 ? ", " + gains.Replace("\n", ", ") : "");
            return y + cardH + 8f;
        }

        // ------------------------------------------------------------ input slots

        float BuildSlots(float y, CharacterSpec spec, int copies, bool hasNext)
        {
            // green arrow pointing from the slots up to the result
            Image arrow = UiKit.AddImage(body, "Arrow", Glyphs.Up, ArrowGreen);
            UiKit.Place(arrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(ArrowH, ArrowH));
            y += ArrowH + 8f;

            RectTransform row = TopRow(body, "Slots", y, SlotH, Margin);
            CharacterRarity rarity = spec != null ? spec.Rarity : CharacterRarity.Common;
            CharacterDef who = selected;
            for (int i = 0; i < MergeRules.CopiesNeeded; i++)
            {
                bool filled = who != null && copies > i;
                RectTransform inner = RarityUi.Box(row, "Slot_" + (i + 1), rarity, !filled, new Vector2(100f, 100f));
                RectTransform outer = (RectTransform)inner.parent;
                float x0 = 0.03f + i * 0.33f;
                outer.anchorMin = new Vector2(x0, 0f);
                outer.anchorMax = new Vector2(x0 + 0.28f, 1f);
                outer.offsetMin = outer.offsetMax = Vector2.zero;
                outer.pivot = new Vector2(0.5f, 0.5f);
                if (filled)
                {
                    RectTransform area = UiKit.Rect(inner, "FaceArea");
                    area.anchorMin = new Vector2(0.06f, 0.05f);
                    area.anchorMax = new Vector2(0.94f, 0.95f);
                    area.offsetMin = area.offsetMax = Vector2.zero;
                    RectTransform face = MomoDoll.Build(area, "Face", 100f, who, art);
                    UiKit.Stretch(face);
                    var fit = face.gameObject.AddComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fit.aspectRatio = who.Rig.CanvasW / who.Rig.CanvasH;
                }
                outer.gameObject.AddComponent<A11yLabel>().Text = "Merge slot " + (i + 1) + (filled ? ", " + who.Name : ", empty");
            }
            return y + SlotH + 14f;
        }

        // ------------------------------------------------------------ MERGE button

        float BuildMergeButton(float y, CharacterSpec spec, int copies, CharacterSpec next)
        {
            bool can = spec != null && next != null && copies >= MergeRules.CopiesNeeded;
            const float bh = 140f;
            Color tint = can ? Color.white : new Color(0.62f, 0.62f, 0.68f, 1f);
            MergeButton = UiKit.AddButton(body, "MergeButton", "MERGE", ToyKit.Button(0), tint, new Vector2(560f, bh), OnMergeTapped, 64);
            MergeButton.interactable = can;
            UiKit.Place((RectTransform)MergeButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(560f, bh));
            string hint;
            if (spec == null) hint = "No heroes with copies yet";
            else if (next == null) hint = "Top tier: nothing to merge into";
            else if (can) hint = "Ready: " + copies + "/" + MergeRules.CopiesNeeded + " copies";
            else hint = "Need " + MergeRules.CopiesNeeded + " copies (you have " + copies + ")";
            MergeButton.GetComponent<A11yLabel>().Text = "Merge. " + hint;
            y += bh + 6f;
            float lh = Line(BodyFont);
            Text t = UiKit.AddText(TopRow(body, "HintRow", y, lh, Margin), "Hint", hint, BodyFont, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(t.rectTransform);
            return y + lh + 12f;
        }

        void OnMergeTapped()
        {
            if (selected == null) return;
            MergeOutcome o = progress.TryMerge(selected.Id);
            if (o.Result != MergeResult.Ok) { Build(); return; }
            AudioManager.Play(Sfx.Win);
            Haptics.Pulse();
            // the lower tier stays selected while copies are left, else the new hero
            selected = progress.Copies(o.FromId) > 0 ? selected : CharacterCatalog.Get(o.ToId);
            Build();
            ShowResult(o);
        }

        /// <summary>Taps MERGE as a player would (tests). Returns the outcome; NotEnoughCopies when the button is not usable.</summary>
        public MergeOutcome TapMerge()
        {
            if (selected == null) return new MergeOutcome { Result = MergeResult.Unknown };
            if (MergeButton == null || !MergeButton.interactable) return new MergeOutcome { Result = progress.NextTierOf(selected.Id) == null ? MergeResult.MaxTier : MergeResult.NotEnoughCopies, FromId = selected.Id };
            string from = selected.Id;
            OnMergeTapped();
            return new MergeOutcome { Result = MergeResult.Ok, FromId = from };
        }

        // ------------------------------------------------------------ hero grid

        void BuildGrid(float y, List<CharacterDef> owned)
        {
            RectTransform vp = UiKit.Rect(body, "GridViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(Margin, 12f);
            vp.offsetMax = new Vector2(-Margin, -y);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            int rows = Mathf.Max(1, (owned.Count + Columns - 1) / Columns);
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

            float pillH = Mathf.Ceil(Line(BodyFont) + 6f);
            for (int i = 0; i < owned.Count; i++)
            {
                CharacterDef who = owned[i];
                CharacterSpec s = who.Info;
                CharacterSpec nx = MergeRules.NextTier(s);
                int n = progress.Copies(who.Id);
                int col = i % Columns, row = i / Columns;
                RectTransform inner = RarityUi.Box(content, "Tile_" + who.Id, s.Rarity, false, new Vector2(100f, TileH));
                RectTransform outer = (RectTransform)inner.parent;
                outer.anchorMin = new Vector2(col / (float)Columns, 1f);
                outer.anchorMax = new Vector2((col + 1) / (float)Columns, 1f);
                outer.pivot = new Vector2(0.5f, 1f);
                outer.sizeDelta = new Vector2(-TileGap, TileH - TileGap);
                outer.anchoredPosition = new Vector2(0f, -(row * TileH + TileGap * 0.5f));

                RectTransform area = UiKit.Rect(inner, "FaceArea");
                area.anchorMin = new Vector2(0.04f, 0f);
                area.anchorMax = new Vector2(0.96f, 1f);
                area.offsetMin = new Vector2(0f, pillH + 12f);
                area.offsetMax = new Vector2(0f, -6f);
                RectTransform face = MomoDoll.Build(area, "Face", 100f, who, art);
                UiKit.Stretch(face);
                var fit = face.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = who.Rig.CanvasW / who.Rig.CanvasH;

                // "x2/3" on a dark pill (white text, always readable); a mergeable stack says so in words on the button, not only by colour
                bool ready = nx != null && n >= MergeRules.CopiesNeeded;
                Image pill = UiKit.AddPanel(inner, "CountPill", ready ? new Color(0.10f, 0.45f, 0.24f, 0.96f) : PillInk);
                RectTransform pr = pill.rectTransform;
                pr.anchorMin = new Vector2(0.06f, 0f);
                pr.anchorMax = new Vector2(0.94f, 0f);
                pr.pivot = new Vector2(0.5f, 0f);
                pr.anchoredPosition = new Vector2(0f, 6f);
                pr.sizeDelta = new Vector2(0f, pillH);
                string count = nx != null ? "x" + n + "/" + MergeRules.CopiesNeeded : "x" + n;
                Text ct = UiKit.AddText(pill.transform, "Count", count, BodyFont, Color.white, TextAnchor.MiddleCenter, true);
                ct.horizontalOverflow = HorizontalWrapMode.Overflow;
                var co = ct.GetComponent<Outline>();
                if (co != null) co.enabled = false;
                UiKit.Stretch(ct.rectTransform);

                if (who == selected)
                {
                    Image chosen = UiKit.AddImage(outer, "Chosen", Theme.RingSprite, Gold);
                    chosen.type = Image.Type.Sliced;
                    chosen.preserveAspect = false;
                    chosen.pixelsPerUnitMultiplier = 1.0f;
                    chosen.rectTransform.anchorMin = Vector2.zero;
                    chosen.rectTransform.anchorMax = Vector2.one;
                    chosen.rectTransform.offsetMin = new Vector2(-3f, -3f);
                    chosen.rectTransform.offsetMax = new Vector2(3f, 3f);
                }

                var b = outer.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                outer.GetComponent<Image>().raycastTarget = true;
                CharacterDef pick = who;
                b.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); Select(pick); });
                outer.gameObject.AddComponent<A11yLabel>().Text = who.Name + ", " + CharacterDefs.RarityNames[(int)s.Rarity] + ", " + n + (nx != null ? " of " + MergeRules.CopiesNeeded + " copies" : " copies, top tier") + (ready ? ", ready to merge" : "") + (who == selected ? ", chosen" : "");
            }
            if (scroll > 0f) content.anchoredPosition = new Vector2(0f, scroll);
            sr.onValueChanged.AddListener(_ => scroll = content.anchoredPosition.y);
        }

        /// <summary>Chooses which hero's copies are merged (a tap on its tile).</summary>
        public void Select(CharacterDef who)
        {
            if (who == null || progress.Copies(who.Id) <= 0) return;
            selected = who;
            Build();
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
