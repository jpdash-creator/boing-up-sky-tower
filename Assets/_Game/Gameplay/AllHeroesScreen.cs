using System;
using System.Collections.Generic;
using System.Text;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The All Heroes screen (docs/34 section 10): EVERY character of the game as cards, 3 to a row, as many rows as needed, scrolling, grouped BY REGION under a banner (the groups
    /// come from <see cref="AllHeroes.Build"/>, so more heroes per region later need no code here). Heroes the player owns are in full colour; everything else is LOCKED (muted card, dark
    /// silhouette, padlock), Rare, Elite and Legendary included, even with Character Test on. Every card has an "i" button (at least 48 dp) that opens a details popup with the stats and powers,
    /// and for a locked hero how to get it. Built once per change; no Update, no per-frame work.
    /// </summary>
    public sealed class AllHeroesScreen
    {
        public const string ScreenName = "AllHeroes";
        public const string InfoPopupName = "AllHeroesInfo";
        const int Columns = 3;
        /// <summary>Cards of one hero line: Common, Uncommon, Rare, Elite, Legendary.</summary>
        const int LineSize = 5;
        const float Margin = 16f, Gap = 12f, DollH = 210f, Pad = 8f;
        const int BodyFont = 40;
        static readonly Color PillInk = new Color(0.07f, 0.06f, 0.20f, 0.94f);
        static readonly Color Silhouette = new Color(0.14f, 0.16f, 0.27f, 1f);
        static readonly Color LockedGrey = new Color(0.36f, 0.38f, 0.47f, 1f);

        /// <summary>What the screen needs from the menu.</summary>
        public sealed class Host
        {
            public Func<RectTransform> NewOverlay;
            public Action CloseOverlay;
            /// <summary>Back returns to the Heroes screen.</summary>
            public Action Back;
            /// <summary>The other two tabs of the top row (Bug List 6 item 3a): Merge and Powers. A tab without an action is inert.</summary>
            public Action OpenMerge, OpenPowers;
            /// <summary>Name of a region by index (null = "Region n").</summary>
            public Func<int, string> RegionName;
        }

        readonly RectTransform screen, root;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly Host host;
        RectTransform body, bg;
        ScrollRect scroll;

        /// <summary>The groups shown (region banner plus its heroes), in screen order.</summary>
        public List<AllHeroGroup> Groups { get; private set; }
        /// <summary>The scroll area (tests measure it and scroll it).</summary>
        public ScrollRect Scroll { get { return scroll; } }
        /// <summary>The entry whose details popup is open, or null.</summary>
        public AllHeroEntry OpenInfoFor { get; private set; }

        public AllHeroesScreen(RectTransform screen, RectTransform screenRoot, ProgressService progress, ArtCatalog art, Host host)
        {
            this.screen = screen;
            this.root = screenRoot;
            this.progress = progress;
            this.art = art;
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

        /// <summary>Touch size of the "i" button: at least 48 dp on the real screen.</summary>
        static float InfoSize { get { return Mathf.Max(UiKit.MinTouchRef, UiKit.MinTouchPx); } }

        /// <summary>Height of one card at the current text size: the "i" strip, the silhouette, and a two-line name pill.</summary>
        public static float CardHeight { get { return Pad + InfoSize + 6f + DollH + 6f + 2f * Line(BodyFont) + 16f + Pad; } }

        /// <summary>Height of a region banner at the current text size.</summary>
        static float BannerHeight { get { return Line(52) + 24f; } }

        // ------------------------------------------------------------ build

        public void Build()
        {
            if (body != null) UnityEngine.Object.Destroy(body.gameObject);
            if (bg == null)
            {
                bg = MergeArt.AddBackground(root, "AllHeroesBackground");   // the Merge screen's background (Bug List 6 item 3a): same painted slot, gradient and glows
            }
            Groups = AllHeroes.Build(host != null ? host.RegionName : null, id => CharacterCatalog.Owns(progress, CharacterCatalog.Get(id)));

            body = UiKit.Rect(screen, "AllHeroesBody");
            UiKit.Stretch(body);

            float bs = Mathf.Max(140f, UiKit.MinTouchRef);
            Button back = ToyKit.GlyphButton(body, "Back", "Back", 2, Glyphs.Left, new Vector2(bs, bs), () => { if (host != null && host.Back != null) host.Back(); });
            UiKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(bs, bs));
            Text title = UiKit.AddText(body, "Title", "ALL HEROES", 72, Color.white, TextAnchor.MiddleCenter, true);
            RectTransform tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.offsetMin = new Vector2(bs + 40f, -(16f + bs));
            tr.offsetMax = new Vector2(-(bs + 40f), -16f);

            float tabsTop = 16f + bs + 8f;
            HeroTabs.Build(body, tabsTop, HeroTabs.AllHeroes, null, host != null ? host.OpenMerge : null, host != null ? host.OpenPowers : null, false, HeroTabs.Counts(progress));
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

            float cardH = CardHeight, bannerH = BannerHeight;
            float y = 4f;
            foreach (AllHeroGroup g in Groups)
            {
                BuildBanner(content, g, y, bannerH);
                y += bannerH + 10f;
                // one hero line (Common to Legendary = 5 cards) per block of rows, so a hero's ladder is never split across other heroes: 3 cards, then 2
                for (int b = 0; b * LineSize < g.Entries.Count; b++)
                {
                    int inBlock = Math.Min(LineSize, g.Entries.Count - b * LineSize);
                    for (int k = 0; k < inBlock; k++) BuildCard(content, g.Entries[b * LineSize + k], k % Columns, y + (k / Columns) * (cardH + Gap), cardH);
                    y += ((inBlock + Columns - 1) / Columns) * (cardH + Gap);
                }
                y += 14f;
            }
            content.sizeDelta = new Vector2(0f, y);
        }

        void BuildBanner(RectTransform content, AllHeroGroup g, float y, float h)
        {
            RectTransform row = TopRow(content, "Banner_" + g.Region, y, h, 0f);
            Image panel = UiKit.AddPanel(row, "Panel", PillInk);
            UiKit.Stretch(panel.rectTransform);
            Image rim = UiKit.AddImage(row, "Rim", Theme.RingSprite, new Color(1f, 0.82f, 0.24f, 0.9f));
            rim.type = Image.Type.Sliced;
            rim.preserveAspect = false;
            rim.pixelsPerUnitMultiplier = 1.4f;
            UiKit.Stretch(rim.rectTransform);
            Text t = UiKit.AddText(row, "Name", g.RegionName, 52, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            UiKit.Stretch(t.rectTransform);
            row.gameObject.AddComponent<A11yLabel>().Text = g.RegionName;
        }

        void BuildCard(RectTransform content, AllHeroEntry e, int col, float y, float cardH)
        {
            RectTransform inner = RarityUi.Box(content, "Hero_" + e.Id, e.Rarity, false, new Vector2(100f, cardH));
            RectTransform outer = (RectTransform)inner.parent;
            outer.anchorMin = new Vector2(col / (float)Columns, 1f);
            outer.anchorMax = new Vector2((col + 1) / (float)Columns, 1f);
            outer.pivot = new Vector2(0.5f, 1f);
            outer.sizeDelta = new Vector2(-Gap, cardH);
            outer.anchoredPosition = new Vector2(0f, -y);
            if (e.Locked)
            {
                Image fill = inner.GetComponent<Image>();   // the rarity colour, muted: still readable as Common / Uncommon / Rare / Elite / Legendary, but clearly not yours
                fill.color = Color.Lerp(RarityUi.Fill(e.Rarity), LockedGrey, 0.62f);
            }

            float ib = InfoSize;
            float lh = Line(BodyFont);

            // top strip: the padlock on the left (locked only), the "i" button on the right
            if (e.Locked)
            {
                float ls = Mathf.Round(ib * 0.6f);
                Image padlock = UiKit.AddImage(inner, "Lock", Glyphs.Lock, Color.white);
                padlock.raycastTarget = false;
                RectTransform lr = padlock.rectTransform;
                lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
                lr.pivot = new Vector2(0f, 1f);
                lr.anchoredPosition = new Vector2(6f, -(ib - ls) * 0.5f);
                lr.sizeDelta = new Vector2(ls, ls);
            }
            AllHeroEntry pickEntry = e;
            Button info = UiKit.AddButton(inner, "Info", "i", ToyKit.Button(2), Color.white, new Vector2(ib, ib), () => ShowInfo(pickEntry), 64);
            info.GetComponent<A11yLabel>().Text = "Details of " + e.Name;
            UiKit.Place((RectTransform)info.transform, new Vector2(1f, 1f), new Vector2(-2f, -2f), new Vector2(ib, ib));
            ((RectTransform)info.transform).pivot = new Vector2(1f, 1f);

            // the hero (silhouette when locked) under the strip
            RectTransform area = UiKit.Rect(inner, "FaceArea");
            area.anchorMin = new Vector2(0.04f, 1f);
            area.anchorMax = new Vector2(0.96f, 1f);
            area.pivot = new Vector2(0.5f, 1f);
            area.offsetMin = new Vector2(0f, -(ib + 6f + DollH));
            area.offsetMax = new Vector2(0f, -(ib + 6f));
            AddDoll(area, e);

            // name pill: the Common hero's name on line 1, the rarity word on line 2 (white on dark, any rarity colour)
            Image pill = UiKit.AddPanel(inner, "NamePill", PillInk);
            pill.raycastTarget = false;
            RectTransform pr = pill.rectTransform;
            pr.anchorMin = new Vector2(0.03f, 0f);
            pr.anchorMax = new Vector2(0.97f, 0f);
            pr.pivot = new Vector2(0.5f, 0f);
            pr.anchoredPosition = new Vector2(0f, 4f);
            pr.sizeDelta = new Vector2(0f, 2f * lh + 12f);
            Text name = UiKit.AddText(pill.transform, "HeroName", e.BaseName, BodyFont, Color.white, TextAnchor.UpperCenter, true);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.offsetMin = new Vector2(0f, -(lh + 4f));
            name.rectTransform.offsetMax = new Vector2(0f, -4f);
            DropOutline(name);
            Text rar = UiKit.AddText(pill.transform, "Rarity", e.RarityName, BodyFont, Color.white, TextAnchor.UpperCenter);
            rar.horizontalOverflow = HorizontalWrapMode.Overflow;
            rar.rectTransform.anchorMin = Vector2.zero;
            rar.rectTransform.anchorMax = new Vector2(1f, 0f);
            rar.rectTransform.pivot = new Vector2(0.5f, 0f);
            rar.rectTransform.offsetMin = new Vector2(0f, 4f);
            rar.rectTransform.offsetMax = new Vector2(0f, 4f + lh);
            DropOutline(rar);

            outer.gameObject.AddComponent<A11yLabel>().Text = e.Name + ", " + e.RarityName + (e.Locked ? ", locked" : ", owned");
        }

        static void DropOutline(Text t)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }

        /// <summary>The hero picture in the area (the Common hero's art for a placeholder tier); a locked one is a dark silhouette.</summary>
        void AddDoll(RectTransform area, AllHeroEntry e)
        {
            CharacterDef who = CharacterCatalog.Get(e.Placeholder ? e.BaseSpec.Id : e.Id);
            RectTransform face = MomoDoll.Build(area, "Face", 100f, who, art);
            UiKit.Stretch(face);
            var fit = face.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = who.Rig.CanvasW / (float)who.Rig.CanvasH;
            if (e.Locked) foreach (Image im in face.GetComponentsInChildren<Image>()) im.color = Silhouette;
        }

        // ------------------------------------------------------------ details popup

        /// <summary>Opens the details popup of the entry with this id (a tap on its "i"); false when the id is not on the screen.</summary>
        public bool OpenInfo(string id)
        {
            foreach (AllHeroEntry e in AllHeroes.Flatten(Groups))
                if (e.Id == id) { ShowInfo(e); return true; }
            return false;
        }

        /// <summary>Scrolls the list: 0 = top, 1 = bottom (tests).</summary>
        public void ScrollTo(float t)
        {
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases();   // the viewport size is only known after a layout pass
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(t);
        }

        static string StatLines(AllHeroEntry e)
        {
            var sb = new StringBuilder();
            if (!e.Placeholder)
            {
                HeroSheetData d = HeroSheetData.For(CharacterCatalog.Get(e.Id));
                for (int i = 0; i < 4; i++) sb.Append(d.Stats[i, 0]).Append(' ').Append(d.Stats[i, 1]).Append('\n');
                sb.Append("Run coins +").Append(d.RunBonusCoins).Append('\n');
                sb.Append("Run gems +").Append(d.RunBonusGems);
                return sb.ToString();
            }
            TierRule rule = TierRules.RuleFor(e.Rarity);
            sb.Append("Not made yet\n");
            if (rule != null)
            {
                sb.Append(TierRules.JumpBandText(e.Rarity)).Append('\n');
                sb.Append("Speed, Control, Wall: set by limits\n");
                sb.Append("Coins +").Append(rule.RunCoinsMin).Append('-').Append(rule.RunCoinsMax).Append('\n');
                sb.Append("Gems +").Append(rule.RunGemsMin).Append('-').Append(rule.RunGemsMax);
            }
            return sb.ToString();
        }

        static string PowerLines(AllHeroEntry e)
        {
            PowerUp[] powers = e.Spec != null ? e.Spec.Powers : TierRules.PowersFor(e.LineId, e.Rarity);
            if (powers == null || powers.Length == 0) return "No special power";
            var sb = new StringBuilder("Powers: ");
            PowerUpCatalog cat = PowerUpCatalog.BuiltIn();
            for (int i = 0; i < powers.Length; i++)
            {
                PowerUpDef def = cat.Get(powers[i]);
                if (i > 0) sb.Append(", ");
                sb.Append(def != null ? def.Name : powers[i].ToString());
            }
            sb.Append("\nFree use, ready again after ").Append(CharacterDefs.CooldownSeconds[(int)e.Rarity]).Append(" s");
            return sb.ToString();
        }

        void ShowInfo(AllHeroEntry e)
        {
            if (host == null || host.NewOverlay == null) return;
            OpenInfoFor = e;
            RectTransform overlay = host.NewOverlay();
            float lTitle = Line(52), lBody = Line(BodyFont);
            const float panelW = 900f, dollH = 280f, okS = 148f, leftX = 36f, leftW = 300f;
            float rowH = Mathf.Max(6f * lBody, dollH + 8f + lBody);
            float h = 36f + 2f * lTitle + 8f + rowH + 12f + 3f * lBody + 8f + 3f * lBody + 14f + okS + 28f;
            RectTransform panel = UiKit.Rect(overlay, InfoPopupName);
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelW, h));
            panel.pivot = new Vector2(0.5f, 0.5f);
            Theme.CardBacking(panel);

            float y = 36f;
            Text nm = UiKit.OnPanel(UiKit.AddText(TopRow(panel, "TitleRow", y, 2f * lTitle, 24f), "HeroName", e.Name, 52, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
            UiKit.Stretch(nm.rectTransform);
            y += 2f * lTitle + 8f;

            // left: the picture (silhouette when locked) and the rarity chip; right: the stats
            RectTransform stage = TopRow(panel, "DollRow", y, rowH, 0f);
            RectTransform dollBox = UiKit.Rect(stage, "DollBox");
            dollBox.anchorMin = dollBox.anchorMax = new Vector2(0f, 1f);
            dollBox.pivot = new Vector2(0f, 1f);
            dollBox.anchoredPosition = new Vector2(leftX, 0f);
            dollBox.sizeDelta = new Vector2(leftW, dollH);
            AddDoll(dollBox, e);
            Image chip = UiKit.AddPanel(stage, "Fill", e.Locked ? Color.Lerp(RarityUi.Fill(e.Rarity), LockedGrey, 0.4f) : RarityUi.Fill(e.Rarity));
            chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0f, 1f);
            chip.rectTransform.pivot = new Vector2(0f, 1f);
            chip.rectTransform.anchoredPosition = new Vector2(leftX, -(dollH + 8f));
            chip.rectTransform.sizeDelta = new Vector2(leftW, lBody);
            Text rt = UiKit.OnPanel(UiKit.AddText(chip.transform, "Rarity", e.RarityName, BodyFont, UiKit.PanelInk));
            rt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(rt.rectTransform);
            if (e.Locked)
            {
                float ls = Mathf.Round(lBody * 1.4f);
                Image padlock = UiKit.AddImage(stage, "Lock", Glyphs.Lock, Color.white);
                padlock.raycastTarget = false;
                padlock.rectTransform.anchorMin = padlock.rectTransform.anchorMax = new Vector2(0f, 1f);
                padlock.rectTransform.pivot = new Vector2(0f, 1f);
                padlock.rectTransform.anchoredPosition = new Vector2(leftX + leftW * 0.5f - ls * 0.5f, -(dollH * 0.5f - ls * 0.5f));
                padlock.rectTransform.sizeDelta = new Vector2(ls, ls);
            }
            RectTransform statBox = UiKit.Rect(stage, "Stats");
            statBox.anchorMin = new Vector2(0f, 1f);
            statBox.anchorMax = new Vector2(1f, 1f);
            statBox.pivot = new Vector2(0f, 1f);
            statBox.offsetMin = new Vector2(leftX + leftW + 24f, -rowH);
            statBox.offsetMax = new Vector2(-24f, 0f);
            Text stats = UiKit.OnPanel(UiKit.AddText(statBox, "StatLines", StatLines(e), BodyFont, UiKit.PanelInk, TextAnchor.UpperLeft));
            stats.lineSpacing = 1f;
            UiKit.Stretch(stats.rectTransform);
            y += rowH + 12f;

            Text pw = UiKit.OnPanel(UiKit.AddText(TopRow(panel, "PowerRow", y, 3f * lBody, 36f), "Powers", PowerLines(e), BodyFont, UiKit.PanelInk, TextAnchor.UpperLeft));
            UiKit.Stretch(pw.rectTransform);
            y += 3f * lBody + 8f;

            string how = e.Locked ? "How to get: " + e.HowToGet : "You own this hero";
            Text hw = UiKit.OnPanel(UiKit.AddText(TopRow(panel, "HowRow", y, 3f * lBody, 36f), "HowToGet", how, BodyFont, e.Locked ? new Color(0.55f, 0.18f, 0.05f, 1f) : new Color(0.05f, 0.38f, 0.16f, 1f), TextAnchor.UpperLeft));
            hw.color = e.Locked ? new Color(0.55f, 0.18f, 0.05f, 1f) : new Color(0.05f, 0.38f, 0.16f, 1f);
            UiKit.Stretch(hw.rectTransform);

            Button ok = ToyKit.IconButton(panel, "OK", "Close details", 1, Glyphs.Check, new Vector2(okS, okS), CloseInfo, 0.56f);
            UiKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(okS, okS));
            panel.gameObject.AddComponent<A11yLabel>().Text = e.Name + ", " + e.RarityName + (e.Locked ? ", locked. How to get: " + e.HowToGet : ", owned");
        }

        /// <summary>Closes the details popup.</summary>
        public void CloseInfo()
        {
            OpenInfoFor = null;
            if (host != null && host.CloseOverlay != null) host.CloseOverlay();
        }
    }
}
