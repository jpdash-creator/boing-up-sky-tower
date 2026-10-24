using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>What the gear screens need from the meta scene: back, the other gear screens and a modal overlay layer that is cleaned up with the screen.</summary>
    public sealed class GearHost
    {
        public Action Back;
        public Action OpenCharacters;
        public Action OpenInventory;
        public Action OpenChests;
        public Action OpenInfo;
        /// <summary>The land stage built behind the screen (backdrop, ground, dirt oval); the doll stands on it.</summary>
        public Stage Stage;
        /// <summary>A fresh full-screen dim overlay (the meta popup layer); closing it is <see cref="CloseOverlay"/>.</summary>
        public Func<RectTransform> NewOverlay;
        public Action CloseOverlay;
    }

    /// <summary>
    /// The inventory (docs/19 section D): the character small on the land stage wearing the equipped gear, two icon buttons (character info, characters), three icon
    /// slot tabs (head, body, boots) and a 5 column grid of gear tiles showing only the gear picture on a rarity coloured tile; tap to wear or take off. A slot with
    /// no owned piece with art shows an empty state icon. Icons only, no words. Menu screen: no per-frame code.
    /// </summary>
    public sealed class InventoryScreen
    {
        public const float TopH = 860f, Side = 20f, DollH = 470f;
        const float TabY = TopH + 20f, TabH = 126f, TabW = 220f, TabGap = 14f, GridTop = TopH + 170f, GridTile = 186f, GridGap = 12f, ButtonsY = 300f;
        const int Columns = 5;

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly GearHost host;
        RectTransform top, doll, tabRow, gridViewport, gridContent;
        ScrollRect scroll;

        public int SlotTab { get; private set; }
        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }

        public InventoryScreen(RectTransform screen, ProgressService progress, ArtCatalog art, GearHost host)
        {
            this.screen = screen;
            this.progress = progress;
            this.art = art;
            this.host = host;
        }

        public void Build(int slotTab = 0)
        {
            SlotTab = Mathf.Clamp(slotTab, 0, GearCatalog.SlotCount - 1);
            BuildPanel();
            BuildTop();
            BuildTabs();
            BuildGrid();
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // ---- layout ----

        void BuildPanel()
        {
            // the dark lower part (the meta screen draws the scene behind it); it covers the whole width under the top scene
            RectTransform bottom = UiKit.Rect(screen, "Panel");
            bottom.anchorMin = new Vector2(0f, 0f);
            bottom.anchorMax = new Vector2(1f, 1f);
            bottom.offsetMin = new Vector2(-200f, -200f);
            bottom.offsetMax = new Vector2(200f, -TopH);
            Image bi = bottom.gameObject.AddComponent<Image>();
            bi.color = new Color(0.17f, 0.23f, 0.42f, 1f);
            bi.raycastTarget = false;
            RectTransform edge = UiKit.Rect(bottom, "Panel");
            edge.anchorMin = new Vector2(0f, 1f);
            edge.anchorMax = new Vector2(1f, 1f);
            edge.pivot = new Vector2(0.5f, 1f);
            edge.sizeDelta = new Vector2(0f, 10f);
            edge.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.26f, 1f);
            edge.GetComponent<Image>().raycastTarget = false;
        }

        void BuildTop()
        {
            if (top != null) UnityEngine.Object.Destroy(top.gameObject);
            top = UiKit.Rect(screen, "Top");
            UiKit.Stretch(top);

            // wallet pills right of the back button
            RectTransform row = UiKit.Rect(top, "Wallet");
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

            // the chosen character on the dirt oval of the land stage, small, wearing the equipped gear (the same composite as in the run)
            if (host.Stage != null && host.Stage.Root != null)
            {
                for (int i = host.Stage.Root.childCount - 1; i >= 0; i--)
                    if (host.Stage.Root.GetChild(i).name == "Doll") UnityEngine.Object.Destroy(host.Stage.Root.GetChild(i).gameObject);
                CharacterDef who = CharacterCatalog.Selected;
                doll = CharacterDoll.Build(host.Stage.Root, "Doll", progress, DollH, who, art);
                host.Stage.Stand(doll);
            }

            // icon buttons beside the character: what the gear boosts (left), the characters (right)
            Button boosts = ToyKit.IconButton(top, "Boosts", "Character info: what my gear boosts", 1, ToyKit.Get("icon_bolt"), new Vector2(130f, 126f), () => { if (host.OpenInfo != null) host.OpenInfo(); }, 0.6f);
            Anchor((RectTransform)boosts.transform, new Vector2(0f, 1f), new Vector2(Side, -ButtonsY), new Vector2(130f, 126f));
            Button heroes = ToyKit.IconButton(top, "Characters", "Characters", 0, ToyKit.Get("icon_heroes"), new Vector2(130f, 126f), () => { if (host.OpenCharacters != null) host.OpenCharacters(); }, 0.6f);
            Anchor((RectTransform)heroes.transform, new Vector2(1f, 1f), new Vector2(-Side, -ButtonsY), new Vector2(130f, 126f));

            // development builds: wear-test shortcut, grants every piece that has art (never shipped)
            if (CharacterCatalog.ShowTest)
            {
                Button grant = ToyKit.IconButton(top, "GrantAll", "Development: get every piece that has art", 2, ToyKit.Get("icon_chest"), new Vector2(130f, 126f), () => GrantAllWithArt(), 0.6f);
                Anchor((RectTransform)grant.transform, new Vector2(0f, 1f), new Vector2(Side, -(ButtonsY + 126f + 24f)), new Vector2(130f, 126f));
            }
        }

        /// <summary>Development builds only: gives one copy of every piece that has art (all drops are normally random). Returns how many were added.</summary>
        public int GrantAllWithArt()
        {
            int added = 0;
            foreach (GearDef d in GearCatalog.All)
            {
                if (!GearCatalog.HasArt(d) || progress.OwnsGear(d.Id)) continue;
                if (progress.GrantGear(d.Id)) added++;
            }
            if (added > 0) AudioManager.Play(Sfx.PowerUp);
            Refresh();
            return added;
        }

        // ---- slot tabs ----

        void BuildTabs()
        {
            if (tabRow != null) UnityEngine.Object.Destroy(tabRow.gameObject);
            tabRow = UiKit.Rect(screen, "Tabs");
            UiKit.Stretch(tabRow);
            float total = GearCatalog.SlotCount * TabW + (GearCatalog.SlotCount - 1) * TabGap;
            for (int s = 0; s < GearCatalog.SlotCount; s++)
            {
                int idx = s;
                Button b = ToyKit.IconButton(tabRow, "Tab" + GearCatalog.SlotNames[s], GearCatalog.SlotNames[s] + " slot" + (s == SlotTab ? ", selected" : ""), s == SlotTab ? 0 : 2, Glyphs.ForSlot(s), new Vector2(TabW, TabH), () => SelectSlotTab(idx), 0.6f);
                Anchor((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(-total * 0.5f + s * (TabW + TabGap), -TabY), new Vector2(TabW, TabH));
                ((RectTransform)b.transform).pivot = new Vector2(0f, 1f);
                ((RectTransform)b.transform).anchoredPosition = new Vector2(-total * 0.5f + s * (TabW + TabGap), -TabY);
            }
        }

        public void SelectSlotTab(int slot)
        {
            SlotTab = Mathf.Clamp(slot, 0, GearCatalog.SlotCount - 1);
            BuildTabs();
            BuildGrid(true);
        }

        // ---- grid ----

        /// <summary>The owned pieces of the selected slot that have art, highest rarity first. Pieces that are not owned or have no art are not shown.</summary>
        public List<GearDef> OwnedItems()
        {
            var owned = new List<GearDef>();
            foreach (GearDef d in GearCatalog.All)
                if ((int)d.Slot == SlotTab && GearCatalog.HasArt(d) && progress.OwnsGear(d.Id)) owned.Add(d);
            owned.Sort((a, b) => a.Rarity != b.Rarity ? b.Rarity.CompareTo(a.Rarity) : a.Number.CompareTo(b.Number));
            return owned;
        }

        void BuildGrid(bool resetScroll = false)
        {
            float keep = scroll != null && !resetScroll ? scroll.verticalNormalizedPosition : 1f;
            if (gridViewport != null) UnityEngine.Object.Destroy(gridViewport.gameObject);
            gridViewport = UiKit.Rect(screen, "GridViewport");
            gridViewport.anchorMin = Vector2.zero;
            gridViewport.anchorMax = Vector2.one;
            gridViewport.offsetMin = new Vector2(0f, ToyKit.TabBarReserve);
            gridViewport.offsetMax = new Vector2(0f, -GridTop);
            gridViewport.gameObject.AddComponent<RectMask2D>();
            var hit = gridViewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            List<GearDef> items = OwnedItems();
            int rows = (items.Count + Columns - 1) / Columns;
            gridContent = UiKit.Rect(gridViewport, "GridContent");
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(1f, 1f);
            gridContent.pivot = new Vector2(0.5f, 1f);
            gridContent.offsetMin = Vector2.zero;
            gridContent.offsetMax = Vector2.zero;
            gridContent.sizeDelta = new Vector2(0f, rows * (GridTile + GridGap) + GridGap);
            scroll = gridViewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = gridContent;
            scroll.viewport = gridViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;
            if (items.Count == 0)
            {
                // empty state: the slot's picture, dim, no text
                Image empty = UiKit.AddImage(gridViewport, "Empty", Glyphs.ForSlot(SlotTab), new Color(0.46f, 0.52f, 0.72f, 0.7f));
                empty.raycastTarget = false;
                Anchor(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(260f, 260f));
                empty.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                empty.gameObject.AddComponent<A11yLabel>().Text = "No " + GearCatalog.SlotNames[SlotTab] + " gear yet";
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                GearDef d = items[i];
                int row = i / Columns, col = i % Columns;
                bool worn = progress.EquippedId(d.Slot) == d.Id;
                RectTransform t = GearUi.Tile(gridContent, "Item" + i, d, new Vector2(GridTile, GridTile), () => TapItem(d), worn);
                t.anchorMin = new Vector2(col / (float)Columns, 1f);
                t.anchorMax = new Vector2((col + 1) / (float)Columns, 1f);
                t.pivot = new Vector2(0.5f, 1f);
                t.anchoredPosition = new Vector2(0f, -(GridGap + row * (GridTile + GridGap)));
                t.sizeDelta = new Vector2(-GridGap, GridTile);
            }
            scroll.verticalNormalizedPosition = keep;
        }

        /// <summary>Tap on a piece: wear it (replacing what the slot wore), or take it off when it is the worn one.</summary>
        public void TapItem(GearDef d)
        {
            if (d == null || !progress.OwnsGear(d.Id)) return;
            if (progress.EquippedId(d.Slot) == d.Id) progress.Unequip(d.Slot);
            else progress.Equip(d.Id);
            Refresh();
        }

        /// <summary>Redraws what depends on the owned and worn gear (the character, the tabs and the grid) and keeps the scroll position.</summary>
        public void Refresh()
        {
            BuildTop();
            BuildTabs();
            BuildGrid();
        }
    }
}
