using System;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The shared look of every shop page (docs/26 section 1, reference screenshots SS11 to SS15): the Momo shopkeeper scene across the top on a
    /// brownish wood background, a ribbon title, framed cards, and the bottom tab bar Bundles | Chests | Power-ups | Coins | Gems (the selected tab is
    /// yellow, the others are blue icon-only, a tab can carry a red dot). Menu screens only: everything is built once in code,
    /// nothing runs per frame. All touch targets are at least 48 dp; every position is an anchor or a fraction of the width, so it fits 9:16, 9:19.5,
    /// 9:21 and 3:4, the safe area and 130 percent text.
    ///
    /// STABLE API (other shop pages build on it):
    ///   ShopChrome.BuildScene(screen, art, progress, out coinText, out gemText, out starText)   once per screen: wood background, scene, wallet pills.
    ///   ShopChrome.BuildBody(screen, ribbonTitle, RibbonColor)   returns the CONTENT RECT (between ribbon and tab bar, side margins applied).
    ///                                                            To remove a page: Object.Destroy(content.parent.gameObject).
    ///   ShopChrome.ScrollArea(content, contentHeight)   a vertical scroll view filling the content rect; returns the top-anchored list rect.
    ///   ShopChrome.Card(parent, name, FrameColor)   a framed rounded card (Blue, Gold, Green); the caller anchors the returned rect and adds children.
    ///   ShopChrome.Ribbon(parent, title, RibbonColor, anchor, pos, size)   yellow, red or blue ribbon with a title.
    ///   ShopChrome.PriceButton(parent, name, role, icon, text, a11y, onClick)   kit button (role 1 green, 0 yellow, 2 blue, 3 red) with an optional icon.
    ///   ShopChrome.IconPile(area, icon, tier01)   code-drawn pile of icons that grows with tier01 (fallback for the gem and coin pile pictures).
    ///   ShopChrome.BuildTabBar(screen, selected, onSelect, bool[] dots)   the 5-tab bar; dots may be null; returns the bar root (destroy it to rebuild).
    ///   Layout constants: ContentTop (px below the safe-area top where the content starts), ContentBottom (tab bar reserve), SideMargin.
    /// </summary>
    public static class ShopChrome
    {
        public enum RibbonColor { Yellow, Red, Blue }
        public enum FrameColor { Blue, Gold, Green }

        public const int TabBundles = 0, TabChests = 1, TabPowerups = 2, TabCoins = 3, TabGems = 4, TabCount = 5;
        /// <summary>Page titles by tab (ribbon text).</summary>
        public static readonly string[] TabNames = { "Bundles", "Chests", "Power Ups", "Coins", "Gems" };
        /// <summary>Short captions of the selected tab.</summary>
        static readonly string[] TabIcons = { "icon_bundle", "icon_chest", "icon_bolt", "icon_coin", "icon_gem" };

        public const float SceneHeight = 440f, RibbonW = 620f, RibbonH = 170f, SideMargin = 28f;
        /// <summary>Pixels from the top of the safe area to the first content row (below the ribbon).</summary>
        public const float ContentTop = SceneHeight + 124f;
        /// <summary>Space kept free at the bottom for the tab bar.</summary>
        public const float ContentBottom = ToyKit.TabBarReserve;
        public const float PriceButtonH = 130f;

        // ---------------- helpers ----------------

        public static string Num(int n) { return n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>
        /// THE one place a real-money price becomes text ("$0.79"). Replace its body with the Google Play localized price once billing exists.
        /// </summary>
        public static string PriceText(double usd) { return ShopCatalog.FormatUsd(usd); }

        /// <summary>A flat rounded rectangle (9-sliced, a decorative surface the layout audit ignores).</summary>
        static Image Round(Transform parent, string name, Color c, float corner)
        {
            Image img = ToyKit.Sliced(parent, name, Theme.RoundSprite, corner);
            img.color = c;
            return img;
        }

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint n = (uint)(a * 374761393 + b * 668265263);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }

        static Sprite wood;

        /// <summary>The warm brown plank background, drawn once in code (a 64 x 256 texture stretched over the screen).</summary>
        public static Sprite WoodSprite
        {
            get
            {
                if (wood != null) return wood;
                const int w = 64, h = 256, plank = 32;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float t = y / (h - 1f);
                    Color baseC = Color.Lerp(new Color(0.24f, 0.12f, 0.07f), new Color(0.34f, 0.18f, 0.10f), t);
                    int p = y / plank;
                    float tone = (Hash(p, 7) - 0.5f) * 0.05f;
                    bool seam = y % plank < 2;
                    for (int x = 0; x < w; x++)
                    {
                        float grain = (Hash(x / 3, y) - 0.5f) * 0.03f + (Hash(y, 3) - 0.5f) * 0.015f;
                        float k = 1f + tone + grain - (seam ? 0.28f : 0f);
                        px[y * w + x] = new Color(baseC.r * k, baseC.g * k, baseC.b * k, 1f);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                wood = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                return wood;
            }
        }

        public static int TotalStars(ProgressService progress)
        {
            int stars = 0;
            for (int i = 1; i <= GameSession.PlayableRegions; i++)
            {
                RegionDef r = GameSession.Repo != null ? GameSession.Repo.TryGetRegionByIndex(i) : null;
                if (r != null) stars += progress.StarsInRegion(r);
            }
            return stars;
        }

        // ---------------- scene and wallet ----------------

        /// <summary>
        /// The wood background (behind everything), the Momo shopkeeper scene across the top (cover-fit, never stretched) and the wallet pills over it
        /// (coins, gems, stars, left to right; the screen's back button sits at the top left). Call once per screen, before BuildBody.
        /// </summary>
        public static void BuildScene(RectTransform screen, ArtCatalog art, ProgressService progress, out Text coinText, out Text gemText, out Text starText)
        {
            Image bg = UiKit.AddImage(screen, "Bg", WoodSprite, Color.white);
            bg.preserveAspect = false;
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = new Vector2(-400f, -400f);   // reaches under notches and rounded corners
            bg.rectTransform.offsetMax = new Vector2(400f, 400f);

            const float over = 250f;   // the scene reaches above the safe area so no notch shows a gap
            RectTransform scene = UiKit.Rect(screen, "Scene");
            scene.anchorMin = new Vector2(0f, 1f);
            scene.anchorMax = new Vector2(1f, 1f);
            scene.pivot = new Vector2(0.5f, 1f);
            scene.anchoredPosition = new Vector2(0f, over);
            scene.sizeDelta = new Vector2(0f, SceneHeight + over);
            scene.gameObject.AddComponent<RectMask2D>();
            Sprite sc = ToyKit.Get("shop_scene");
            Image si = UiKit.AddImage(scene, "SceneArt", sc, sc != null ? Color.white : new Color(0.45f, 0.28f, 0.16f, 1f));
            si.preserveAspect = false;
            UiKit.Stretch(si.rectTransform);
            if (sc != null)
            {
                var fit = si.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = sc.rect.width / sc.rect.height;
            }
            Image edge = UiKit.AddImage(scene, "Fill", null, new Color(0.17f, 0.08f, 0.04f, 0.9f));
            edge.rectTransform.anchorMin = new Vector2(0f, 0f);
            edge.rectTransform.anchorMax = new Vector2(1f, 0f);
            edge.rectTransform.pivot = new Vector2(0.5f, 0f);
            edge.rectTransform.anchoredPosition = Vector2.zero;
            edge.rectTransform.sizeDelta = new Vector2(0f, 10f);

            RectTransform row = UiKit.Rect(screen, "Wallet");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(190f, -118f);
            row.offsetMax = new Vector2(-16f, -18f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            coinText = ToyKit.WalletPillFlex(row, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, 100f);
            gemText = ToyKit.WalletPillFlex(row, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), 100f);
            starText = ToyKit.WalletPillFlex(row, "StarsPill", art != null ? art.StarFilled : null, new Color(1f, 0.92f, 0.5f), 100f);
            if (progress != null)
            {
                UiKit.SetText(coinText, progress.Coins.ToString());
                UiKit.SetText(gemText, progress.Gems.ToString());
                UiKit.SetText(starText, TotalStars(progress).ToString());
            }
        }

        // ---------------- body, ribbon, cards ----------------

        /// <summary>
        /// The page body: a ribbon title under the scene and the content rect below it (side margins, bottom reserve for the tab bar). Returns the content
        /// rect; its parent ("ShopBody") also holds the ribbon, destroy that parent to remove the page.
        /// </summary>
        public static RectTransform BuildBody(RectTransform screen, string ribbonTitle, RibbonColor color)
        {
            RectTransform root = UiKit.Rect(screen, "ShopBody");
            UiKit.Stretch(root);
            Ribbon(root, ribbonTitle, color, new Vector2(0.5f, 1f), new Vector2(0f, -(SceneHeight - 60f)), new Vector2(RibbonW, RibbonH));
            RectTransform content = UiKit.Rect(root, "Content");
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(SideMargin, ContentBottom);
            content.offsetMax = new Vector2(-SideMargin, -ContentTop);
            return content;
        }

        /// <summary>A ribbon with a title. Yellow uses the painted kit ribbon (ink text); red and blue are drawn in code (white text).</summary>
        public static Text Ribbon(Transform parent, string title, RibbonColor color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            color = RibbonColor.Yellow;   // owner (issue list 27): every shop page has the same yellow ribbon; the parameter stays for API compatibility
            if (ToyKit.Get("ribbon_gold") != null) return StyleTitle(ToyKit.Ribbon(parent, title, anchor, pos, size));
            Color band, tail, hi, ink;
            bool darkInk = color == RibbonColor.Yellow;
            switch (color)
            {
                case RibbonColor.Red: band = new Color(0.80f, 0.15f, 0.18f, 1f); tail = new Color(0.55f, 0.08f, 0.12f, 1f); break;
                case RibbonColor.Blue: band = new Color(0.17f, 0.40f, 0.82f, 1f); tail = new Color(0.10f, 0.24f, 0.55f, 1f); break;
                default: band = new Color(1f, 0.80f, 0.22f, 1f); tail = new Color(0.78f, 0.55f, 0.08f, 1f); break;
            }
            hi = new Color(1f, 1f, 1f, 0.22f);
            ink = darkInk ? UiKit.ButtonInk : Color.white;
            RectTransform root = UiKit.Rect(parent, "Ribbon");
            UiKit.Place(root, anchor, pos, size);
            for (int s = -1; s <= 1; s += 2)
            {
                Image t = Round(root, "Fill", tail, 10f);
                UiKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(s * (size.x * 0.5f - size.y * 0.26f), -size.y * 0.06f), new Vector2(size.y * 0.62f, size.y * 0.62f));
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            Vector2 bandSize = new Vector2(size.x - size.y * 0.5f, size.y * 0.78f);
            Image b = Round(root, "Fill", band, 30f);
            UiKit.Place(b.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, bandSize);
            b.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image h = Round(root, "Fill", hi, 20f);
            UiKit.Place(h.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, bandSize.y * 0.27f), new Vector2(bandSize.x - 40f, bandSize.y * 0.3f));
            h.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text text = UiKit.AddText(root, "Title", title, 64, ink, TextAnchor.MiddleCenter, true);
            if (darkInk) { var o = text.GetComponent<Outline>(); if (o != null) o.enabled = false; }
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, bandSize);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return StyleTitle(text);
        }

        /// <summary>The page header look: Pascal Case words in italic, smaller than the kit default (still above the 16 sp floor).</summary>
        static Text StyleTitle(Text t)
        {
            t.fontStyle = FontStyle.Italic;
            t.fontSize = Mathf.RoundToInt(52f * UiKit.TextScale);
            return t;
        }

        /// <summary>The fill colour of a card frame (text colours are chosen against it by the pages).</summary>
        public static Color FillOf(FrameColor f)
        {
            switch (f)
            {
                case FrameColor.Blue: return new Color(0.09f, 0.27f, 0.58f, 1f);
                case FrameColor.Green: return new Color(0.12f, 0.45f, 0.20f, 1f);
                default: return new Color(0.98f, 0.78f, 0.30f, 1f);
            }
        }

        /// <summary>A rounded card with a coloured frame, an inner fill and a soft top highlight. The caller anchors the returned rect and adds children to it.</summary>
        public static RectTransform Card(Transform parent, string name, FrameColor frame)
        {
            Color edge;
            switch (frame)
            {
                case FrameColor.Blue: edge = new Color(0.25f, 0.56f, 0.95f, 1f); break;
                case FrameColor.Green: edge = new Color(0.30f, 0.76f, 0.30f, 1f); break;
                default: edge = new Color(0.95f, 0.60f, 0.10f, 1f); break;
            }
            RectTransform host = UiKit.Rect(parent, name);
            Image bg = Round(host, "Bg", edge, 40f);
            UiKit.Stretch(bg.rectTransform);
            Image fill = Round(host, "Fill", FillOf(frame), 32f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(9f, 9f);
            fill.rectTransform.offsetMax = new Vector2(-9f, -9f);
            Image gloss = Round(host, "Fill", new Color(1f, 1f, 1f, 0.10f), 26f);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            gloss.rectTransform.anchorMax = new Vector2(1f, 1f);
            gloss.rectTransform.offsetMin = new Vector2(14f, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-14f, -14f);
            return host;
        }

        /// <summary>A vertical scroll view that fills <paramref name="content"/>; returns the top-anchored list rect of the given height to place cards in.</summary>
        public static RectTransform ScrollArea(RectTransform content, float contentHeight)
        {
            RectTransform vp = UiKit.Rect(content, "ShopViewport");
            UiKit.Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // gives the ScrollRect something to drag on between the cards
            RectTransform list = UiKit.Rect(vp, "ShopList");
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.offsetMin = Vector2.zero;
            list.offsetMax = Vector2.zero;
            list.sizeDelta = new Vector2(0f, contentHeight);
            var scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = list;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;
            return list;
        }

        /// <summary>A kit button with an optional currency icon at the left and the text after it. The caller sets its anchors and size (at least 48 dp).</summary>
        public static Button PriceButton(Transform parent, string name, int role, Sprite icon, string text, string a11y, Action onClick, int fontSize = 40)
        {
            Button b = UiKit.AddButton(parent, name, text, ToyKit.Button(role), Color.white, new Vector2(200f, PriceButtonH), onClick, fontSize);
            b.GetComponent<A11yLabel>().Text = a11y;
            Text label = b.transform.Find("Label").GetComponent<Text>();
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (icon != null)
            {
                Image ic = UiKit.AddImage(b.transform, "Icon", icon, Color.white);
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                ic.rectTransform.pivot = new Vector2(0f, 0.5f);
                ic.rectTransform.anchoredPosition = new Vector2(14f, 0f);
                ic.rectTransform.sizeDelta = new Vector2(52f, 52f);
                var lrt = label.rectTransform;
                lrt.offsetMin = new Vector2(62f, 0f);
                lrt.offsetMax = new Vector2(-8f, 0f);
            }
            return b;
        }

        /// <summary>
        /// A pile of icons that grows with <paramref name="tier01"/> (0 = one big icon, 1 = ten smaller ones in a pyramid), centred in the 300 x 170 area.
        /// The fallback when there is no gem or coin pile picture. Decorative (no raycasts).
        /// </summary>
        public static void IconPile(RectTransform area, Sprite icon, float tier01)
        {
            tier01 = Mathf.Clamp01(tier01);
            int n = 1 + Mathf.RoundToInt(tier01 * 9f);
            int rows = 1;
            while (rows * (rows + 1) / 2 < n) rows++;
            float s = Mathf.Lerp(104f, 64f, tier01);
            float stepX = s * 0.62f, stepY = s * 0.5f;
            var counts = new int[rows];
            int rem = n;
            for (int k = rows - 1; k >= 0; k--) { counts[k] = Mathf.Min(k + 1, rem); rem -= counts[k]; }
            for (int k = 0; k < rows; k++)           // top rows first: the lower rows draw in front
            {
                int b = rows - 1 - k;
                for (int i = 0; i < counts[k]; i++)
                {
                    Image g = UiKit.AddImage(area, "Gem", icon, icon != null ? Color.white : new Color(0.6f, 0.4f, 0.9f, 1f));
                    g.raycastTarget = false;
                    RectTransform rt = g.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2((i - (counts[k] - 1) * 0.5f) * stepX, 4f + b * stepY);
                    rt.sizeDelta = new Vector2(s, s);
                    rt.localRotation = Quaternion.Euler(0f, 0f, ((b + i) % 3 - 1) * 8f);
                }
            }
        }

        // ---------------- tab bar ----------------

        /// <summary>
        /// The bottom tab bar on a blue backing. Five tabs, each a real button of at least 48 dp: the selected one is yellow, wider and shows its caption,
        /// the others are blue with an icon only. <paramref name="dots"/> (may be null) puts a red dot on a tab. Returns the bar root.
        /// </summary>
        public static RectTransform BuildTabBar(RectTransform screen, int selected, Action<int> onSelect, bool[] dots)
        {
            RectTransform root = UiKit.Rect(screen, "TabBar");
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.offsetMin = new Vector2(-100f, -300f);
            root.offsetMax = new Vector2(100f, ToyKit.TabBarReserve - 20f);   // the bar draws under the notch and the gesture area
            Image back = Round(root, "Bg", new Color(0.26f, 0.35f, 0.56f, 1f), 24f);
            UiKit.Stretch(back.rectTransform);

            RectTransform tabs = UiKit.Rect(root, "Tabs");
            tabs.anchorMin = new Vector2(0f, 0f);
            tabs.anchorMax = new Vector2(1f, 0f);
            tabs.pivot = new Vector2(0.5f, 0f);
            tabs.offsetMin = new Vector2(112f, 300f + 18f);
            tabs.offsetMax = new Vector2(-112f, 300f + 18f + 158f);

            const float selWeight = 1.55f;
            float total = (TabCount - 1) + selWeight;
            float gap = UiKit.MinSpacingPx + 2f;   // 8 dp between neighbours
            float cum = 0f;
            for (int i = 0; i < TabCount; i++)
            {
                int idx = i;
                bool on = i == selected;
                float w = on ? selWeight : 1f;
                float x0 = cum / total, x1 = (cum + w) / total;
                cum += w;
                Button b = UiKit.AddButton(tabs, "Tab" + TabNames[i].Replace("-", ""), "", ToyKit.Button(on ? 0 : 2), Color.white, new Vector2(160f, 150f), () => { if (onSelect != null) onSelect(idx); });
                b.GetComponent<A11yLabel>().Text = TabNames[i] + " tab" + (on ? ", selected" : "");
                b.GetComponent<Image>().raycastTarget = true;
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(x0, 0f);
                rt.anchorMax = new Vector2(x1, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(i == 0 ? 0f : gap * 0.5f, 0f);
                rt.offsetMax = new Vector2(i == TabCount - 1 ? 0f : -gap * 0.5f, 0f);
                Image ic = UiKit.AddImage(b.transform, "Icon", i == TabBundles ? BundlesScreen.BagIcon(null) ?? ToyKit.Get(TabIcons[i]) : ToyKit.Get(TabIcons[i]), Color.white);   // Bundles tab = the same bag as the bundle rows
                var irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(86f, 86f);   // icons only: no caption text (the accessibility label names the tab)
                if (dots != null && i < dots.Length && dots[i]) ToyKit.AddBadge(b.transform);
            }
            return root;
        }
    }
}
