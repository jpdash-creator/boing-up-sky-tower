using System;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The top row of three icon tabs of the Heroes screen (Bug List 6 item 3a): (1) the hero icon with "All Heroes", (2) the Merge icon with "Merge", (3) a power-up icon with "Powers".
    /// The same row sits under the header of All Heroes, Merge and Powers (the selected tab is gold with a white rim, the others dark with white text, so the state never relies on colour alone:
    /// the accessibility label says "selected") and on the Heroes landing screen (no tab selected). Every tab is at least 48 dp high and wide, 24 px apart, anchored by thirds of the width
    /// so it fits every aspect, and the label survives 130 percent text. Built once per screen build; no Update, no per-frame work.
    /// </summary>
    public static class HeroTabs
    {
        public const int AllHeroes = 0, Merge = 1, Powers = 2;
        public const string RowName = "HeroTabs";
        public static readonly string[] Captions = { "All Heroes", "Merge", "Powers" };
        public static readonly string[] ButtonNames = { "TabAllHeroes", "TabMerge", "TabPowers" };
        /// <summary>Margin between the row and the (safe area) screen edge.</summary>
        public const float SideMargin = 28f;
        const int Font = 38;
        const float IconSize = 84f, Gap = 24f, IconTop = 12f, CompactIconSize = 56f, CompactIconTop = 6f;
        static readonly Color SelectedFill = new Color(1f, 0.82f, 0.24f, 1f);
        static readonly Color PlainFill = new Color(0.10f, 0.08f, 0.26f, 0.94f);

        static float Line { get { return Mathf.Ceil(Mathf.Max(Font, UiKit.MinBodyTextPx) * UiKit.TextScale * 1.3f); } }

        /// <summary>Height of the row at the current text size (never below 48 dp on the real screen).</summary>
        public static float RowH { get { return RowHFor(false); } }

        /// <summary>Row height for the normal or the compact variant (smaller icon and padding, used when a page is short of height, for example the Merge screen at 3:4 with 130 percent text).</summary>
        public static float RowHFor(bool compact)
        {
            return Mathf.Max(UiKit.MinTouchRef, compact ? CompactIconTop + CompactIconSize + 2f + Line + 6f : IconTop + IconSize + 4f + Line + 12f);
        }

        /// <summary>
        /// Builds the row <paramref name="top"/> px below the top of <paramref name="parent"/> and returns it. <paramref name="selected"/> is 0 to 2 (the tab of the page showing) or -1 for none;
        /// a tap on the selected tab does nothing. A null action leaves its tab inert.
        /// There is no owned-count badge on the tabs (Bug List 8 item 1a/1b).
        /// </summary>
        public static RectTransform Build(RectTransform parent, float top, int selected, Action openAllHeroes, Action openMerge, Action openPowers, bool compact = false)
        {
            float h = RowHFor(compact);
            RectTransform row = UiKit.Rect(parent, RowName);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(SideMargin, -(top + h));
            row.offsetMax = new Vector2(-SideMargin, -top);
            Action[] actions = { openAllHeroes, openMerge, openPowers };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                bool on = i == selected;
                Button b = UiKit.AddButton(row, ButtonNames[i], "", UiKit.PillSprite, on ? SelectedFill : PlainFill, new Vector2(300f, h), () => { if (!on && actions[idx] != null) actions[idx](); });
                b.GetComponent<A11yLabel>().Text = Captions[i] + " tab" + (on ? ", selected" : "");
                b.GetComponent<Image>().raycastTarget = true;
                RectTransform cell = (RectTransform)b.transform;
                cell.anchorMin = new Vector2(i / 3f, 0f);
                cell.anchorMax = new Vector2((i + 1) / 3f, 1f);
                cell.pivot = new Vector2(0.5f, 0.5f);
                cell.offsetMin = new Vector2(Gap * 0.5f, 0f);
                cell.offsetMax = new Vector2(-Gap * 0.5f, 0f);

                if (on)
                {
                    Image rim = UiKit.AddImage(cell, "Rim", Theme.RingSprite, Color.white);
                    rim.type = Image.Type.Sliced;
                    rim.preserveAspect = false;
                    rim.pixelsPerUnitMultiplier = 1.4f;
                    UiKit.Stretch(rim.rectTransform);
                }

                AddIcon(cell, i, compact);

                Text label = UiKit.AddText(cell, "Label", Captions[i], Font, on ? UiKit.ButtonInk : Color.white, TextAnchor.MiddleCenter, true);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline o = label.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                RectTransform lr = label.rectTransform;
                lr.anchorMin = new Vector2(0f, 0f);
                lr.anchorMax = new Vector2(1f, 0f);
                lr.pivot = new Vector2(0.5f, 0f);
                float lb = compact ? 6f : 10f;
                lr.offsetMin = new Vector2(4f, lb);
                lr.offsetMax = new Vector2(-4f, lb + Line);
            }
            return row;
        }

        /// <summary>The icon of tab <paramref name="tab"/>, centred at the top of its cell (name "Icon"). Missing art falls back to a drawn badge, never nothing.</summary>
        static void AddIcon(RectTransform cell, int tab, bool compact)
        {
            float size = compact ? CompactIconSize : IconSize, top = compact ? CompactIconTop : IconTop;
            if (tab == Powers)
            {
                // the Flight power-up art (or its code badge when the art is missing)
                Image pw = PowerUpUi.AddIcon(cell, "Icon", PowerUp.Flight, new Vector2(0.5f, 1f), new Vector2(0f, -top), size, false);
                pw.raycastTarget = false;
                return;
            }
            Sprite sprite = tab == Merge ? MergeArt.Icon : (ToyKit.Get("icon_heroes") ?? ShopIcons.CharacterPower);
            Image ic = UiKit.AddImage(cell, "Icon", sprite, Color.white);
            ic.preserveAspect = true;
            ic.raycastTarget = false;
            UiKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(size, size));
        }
    }
}
