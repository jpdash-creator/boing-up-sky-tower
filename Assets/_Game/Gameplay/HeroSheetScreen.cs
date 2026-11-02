using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>What the hero sheet shows (plain numbers, docs/29). Built from a character's spec, or from a fake one in tests.</summary>
    public sealed class HeroSheetData
    {
        public string Name, RarityName, Region, PowerText;
        public CharacterRarity Rarity;
        /// <summary>Six main stats in table order: Jump, Speed, Control, Wall Bounce, Coins, Gems (label, value).</summary>
        public string[,] Stats = new string[6, 2];
        /// <summary>Three derived numbers (label, value).</summary>
        public string[,] Derived = new string[3, 2];

        static string F(double v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }

        /// <summary>The real hero: name, rarity, region and powers from its spec, numbers from its stats through <see cref="CharacterStats.Sheet"/>.</summary>
        public static HeroSheetData For(CharacterDef who)
        {
            CharacterSpec spec = who.Info;
            string region = "Found in chests";
            if (spec != null && spec.Region > 0)
            {
                RegionDef r = GameSession.Repo != null ? GameSession.Repo.TryGetRegionByIndex(spec.Region) : null;
                region = r != null ? r.Name : "Region " + spec.Region;
            }
            return From(who.Name, spec != null ? spec.Rarity : CharacterRarity.Common, region, spec != null ? spec.Stats : null, spec != null ? spec.Powers : null);
        }

        /// <summary>Any hero from raw values (the test fixture builds a fake Legendary this way).</summary>
        public static HeroSheetData From(string name, CharacterRarity rarity, string region, CharacterStats stats, PowerUp[] powers)
        {
            StatSheet s = (stats ?? CharacterStats.Default).Sheet(GameSession.Cfg);
            var d = new HeroSheetData { Name = name, Rarity = rarity, RarityName = CharacterDefs.RarityNames[(int)rarity], Region = region };
            d.Stats[0, 0] = "Jump"; d.Stats[0, 1] = F(s.Jump);
            d.Stats[1, 0] = "Speed"; d.Stats[1, 1] = F(s.Speed);
            d.Stats[2, 0] = "Control"; d.Stats[2, 1] = F(s.Control);
            d.Stats[3, 0] = "Wall Bounce"; d.Stats[3, 1] = F(s.WallBounce);
            d.Stats[4, 0] = "Coins"; d.Stats[4, 1] = "+" + s.CoinFind;
            d.Stats[5, 0] = "Gems"; d.Stats[5, 1] = "+" + s.GemFind;
            d.Derived[0, 0] = "Full-speed jump"; d.Derived[0, 1] = F(s.FullSpeedJump);
            d.Derived[1, 0] = "Stopping distance"; d.Derived[1, 1] = F(s.StopDistance);
            d.Derived[2, 0] = "Time to top speed"; d.Derived[2, 1] = F(s.TimeToTopSpeed) + " s";
            d.PowerText = PowerLine(rarity, powers);
            return d;
        }

        static string PowerLine(CharacterRarity rarity, PowerUp[] powers)
        {
            if (powers == null || powers.Length == 0) return "No special power";
            var sb = new StringBuilder("Powers: ");
            PowerUpCatalog cat = PowerUpCatalog.BuiltIn();
            for (int i = 0; i < powers.Length; i++)
            {
                PowerUpDef def = cat.Get(powers[i]);
                if (i > 0) sb.Append(", ");
                sb.Append(def != null ? def.Name : powers[i].ToString());
            }
            sb.Append(". Free at the start of a run, ready again after ").Append(CharacterDefs.CooldownSeconds[(int)rarity]).Append(" s.");
            return sb.ToString();
        }
    }

    /// <summary>The hero sheet plates (Resources/Art/HeroSheet, cut by Tools/art_pipeline/process_hero_sheet.py; borders in slices.json). A missing file falls back to a plain rounded wood or gold colour plate (CUX-10).</summary>
    public static class HeroSheetArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Dictionary<string, Vector4> borders;
        static readonly Regex Entry = new Regex("\"([^\"]+)\"\\s*:\\s*\\{[^}]*?\"border\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]", RegexOptions.Singleline);

        /// <summary>Art rows of the ribbon plates (131 px high) that belong to the neighbouring plates, cut off when the sprite is made.</summary>
        public const float RibbonCropTop = 10f, RibbonCropBottom = 12f;
        /// <summary>Vertical centre of the ribbon's front band as a fraction of the cropped plate height (measured on the art), where the word is centred.</summary>
        public const float RibbonBandCenter = 0.33f;

        public static readonly Color Wood =new Color(0.76f, 0.43f, 0.20f, 1f);
        public static readonly Color Gold = new Color(0.99f, 0.74f, 0.20f, 1f);
        public static readonly Color Cream = new Color(0.98f, 0.91f, 0.75f, 1f);

        static Dictionary<string, Vector4> Borders()
        {
            if (borders != null) return borders;
            borders = new Dictionary<string, Vector4>();
            TextAsset ta = Resources.Load<TextAsset>("Art/HeroSheet/slices");
            if (ta == null) return borders;
            foreach (Match m in Entry.Matches(ta.text))
                borders[m.Groups[1].Value] = new Vector4(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value));
            return borders;
        }

        /// <summary>The sprite of one plate with its 9-slice border applied (named panel_hs_x so the layout audit treats it as a surface), or null when the file is missing.</summary>
        public static Sprite Plate(string name)
        {
            Sprite s;
            if (cache.TryGetValue(name, out s) && s != null) return s;
            s = Resources.Load<Sprite>("Art/HeroSheet/" + name);
            Vector4 b;
            if (s != null && Borders().TryGetValue(name, out b))
            {
                Rect rect = s.rect;
                // The ribbon cut still carries slivers of the neighbouring plates: a black outline strip along the top edge and a black bar along the bottom edge.
                // They showed as black lines above and below the ribbon, so the sprite rect skips them (texture y runs bottom-up).
                if (name == "ribbon" || name == "ribbon_gray")
                {
                    rect = new Rect(rect.x, rect.y + RibbonCropBottom, rect.width, rect.height - RibbonCropTop - RibbonCropBottom);
                }
                Sprite sl = Sprite.Create(s.texture, rect, new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect, b);
                sl.name = "panel_hs_" + name;
                s = sl;
            }
            cache[name] = s;
            return s;
        }

        /// <summary>
        /// Draws the plate sliced so its corners keep the proportion they have in the art at the drawn height. <paramref name="drawnHeight"/> is the
        /// height the plate is drawn at when it is not stretched (its natural height times the scale you want).
        /// </summary>
        public static Image Skin(Image img, string plate, Color fallback, float drawnHeight)
        {
            Sprite sp = Plate(plate);
            img.preserveAspect = false;
            img.raycastTarget = false;
            if (sp == null)
            {
                img.sprite = UiKit.PillSprite;
                img.color = fallback;
                img.type = Image.Type.Sliced;
                return img;
            }
            img.sprite = sp;
            img.color = Color.white;
            img.type = Image.Type.Sliced;
            float k = drawnHeight / Mathf.Max(1f, sp.rect.height);   // drawn pixels per art pixel
            float maxBorder = Mathf.Max(sp.border.x, sp.border.y, sp.border.z, sp.border.w);
            img.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 1f / Mathf.Max(0.05f, k));
            if (maxBorder <= 0f) img.type = Image.Type.Simple;
            return img;
        }

        public static float NaturalHeight(string plate, float drawnWidth, float fallback)
        {
            Sprite sp = Plate(plate);
            return sp == null ? fallback : drawnWidth * sp.rect.height / sp.rect.width;
        }

        public static Color RarityTint(CharacterRarity r) { return RarityUi.Fill(r); }

        /// <summary>Ink or white, whichever reads better (4.5:1 or more) on the rarity colour as it shows on the ribbon (the grey ribbon body is about 90 percent bright).</summary>
        public static Color RibbonTextColor(CharacterRarity r)
        {
            Color f = RarityUi.Fill(r);
            Color onRibbon = new Color(f.r * 0.9f, f.g * 0.9f, f.b * 0.9f, 1f);
            return UiKit.Contrast(UiKit.PanelInk, onRibbon) >= UiKit.Contrast(Color.white, onRibbon) ? UiKit.PanelInk : Color.white;
        }
    }

    /// <summary>Re-runs the hero sheet layout when the size of the screen changes (the aspect is known only after the first canvas pass).</summary>
    sealed class HeroSheetFitter : MonoBehaviour
    {
        public Action<float, float> OnSize;
        RectTransform rt;
        void OnRectTransformDimensionsChange()
        {
            if (rt == null) rt = (RectTransform)transform;
            if (OnSize != null && rt.rect.width > 1f) OnSize(rt.rect.width, rt.rect.height);
        }
    }

    /// <summary>
    /// The hero sheet (owner pick sheet_a_wood, Layout A): header with back (left socket) and close (right socket), the hero in the portrait frame, name, rarity banner
    /// (tinted by rarity, with the word), region, then two tabs: "Stats" (six stats in a 3 x 2 table and the "Use this hero" button) and "Details" (three derived numbers
    /// and the power panel). All text is code drawn on the plates, best fit between the largest size and the 16 sp floor. The header stays fixed; the content scrolls inside
    /// the screen only when it does not fit (small aspect or 130 percent text).
    /// </summary>
    public sealed class HeroSheetScreen
    {
        public const string ScreenName = "Hero";
        const float Margin = 20f, HeaderH = 132f, BaseGap = 18f;
        /// <summary>Base sizes of the sleek layout; the spare height of tall screens is shared out over them (see <see cref="Plan"/>).</summary>
        const float BaseStatCellH = 128f, StatIcon = 88f, BaseDerivedRowH = 84f, TabH = 128f, UseH = 128f, PowerH = 300f, PanelPad = 58f;
        const float PortraitFrac = 0.46f, PortraitFracMax = 0.48f;
        static readonly int FloorPx = Mathf.CeilToInt(UiKit.MinBodyTextPx);

        readonly RectTransform root;
        readonly CharacterDef who;
        readonly ArtCatalog art;
        readonly HeroSheetData data;
        readonly bool inUse;
        readonly Action onBack, onClose, onUse;
        RectTransform content;
        float width, height;
        int tab;

        // the plan of the current build: portrait width fraction, gap between blocks, stat cell height, details row height, space above the first block
        float pf, gap, cellH, rowH, topPad;

        public RectTransform Root { get { return root; } }
        /// <summary>0 = Stats, 1 = Details.</summary>
        public int Tab { get { return tab; } }

        public HeroSheetScreen(RectTransform parent, CharacterDef who, ArtCatalog art, HeroSheetData data, bool inUse, Action onBack, Action onClose, Action onUse)
        {
            this.who = who; this.art = art; this.data = data; this.inUse = inUse;
            this.onBack = onBack; this.onClose = onClose; this.onUse = onUse;
            root = UiKit.Rect(parent, "HeroSheet");
            UiKit.Stretch(root);
            Rebuild(Mathf.Max(parent.rect.width, 1f) > 1f ? parent.rect.width : 940f, parent.rect.height > 1f ? parent.rect.height : 1920f);
            var fit = root.gameObject.AddComponent<HeroSheetFitter>();
            fit.OnSize = (w, h) => { float cw = ColumnWidth(w); if (Mathf.Abs(cw - width) > 0.5f || Mathf.Abs(h - height) > 0.5f) Rebuild(w, h); };
        }

        public void SelectTab(int t)
        {
            if (t == tab) return;
            tab = t;
            Rebuild(width + Margin * 2f, height);
        }

        static float ColumnWidth(float screenW) { return Mathf.Clamp(screenW - Margin * 2f, 760f, 1000f); }

        void Rebuild(float screenW, float screenH)
        {
            float w = ColumnWidth(screenW);
            width = w;
            height = screenH;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform c = root.GetChild(i);
                c.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(c.gameObject);
                else UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            BuildHeader(w);
            BuildScroll(w);
        }

        // ---------------- header (fixed) ----------------

        void BuildHeader(float w)
        {
            RectTransform bar = UiKit.Rect(root, "Header");
            UiKit.Place(bar, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(w, HeaderH));
            Image bg = UiKit.AddImage(bar, "Fill", null, Color.white);
            HeroSheetArt.Skin(bg, "header", HeroSheetArt.Wood, HeaderH);
            bg.color = new Color(0.86f, 0.86f, 0.86f, 1f);   // a little darker so the white title reads (4.5:1 on the wood)
            UiKit.Stretch(bg.rectTransform);

            float btn = Mathf.Ceil(UiKit.MinTouchPx);   // the smallest legal touch target (48 dp): tidy visuals
            float socketX = Mathf.Max(btn * 0.5f + 6f, 0.087f * w);   // the sockets sit about 8.7 percent in from each end of the art
            Button back = ToyKit.GlyphButton(bar, "Back", "Back", 2, Glyphs.Left, new Vector2(btn, btn), onBack, false, 0.54f);
            UiKit.Place((RectTransform)back.transform, new Vector2(0f, 0.5f), new Vector2(socketX - btn * 0.5f, 0f), new Vector2(btn, btn));
            Button close = ToyKit.GlyphButton(bar, "Close", "Close", 3, Glyphs.Cross, new Vector2(btn, btn), onClose, false, 0.5f);
            UiKit.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(-(socketX - btn * 0.5f), 0f), new Vector2(btn, btn));

            float titleW = w - 2f * (socketX + btn * 0.5f) - 24f;
            Text t = UiKit.AddText(bar, "Title", "Hero", 48, new Color(1f, 0.97f, 0.88f, 1f), TextAnchor.MiddleCenter, true);
            UiKit.Place((RectTransform)t.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(titleW, HeaderH - 40f));
            Fit(t, "Hero", 48, FloorPx, titleW, HeaderH - 40f, false);
        }

        // ---------------- scrolling content ----------------

        void BuildScroll(float w)
        {
            RectTransform vp = UiKit.Rect(root, "Viewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, 8f);
            vp.offsetMax = new Vector2(0f, -(HeaderH + 8f + 12f));
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            content = UiKit.Rect(vp, "Content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            float vpH = Mathf.Max(0f, height - (HeaderH + 8f + 12f) - 8f);
            Plan(w, vpH);
            float h = BuildContent(w);
            content.sizeDelta = new Vector2(w, h);

            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = vp;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 60f;
        }

        static RectTransform Put(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static float PortraitH(float w, float frac) { float pw = Mathf.Round(w * frac); return Mathf.Round(HeroSheetArt.NaturalHeight("portrait", pw, pw * 1.08f)); }

        /// <summary>Height of the content for the current plan values (without the top padding).</summary>
        float Measure(float w)
        {
            float y = PortraitH(w, pf) + gap + TabH + gap;
            if (tab == 0) y += 3f * cellH + 2f * 14f + gap + UseH;
            else y += 3f * rowH + 2f * 10f + 2f * PanelPad + gap + PowerH;
            return y + 24f;
        }

        /// <summary>
        /// Shares the spare height of the viewport out so the screen is balanced and not top-heavy: first a little more portrait, then taller stat cells (or detail rows),
        /// then larger gaps, and the rest as space above the first block. When nothing is spare (short screen, 130 percent text) the base sizes stay and the content scrolls.
        /// </summary>
        void Plan(float w, float vpH)
        {
            pf = PortraitFrac; gap = BaseGap; cellH = BaseStatCellH; rowH = BaseDerivedRowH; topPad = 0f;
            float extra = vpH - Measure(w);
            if (extra <= 0f) return;

            float ph0 = PortraitH(w, pf);
            float asp = ph0 / Mathf.Max(1f, Mathf.Round(w * pf));
            float wantPh = ph0 + extra * 0.4f;
            pf = Mathf.Min(PortraitFracMax, wantPh / asp / w);
            extra = vpH - Measure(w);
            if (extra <= 0f) return;

            float d = Mathf.Min(extra * 0.5f / 3f, 44f);
            cellH += d; rowH += Mathf.Min(d, 30f);
            extra = vpH - Measure(w);
            if (extra <= 0f) return;

            gap += Mathf.Min(extra * 0.6f / 3f, 40f);
            extra = vpH - Measure(w);
            if (extra <= 0f) return;

            topPad = Mathf.Min(extra * 0.4f, 140f);
        }

        /// <summary>Lays out portrait block, tabs and the tab content from the top; returns the content height.</summary>
        float BuildContent(float w)
        {
            float y = topPad;
            y = BuildPortraitBlock(w, y) + gap;
            y = BuildTabs(w, y) + gap;
            y = tab == 0 ? BuildStats(w, y) : BuildDetails(w, y);
            return y + 24f;
        }

        float BuildPortraitBlock(float w, float y)
        {
            float pw = Mathf.Round(w * pf);
            float ph = PortraitH(w, pf);
            RectTransform frame = UiKit.Rect(content, "Portrait");
            Put(frame, 0f, y, pw, ph);
            Image fi = UiKit.AddImage(frame, "Fill", null, Color.white);
            HeroSheetArt.Skin(fi, "portrait", HeroSheetArt.Cream, ph);
            UiKit.Stretch(fi.rectTransform);
            RectTransform doll = MomoDoll.Build(frame, "Hero", ph * 0.74f, who, art);
            UiKit.Place(doll, new Vector2(0.5f, 0.5f), Vector2.zero, doll.sizeDelta);

            float rx = pw + 16f, rw = w - rx;
            // the plates are drawn a little lower than their natural height so the text on them stays small and sleek
            float nh = Mathf.Round(HeroSheetArt.NaturalHeight("name", rw, 112f) * 0.82f);
            float bh = Mathf.Round(HeroSheetArt.NaturalHeight("ribbon", rw, 120f) * 0.82f);
            float gh = Mathf.Round(HeroSheetArt.NaturalHeight("region", rw, 112f) * 0.82f);
            float space = Mathf.Max(8f, (ph - nh - bh - gh) / 2f);
            float top = Mathf.Max(0f, (ph - (nh + bh + gh + 2f * space)) / 2f);

            // name plate
            float ny = y + top;
            Image np = UiKit.AddImage(content, "NamePlate", null, Color.white);
            HeroSheetArt.Skin(np, "name", HeroSheetArt.Cream, nh);
            Put(np.rectTransform, rx, ny, rw, nh);
            PlateText(np.transform, "HeroName", data.Name, 46, rw, nh, TextAnchor.MiddleCenter, true, UiKit.PanelInk, 30f, 12f);

            // rarity banner: grey ribbon tinted by rarity, with the word (never colour alone)
            float by = ny + nh + space;
            RectTransform banner = UiKit.Rect(content, "RarityBanner");
            Put(banner, rx, by, rw, bh);
            Image ri = UiKit.AddImage(banner, "Ribbon", null, Color.white);
            HeroSheetArt.Skin(ri, "ribbon_gray", HeroSheetArt.Gold, bh);
            ri.color = HeroSheetArt.RarityTint(data.Rarity);
            UiKit.Stretch(ri.rectTransform);
            float bandIn = rw * 0.20f;   // the tails take about a fifth of the width on each side
            Text rt = UiKit.AddText(banner, "Rarity", data.RarityName, 44, HeroSheetArt.RibbonTextColor(data.Rarity), TextAnchor.MiddleCenter, true);
            Color rc = HeroSheetArt.RibbonTextColor(data.Rarity);
            if (rc == UiKit.PanelInk) UiKit.OnPanel(rt);
            // centred on the front band of the ribbon: box centre = band centre, text anchored middle, no extra offset
            float bandH = bh * 0.56f;
            Put(rt.rectTransform, bandIn, bh * HeroSheetArt.RibbonBandCenter - bandH * 0.5f, rw - 2f * bandIn, bandH);
            Fit(rt, data.RarityName, 44, FloorPx, rw - 2f * bandIn, bh * 0.56f, false);

            // region plate
            float gy = by + bh + space;
            Image gp = UiKit.AddImage(content, "RegionPlate", null, Color.white);
            HeroSheetArt.Skin(gp, "region", HeroSheetArt.Cream, gh);
            Put(gp.rectTransform, rx, gy, rw, gh);
            PlateText(gp.transform, "Region", data.Region, 42, rw, gh, TextAnchor.MiddleCenter, true, UiKit.PanelInk, 30f, 12f);

            frame.gameObject.AddComponent<A11yLabel>().Text = data.Name + ", " + data.RarityName + ", " + data.Region;
            return y + ph;
        }

        float BuildTabs(float w, float y)
        {
            const float th = TabH;   // at least 48 dp (UiKit.MinTouchPx about 126 px)
            float tw = (w - 16f) / 2f;
            string[] names = { "Stats", "Details" };
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                bool on = tab == i;
                Button b = UiKit.AddButton(content, "Tab_" + names[i], null, null, Color.white, new Vector2(tw, th), () => SelectTab(index));
                Image img = b.GetComponent<Image>();
                HeroSheetArt.Skin(img, on ? "gold" : (i == 0 ? "tab_a" : "tab_b"), on ? HeroSheetArt.Gold : HeroSheetArt.Wood, th);
                if (!on) img.color = new Color(0.78f, 0.78f, 0.78f, 1f);   // darker plate under white text (4.5:1)
                img.raycastTarget = true;
                b.transition = Selectable.Transition.None;
                Put((RectTransform)b.transform, i * (tw + 16f), y, tw, th);
                b.GetComponent<A11yLabel>().Text = names[i] + (on ? ", selected" : "");
                Text t = UiKit.AddText(b.transform, "Label", names[i], 44, on ? UiKit.PanelInk : Color.white, TextAnchor.MiddleCenter, true);
                if (on) UiKit.OnPanel(t);
                Put(t.rectTransform, 24f, 10f, tw - 48f, th - 20f);
                Fit(t, names[i], 44, FloorPx, tw - 48f, th - 20f, false);
            }
            return y + th;
        }

        // ---------------- Stats tab ----------------

        static Sprite StatIconSprite(int i)
        {
            switch (i)
            {
                case 0: return Glyphs.Up;
                case 1: return RewardArt.Icon("bolt");
                case 2: return RewardArt.Icon("shield");
                case 3: return RewardArt.Icon("wall");
                case 4: return RewardArt.Icon("coinbag");
                default: return RewardArt.Icon("gem");
            }
        }

        float BuildStats(float w, float y)
        {
            const float colGap = 14f, rowGap = 14f, labelH = 48f, valueH = 58f;
            float cw = (w - colGap) / 2f;
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 2) * (cw + colGap);
                float cy = y + (i / 2) * (cellH + rowGap);
                Image sq = UiKit.AddImage(content, "StatIconPlate" + i, null, Color.white);
                HeroSheetArt.Skin(sq, "stat_icon", HeroSheetArt.Gold, StatIcon);
                Put(sq.rectTransform, x, cy + (cellH - StatIcon) * 0.5f, StatIcon, StatIcon);
                Sprite sp = StatIconSprite(i);
                if (sp != null)
                {
                    Image ic = UiKit.AddImage(sq.transform, "Icon", sp, i == 0 ? UiKit.PanelInk : Color.white);
                    UiKit.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(StatIcon * 0.52f, StatIcon * 0.52f));
                }
                float vx = x + StatIcon + 8f, vw = cw - StatIcon - 8f;
                Image vp = UiKit.AddImage(content, "StatValuePlate" + i, null, Color.white);
                HeroSheetArt.Skin(vp, "stat_value", HeroSheetArt.Cream, cellH);
                Put(vp.rectTransform, vx, cy, vw, cellH);
                // label above value, both centred in the plate with padding from its border
                const float padX = 34f;
                float inner = vw - 2f * padX;
                float top = (cellH - labelH - valueH) * 0.5f;
                Text label = UiKit.OnPanel(UiKit.AddText(vp.transform, "Label", data.Stats[i, 0], FloorPx, UiKit.PanelInk, TextAnchor.MiddleCenter));
                Put(label.rectTransform, padX, top, inner, labelH);
                Fit(label, data.Stats[i, 0], FloorPx, FloorPx, inner, labelH, false);
                Text value = UiKit.OnPanel(UiKit.AddText(vp.transform, "Value", data.Stats[i, 1], 48, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
                Put(value.rectTransform, padX, top + labelH, inner, valueH);
                Fit(value, data.Stats[i, 1], 48, FloorPx, inner, valueH, false);
                vp.gameObject.AddComponent<A11yLabel>().Text = data.Stats[i, 0] + " " + data.Stats[i, 1];
            }
            y += 3f * cellH + 2f * rowGap + gap;

            // use this hero
            Vector2 bs = new Vector2(Mathf.Min(560f, w - 40f), UseH);
            string label2 = inUse ? "In use" : "Use this hero";
            Button use = UiKit.AddButton(content, "UseHero", label2, ToyKit.Button(1), Color.white, bs, inUse ? null : onUse, 46);
            Put((RectTransform)use.transform, (w - bs.x) * 0.5f, y, bs.x, bs.y);
            if (inUse) use.interactable = false;
            use.GetComponent<A11yLabel>().Text = inUse ? data.Name + " is the chosen hero" : "Use " + data.Name;
            Text ut = use.GetComponentInChildren<Text>();
            if (ut != null) Fit(ut, label2, 46, FloorPx, bs.x - 60f, bs.y - 30f, false);
            return y + bs.y;
        }

        // ---------------- Details tab ----------------

        float BuildDetails(float w, float y)
        {
            // derived numbers panel with three rows
            float rowsH = 3f * rowH + 2f * 10f;
            float panelH = rowsH + 2f * PanelPad;
            Image pn = UiKit.AddImage(content, "DerivedPanel", null, Color.white);
            HeroSheetArt.Skin(pn, "panel", new Color(0.72f, 0.40f, 0.18f, 1f), HeroSheetArt.NaturalHeight("panel", w, 420f));
            Put(pn.rectTransform, 0f, y, w, panelH);
            float rx = 62f, rw = w - 2f * rx;
            for (int i = 0; i < 3; i++)
            {
                float ry = PanelPad + i * (rowH + 10f);
                Image rp = UiKit.AddImage(pn.transform, "DerivedRow" + i, null, Color.white);
                HeroSheetArt.Skin(rp, "stat_value", HeroSheetArt.Cream, rowH);
                Put(rp.rectTransform, rx, ry, rw, rowH);
                const float padX = 30f;
                float labW = (rw - 2f * padX) * 0.62f, valW = (rw - 2f * padX) - labW;
                Text lab = UiKit.OnPanel(UiKit.AddText(rp.transform, "Label", data.Derived[i, 0], FloorPx, UiKit.PanelInk, TextAnchor.MiddleCenter));
                Put(lab.rectTransform, padX, 8f, labW, rowH - 16f);
                Fit(lab, data.Derived[i, 0], FloorPx, FloorPx, labW, rowH - 16f, false);
                Text val = UiKit.OnPanel(UiKit.AddText(rp.transform, "Value", data.Derived[i, 1], 46, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
                Put(val.rectTransform, padX + labW, 8f, valW, rowH - 16f);
                Fit(val, data.Derived[i, 1], 46, FloorPx, valW, rowH - 16f, false);
                rp.gameObject.AddComponent<A11yLabel>().Text = data.Derived[i, 0] + " " + data.Derived[i, 1];
            }
            y += panelH + gap;

            // hero power panel (the gold plate)
            Image pw = UiKit.AddImage(content, "PowerPanel", null, Color.white);
            HeroSheetArt.Skin(pw, "gold", HeroSheetArt.Gold, 130f);
            Put(pw.rectTransform, 0f, y, w, PowerH);
            Text head = UiKit.OnPanel(UiKit.AddText(pw.transform, "Heading", "Hero power", 46, UiKit.PanelInk, TextAnchor.MiddleCenter, true));
            Put(head.rectTransform, 40f, 28f, w - 80f, 60f);
            Fit(head, "Hero power", 46, FloorPx, w - 80f, 60f, false);
            Text body = UiKit.OnPanel(UiKit.AddText(pw.transform, "PowerText", data.PowerText, 42, UiKit.PanelInk, TextAnchor.MiddleCenter));
            Put(body.rectTransform, 56f, 96f, w - 112f, PowerH - 96f - 36f);
            Fit(body, data.PowerText, 42, FloorPx, w - 112f, PowerH - 96f - 36f, true);
            pw.gameObject.AddComponent<A11yLabel>().Text = "Hero power. " + data.PowerText;
            return y + PowerH;
        }

        // ---------------- text helpers ----------------

        /// <summary>Text on a plate: ink on the cream, best fit from <paramref name="maxPx"/> down to the 16 sp floor inside the plate minus the insets.</summary>
        static Text PlateText(Transform plate, string name, string s, int maxPx, float plateW, float plateH, TextAnchor align, bool display, Color ink, float insetX, float insetY)
        {
            Text t = UiKit.AddText(plate, name, s, maxPx, ink, align, display);
            UiKit.OnPanel(t);
            Put(t.rectTransform, insetX, insetY, plateW - 2f * insetX, plateH - 2f * insetY);
            Fit(t, s, maxPx, FloorPx, plateW - 2f * insetX, plateH - 2f * insetY, false);
            return t;
        }

        /// <summary>
        /// Best fit: the largest font size (the base size times the accessibility text scale, never below the 16 sp floor) at which the text fits the box.
        /// One line (Overflow, no wrap) unless <paramref name="wrap"/>; at the floor it stays at the floor.
        /// </summary>
        static void Fit(Text t, string s, int basePx, int minPx, float w, float h, bool wrap)
        {
            t.text = s;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = false;
            int max = Mathf.Max(minPx, Mathf.RoundToInt(basePx * UiKit.TextScale));
            int size = max;
            for (; size > minPx; size -= 2)
            {
                t.fontSize = size;
                bool ok = t.preferredHeight <= h + 0.5f && (wrap || t.preferredWidth <= w + 0.5f);
                if (ok) break;
            }
            t.fontSize = Mathf.Max(size, minPx);
            Outline o = t.GetComponent<Outline>();
            if (o != null && o.enabled)
            {
                float d = Mathf.Max(1.6f, t.fontSize * 0.024f);
                o.effectDistance = new Vector2(d, -d);
            }
        }
    }
}
