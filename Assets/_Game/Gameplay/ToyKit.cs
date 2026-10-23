using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The chunky toy-plate UI kit (docs/16, picked by the owner 2026-10-23): painted buttons, panels, pill, badge, tab bar and logo
    /// from Resources/Art/Ui. Every part is 9-sliced with borders that hold the painted corners, so only a flat middle ever stretches.
    /// </summary>
    public static class ToyKit
    {
        public const float TabBarHeight = 190f, TabBarBottom = 16f;
        /// <summary>Space a screen leaves at the bottom of the safe area for the tab bar.</summary>
        public const float TabBarReserve = TabBarHeight + TabBarBottom + 24f;

        public static readonly string[] TabLabels = { "Shop", "Gear", "PLAY", "Heroes", "Daily" };
        public static readonly string[] TabA11y = { "Shop", "Gear", "Play", "Characters", "Daily rewards" };
        public const int TabShop = 0, TabGear = 1, TabPlay = 2, TabHeroes = 3, TabDaily = 4;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            Sprite s;
            if (cache.TryGetValue(name, out s) && s != null) return s;
            s = Resources.Load<Sprite>("Art/Ui/" + name);
            cache[name] = s;
            return s;
        }

        public static bool IsKitSprite(Sprite s)
        {
            if (s == null) return false;
            string n = s.name;
            return n.StartsWith("btn_") || n.StartsWith("panel_") || n == "pill_dark" || n == "tabbar" || n == "badge_red" || n == "ribbon_gold" || n == "logo" || n == "shop_scene" || n.StartsWith("plate_") || n.StartsWith("glyph_") || n.StartsWith("pw_");
        }

        /// <summary>Button art by role: 0 secondary (yellow), 1 main action (green), 2 back (blue), 3 danger (red).</summary>
        public static Sprite Button(int role)
        {
            switch (role)
            {
                case 1: return Get("btn_green");
                case 2: return Get("btn_blue");
                case 3: return Get("btn_red");
                default: return Get("btn_yellow");
            }
        }

        /// <summary>A 9-sliced kit image whose painted corners are drawn about <paramref name="corner"/> canvas units wide.</summary>
        public static Image Sliced(Transform parent, string name, Sprite sprite, float corner)
        {
            Image img = UiKit.AddImage(parent, name, sprite, Color.white);
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            if (sprite != null)
            {
                float border = Mathf.Max(sprite.border.x, sprite.border.y, sprite.border.z, sprite.border.w);
                img.pixelsPerUnitMultiplier = Mathf.Max(0.2f, border / Mathf.Max(1f, corner));
            }
            return img;
        }

        public static Image Panel(Transform parent, string name, float corner, bool blue = false)
        {
            return Sliced(parent, name, Get(blue ? "panel_blue" : "panel_cream"), corner);
        }

        static Sprite emptyStar;

        /// <summary>Points the catalog's currency and star icons at the new kit icons (chunky toy-plate style) so every screen shares them.</summary>
        public static void ApplyTo(ArtCatalog art)
        {
            if (art == null) return;
            Sprite coin = Get("icon_coin"), gem = Get("icon_gem"), star = Get("icon_star");
            if (coin != null) art.CoinIcon = coin;
            if (gem != null) art.GemIcon = gem;
            if (star != null)
            {
                art.StarFilled = star;
                art.StarEmpty = EmptyStar(star) ?? art.StarEmpty;
            }
        }

        /// <summary>A dark, flat copy of the star for stars not yet earned.</summary>
        static Sprite EmptyStar(Sprite star)
        {
            if (emptyStar != null) return emptyStar;
            try
            {
                Texture2D src = star.texture;
                var px = src.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    byte a = px[i].a;
                    byte v = (byte)(px[i].r / 5 + 40);
                    px[i] = new Color32(v, (byte)(v + 4), (byte)(v + 14), (byte)(a * 0.85f));
                }
                var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(px);
                tex.Apply();
                emptyStar = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), star.pixelsPerUnit);
            }
            catch (Exception) { emptyStar = null; }   // not readable: the catalog keeps its own empty star
            return emptyStar;
        }

        /// <summary>A white glyph (chevron, pause bars, cross) centred on a button, about <paramref name="fraction"/> of its size.</summary>
        public static Image AddGlyph(Transform button, Sprite glyph, float fraction = 0.62f, bool flipX = false)
        {
            Image g = UiKit.AddImage(button, "Glyph", glyph, Color.white);
            RectTransform rt = g.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var parent = (RectTransform)button;
            float s = Mathf.Min(parent.sizeDelta.x, parent.sizeDelta.y) * fraction;
            rt.sizeDelta = new Vector2(s, s);
            rt.anchoredPosition = Vector2.zero;
            if (flipX) rt.localScale = new Vector3(-1f, 1f, 1f);
            return g;
        }

        public static Button GlyphButton(Transform parent, string name, string a11y, int role, Sprite glyph, Vector2 size, Action onClick, bool flipX = false, float fraction = 0.62f)
        {
            Button b = UiKit.AddButton(parent, name, "", Button(role), Color.white, size, onClick);
            b.GetComponent<A11yLabel>().Text = a11y;
            b.GetComponent<Image>().raycastTarget = true;
            AddGlyph(b.transform, glyph, fraction, flipX);
            return b;
        }

        /// <summary>One slot of the bottom icon bar.</summary>
        public sealed class BarItem
        {
            public string Icon;          // sprite name under Resources/Art/Ui
            public string Caption;       // tiny caption, drawn only on the selected tab
            public string A11y;
            public bool Raised;          // PLAY: bigger, green, lifted above the row
            public bool Badge;           // red notification badge
        }

        /// <summary>
        /// The bottom bar of SS2/SS3 (owner issue 14): a row of separate square icon buttons. The selected one is yellow with a tiny caption, a raised
        /// item (PLAY) is green and bigger, the others are blue plates with an icon only. Each is a real button of at least 48 dp. Placed at the bottom
        /// of <paramref name="screen"/>; the buttons are anchored by fractions of the width so they fit every aspect.
        /// </summary>
        public static RectTransform BuildIconBar(RectTransform screen, BarItem[] items, int selected, Action<int> onSelect)
        {
            RectTransform bar = UiKit.Rect(screen, "TabBar");
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, TabBarBottom);
            bar.sizeDelta = new Vector2(-24f, TabBarHeight);
            int n = items.Length;
            const float gap = 12f;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                BarItem it = items[i];
                bool on = i == selected;
                int role = it.Raised ? 1 : (on ? 0 : 2);
                Button b = UiKit.AddButton(bar, "Tab" + it.A11y.Replace(" ", ""), "", ToyKit.Button(role), Color.white, new Vector2(160f, 150f), () => onSelect(idx));
                b.GetComponent<A11yLabel>().Text = it.A11y + " tab" + (on ? ", selected" : "");
                b.GetComponent<Image>().raycastTarget = true;
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i / (float)n, 0f);
                rt.anchorMax = new Vector2((i + 1) / (float)n, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                float lift = it.Raised ? 26f : 0f;
                rt.offsetMin = new Vector2(gap * 0.5f - (it.Raised ? 8f : 0f), 14f);
                rt.offsetMax = new Vector2(-gap * 0.5f + (it.Raised ? 8f : 0f), 14f + 158f + lift);
                bool caption = on || it.Raised;
                Image ic = UiKit.AddImage(b.transform, "Icon", Get(it.Icon), Color.white);
                var irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, it.Raised ? 0.7f : (caption ? 0.66f : 0.5f));
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                float isz = it.Raised ? 80f : (caption ? 66f : 90f);
                irt.sizeDelta = new Vector2(isz, isz);
                if (caption && !string.IsNullOrEmpty(it.Caption))
                {
                    Text cap = UiKit.AddText(b.transform, "Caption", it.Caption, 42, UiKit.ButtonInk, TextAnchor.LowerCenter, true);
                    DropOutline(cap);
                    cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var crt = (RectTransform)cap.transform;
                    crt.anchorMin = new Vector2(0f, 0f);
                    crt.anchorMax = new Vector2(1f, 0f);
                    crt.pivot = new Vector2(0.5f, 0f);
                    crt.offsetMin = new Vector2(0f, 12f);
                    crt.offsetMax = new Vector2(0f, 68f);
                }
                if (it.Badge) AddBadge(b.transform);
            }
            return bar;
        }

        /// <summary>
        /// A popup action. Simple words become round icon buttons (owner issue 14): PLAY and START a green play icon, OK and Confirm a green tick,
        /// Close and Cancel a red cross. Anything with a price or a longer sentence stays a wide kit button so the amount is readable.
        /// </summary>
        public static Button PopupAction(Transform panel, string label, Sprite sprite, Color tint, float y, Action action, float x = 0f)
        {
            Sprite icon = null;
            int role = 1;
            float size = 170f;
            switch (label)
            {
                case "PLAY": case "START": icon = Get("icon_play"); size = 150f; break;
                case "OK": case "Confirm": icon = Glyphs.Check; size = 130f; break;
                case "Close": case "Cancel": icon = Glyphs.Cross; role = 3; size = 130f; break;
            }
            if (icon != null)
            {
                Button ib = IconButton(panel, label, label, role, icon, new Vector2(size, size), action, label == "PLAY" || label == "START" ? 0.6f : 0.56f);
                UiKit.Place((RectTransform)ib.transform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(size, size));
                return ib;
            }
            Button b = UiKit.AddButton(panel, label, label, sprite, tint, new Vector2(560f, 130f), action, 46);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(560f, 130f));
            return b;
        }

        /// <summary>A small red notification badge in the top-right corner of a button.</summary>
        public static Image AddBadge(Transform button)
        {
            Image bd = UiKit.AddImage(button, "Badge", Get("badge_red"), Color.white);
            var rt = bd.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            rt.sizeDelta = new Vector2(52f, 58f);
            return bd;
        }

        /// <summary>A round or square plate button carrying a painted icon (settings gear, etc.).</summary>
        public static Button IconButton(Transform parent, string name, string a11y, int role, Sprite icon, Vector2 size, Action onClick, float fraction = 0.62f)
        {
            Button b = UiKit.AddButton(parent, name, "", Button(role), Color.white, size, onClick);
            b.GetComponent<A11yLabel>().Text = a11y;
            b.GetComponent<Image>().raycastTarget = true;
            AddGlyph(b.transform, icon, fraction);
            return b;
        }

        /// <summary>A dark pill with a painted icon and a right-aligned number (wallet and HUD pills). Returns the value text.</summary>
        public static Text WalletPill(Transform parent, string name, Sprite icon, Vector2 anchor, Vector2 pos, Vector2 size, Color numberColor)
        {
            Image pill = Theme.GlassPanel(parent, name, size.y * 0.25f);
            UiKit.Place(pill.rectTransform, anchor, pos, size);
            Image ic = UiKit.AddImage(pill.transform, "Icon", icon, Color.white);
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(size.y * 0.28f, 0f);
            irt.sizeDelta = new Vector2(size.y * 0.78f, size.y * 0.78f);
            Text t = UiKit.AddText(pill.transform, "Value", "0", 46, numberColor, TextAnchor.MiddleRight, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(size.y * 1.1f, 0f);
            trt.offsetMax = new Vector2(-size.y * 0.35f, 0f);
            return t;
        }

        /// <summary>A centred row of icon pills on a popup, <paramref name="y"/> below the top edge of <paramref name="panel"/>.</summary>
        public static RectTransform PillRow(Transform panel, string name, float y, float height)
        {
            RectTransform row = UiKit.Rect(panel, name);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -y);
            row.sizeDelta = new Vector2(-48f, height);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            return row;
        }

        /// <summary>A dark pill with a painted icon and a bold number (result and goal details instead of sentences). Returns the value text.</summary>
        public static Text InfoPill(RectTransform row, Sprite icon, string text, Color color, float width, float height)
        {
            Image pill = Theme.GlassPanel(row, "Pill", height * 0.25f);
            var le = pill.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            le.preferredHeight = height;
            le.flexibleWidth = 0f;
            Image ic = UiKit.AddImage(pill.transform, "Icon", icon, Color.white);
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(height * 0.18f, 0f);
            irt.sizeDelta = new Vector2(height * 0.74f, height * 0.74f);
            Text t = UiKit.AddText(pill.transform, "Value", text, 50, color, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(height * 0.95f, 0f);
            trt.offsetMax = new Vector2(-height * 0.18f, 0f);
            return t;
        }

        /// <summary>A wallet pill that fills its slot in a layout row (no fixed placement). Returns the value text.</summary>
        public static Text WalletPillFlex(Transform parent, string name, Sprite icon, Color numberColor, float height)
        {
            Image pill = Theme.GlassPanel(parent, name, height * 0.25f);
            var le = pill.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.flexibleWidth = 1f;
            Image ic = UiKit.AddImage(pill.transform, "Icon", icon, Color.white);
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(height * 0.2f, 0f);
            irt.sizeDelta = new Vector2(height * 0.7f, height * 0.7f);
            Text t = UiKit.AddText(pill.transform, "Value", "0", 46, numberColor, TextAnchor.MiddleRight, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(height * 0.95f, 0f);
            trt.offsetMax = new Vector2(-height * 0.25f, 0f);
            return t;
        }

        /// <summary>The gold ribbon banner with a title in ink (section titles like SS2/SS3).</summary>
        public static Text Ribbon(Transform parent, string title, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Sprite r = Get("ribbon_gold");
            Image img = UiKit.AddImage(parent, "Ribbon", r, Color.white);
            img.preserveAspect = true;
            UiKit.Place(img.rectTransform, anchor, pos, size);
            Text t = UiKit.AddText(img.transform, "Title", title, 64, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
            DropOutline(t);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = new Vector2(0.22f, 0.5f);
            trt.anchorMax = new Vector2(0.78f, 0.9f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            return t;
        }

        static void DropOutline(Text t)
        {
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }
    }
}
