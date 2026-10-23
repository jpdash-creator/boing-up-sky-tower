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
        public const float TabBarHeight = 170f, TabBarBottom = 16f;
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
            return n.StartsWith("btn_") || n.StartsWith("panel_") || n == "pill_dark" || n == "tabbar" || n == "badge_red" || n == "ribbon_gold" || n == "logo";
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

        /// <summary>
        /// The persistent bottom tab bar: Shop, Gear, PLAY (centre, biggest), Characters, Daily. The selected tab is a yellow plate, PLAY is always a
        /// raised green plate. Tabs are real buttons of at least 48 dp. Returns the bar rect (placed at the bottom of <paramref name="screen"/>).
        /// </summary>
        public static RectTransform BuildTabBar(RectTransform screen, int selected, Action<int> onSelect)
        {
            Sprite barSprite = Get("tabbar");
            RectTransform bar = UiKit.Rect(screen, "TabBar");
            UiKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0f, TabBarBottom), new Vector2(1000f, TabBarHeight));
            bar.pivot = new Vector2(0.5f, 0f);
            if (barSprite != null)
            {
                Image back = Sliced(bar, "Back", barSprite, 56f);
                UiKit.Stretch(back.rectTransform);
            }
            else
            {
                Image back = UiKit.AddPanel(bar, "Back", Theme.Sky);
                UiKit.Stretch(back.rectTransform);
            }
            const float slotW = 184f, pad = 40f;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                int idx = i;
                bool play = i == TabPlay, on = i == selected;
                float size = play ? 180f : 130f;
                Sprite plate = play ? Button(1) : (on ? Button(0) : Theme.RoundSprite);
                Color tint = play || on ? Color.white : new Color(1f, 1f, 1f, 0f);
                Button b = UiKit.AddButton(bar, "Tab" + TabA11y[i].Replace(" ", ""), TabLabels[i], plate, tint, new Vector2(size, size), () => onSelect(idx), play ? 48 : 42);
                b.GetComponent<A11yLabel>().Text = TabA11y[i] + " tab" + (on ? ", selected" : "");
                var rt = (RectTransform)b.transform;
                UiKit.Place(rt, new Vector2(0f, 0f), new Vector2(pad + slotW * i + (slotW - size) * 0.5f, play ? 12f : (TabBarHeight - size) * 0.5f), new Vector2(size, size));
            }
            return bar;
        }
    }
}
