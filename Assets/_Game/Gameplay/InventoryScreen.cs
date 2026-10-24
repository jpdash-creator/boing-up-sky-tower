using System;
using System.Collections.Generic;
using System.Globalization;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
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
        /// <summary>A fresh full-screen dim overlay (the meta popup layer); closing it is <see cref="CloseOverlay"/>.</summary>
        public Func<RectTransform> NewOverlay;
        public Action CloseOverlay;
    }

    /// <summary>
    /// The inventory (owner reference 1, docs/17 section E): Momo in the middle on a soft scene, four rarity framed slot tiles around him, two icon buttons under
    /// him (boost summary, characters), then four slot tabs, a forge button and a 5 column grid of the slot's pieces (owned in rarity colour, unowned as
    /// dark silhouettes). Icons and numbers only. Menu screen: no per-frame code.
    /// </summary>
    public sealed class InventoryScreen
    {
        const float TopH = 1010f, Side = 20f, SlotTile = 190f, DollH = 520f, DollTop = 250f;
        const float TabY = TopH + 20f, TabH = 126f, GridTop = TopH + 170f, GridTile = 186f, GridGap = 12f;
        const int Columns = 5;

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly GearHost host;
        RectTransform top, doll, tabRow, gridViewport, gridContent;
        ScrollRect scroll;
        Text forgeText;
        Button forgeButton;

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
            // the dark lower half of reference 1 (the meta screen draws the scene behind it); it covers the whole width under the top scene
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

            // Momo in the middle, the worn gear on him
            doll = MomoDoll.Build(top, "Doll", art != null ? art.Player : null, progress, DollH);
            Anchor(doll, new Vector2(0.5f, 1f), new Vector2(0f, -DollTop), doll.sizeDelta);

            // four slots: head and body on the left, pants and boots on the right
            for (int s = 0; s < GearCatalog.SlotCount; s++) BuildSlotTile((GearSlot)s);

            // two icon buttons under Momo: what the gear boosts, and the characters
            Button boosts = ToyKit.IconButton(top, "Boosts", "What my gear boosts", 1, ToyKit.Get("icon_bolt"), new Vector2(130f, 126f), OpenBoostSummary, 0.6f);
            Anchor((RectTransform)boosts.transform, new Vector2(0.5f, 1f), new Vector2(-84f, -(DollTop + DollH + 30f)), new Vector2(130f, 126f));
            Button heroes = ToyKit.IconButton(top, "Characters", "Characters", 0, ToyKit.Get("icon_heroes"), new Vector2(130f, 126f), () => { if (host.OpenCharacters != null) host.OpenCharacters(); }, 0.6f);
            Anchor((RectTransform)heroes.transform, new Vector2(0.5f, 1f), new Vector2(84f, -(DollTop + DollH + 30f)), new Vector2(130f, 126f));
        }

        void BuildSlotTile(GearSlot slot)
        {
            int s = (int)slot;
            bool left = s < 2;
            GearDef d = progress.EquippedDef(slot);
            Vector2 size = new Vector2(SlotTile, SlotTile);
            RectTransform t = d != null
                ? GearUi.Tile(top, "Slot" + GearCatalog.SlotNames[s], d, GearTileState.Owned, progress.GearLevel(d.Id), 1, size, () => TapSlot(slot), false, slot, true)
                : GearUi.Tile(top, "Slot" + GearCatalog.SlotNames[s], null, GearTileState.Empty, 1, 0, size, () => TapSlot(slot), false, slot);
            float y = 200f + (s % 2) * (SlotTile + 70f);
            Anchor(t, new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? Side : -Side, -y), size);
        }

        void TapSlot(GearSlot slot)
        {
            SelectSlotTab((int)slot);   // tapping a worn piece opens its slot list; the list item swaps or removes it
        }

        // ---- slot tabs and forge ----

        void BuildTabs()
        {
            if (tabRow != null) UnityEngine.Object.Destroy(tabRow.gameObject);
            tabRow = UiKit.Rect(screen, "Tabs");
            UiKit.Stretch(tabRow);
            for (int s = 0; s < GearCatalog.SlotCount; s++)
            {
                int idx = s;
                Button b = ToyKit.IconButton(tabRow, "Tab" + GearCatalog.SlotNames[s], GearCatalog.SlotNames[s] + " slot" + (s == SlotTab ? ", selected" : ""), s == SlotTab ? 0 : 2, Glyphs.ForSlot(s), new Vector2(126f, TabH), () => SelectSlotTab(idx), 0.6f);
                Anchor((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(Side + s * 134f, -TabY), new Vector2(126f, TabH));
            }
            int forgeable = progress.ForgeablePieces();
            forgeButton = UiKit.AddButton(tabRow, "Forge", "", ToyKit.Button(forgeable > 0 ? 1 : 2), Color.white, new Vector2(250f, TabH), TapForge, 46);
            Anchor((RectTransform)forgeButton.transform, new Vector2(1f, 1f), new Vector2(-Side, -TabY), new Vector2(250f, TabH));
            forgeButton.GetComponent<A11yLabel>().Text = forgeable > 0 ? "Forge: merge spare copies, " + forgeable + " pieces ready" : "Forge: nothing to merge yet";
            Image ai = UiKit.AddImage(forgeButton.transform, "Icon", Glyphs.Anvil, forgeable > 0 ? Color.white : new Color(1f, 1f, 1f, 0.55f));
            Anchor(ai.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(80f, 80f));
            ai.rectTransform.pivot = new Vector2(0f, 0.5f);
            if (forgeable > 0)
            {
                forgeText = UiKit.AddText(forgeButton.transform, "Ready", forgeable.ToString(), 56, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
                forgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
                var o = forgeText.GetComponent<Outline>();
                if (o != null) o.enabled = false;
                var trt = (RectTransform)forgeText.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(110f, 0f);
                trt.offsetMax = new Vector2(-20f, 0f);
            }
            else
            {
                Image lk = UiKit.AddImage(forgeButton.transform, "Locked", ToyKit.Get("icon_lock"), new Color(1f, 1f, 1f, 0.7f));
                Anchor(lk.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(66f, 66f));
                lk.rectTransform.pivot = new Vector2(1f, 0.5f);
            }
        }

        public void SelectSlotTab(int slot)
        {
            SlotTab = Mathf.Clamp(slot, 0, GearCatalog.SlotCount - 1);
            BuildTabs();
            BuildGrid(true);
        }

        /// <summary>Forge: merges every piece with 3 spare copies into +1 level. Nothing to merge plays no change.</summary>
        public void TapForge()
        {
            int levels = progress.ForgeAll();
            if (levels > 0) AudioManager.Play(Sfx.PowerUp);
            Refresh();
        }

        // ---- grid ----

        /// <summary>Owned pieces of the slot first (highest rarity first), then the pieces not found yet.</summary>
        List<GearDef> GridItems()
        {
            var owned = new List<GearDef>();
            var missing = new List<GearDef>();
            foreach (GearDef d in GearCatalog.All)
            {
                if ((int)d.Slot != SlotTab) continue;
                (progress.OwnsGear(d.Id) ? owned : missing).Add(d);
            }
            Comparison<GearDef> byRarity = (a, b) => a.Rarity != b.Rarity ? b.Rarity.CompareTo(a.Rarity) : a.Number.CompareTo(b.Number);
            owned.Sort(byRarity);
            missing.Sort(byRarity);
            owned.AddRange(missing);
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
            List<GearDef> items = GridItems();
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
            for (int i = 0; i < items.Count; i++)
            {
                GearDef d = items[i];
                int row = i / Columns, col = i % Columns;
                bool owned = progress.OwnsGear(d.Id);
                bool worn = progress.EquippedId(d.Slot) == d.Id;
                RectTransform t = GearUi.Tile(gridContent, "Item" + i, d, owned ? GearTileState.Owned : GearTileState.Missing, progress.GearLevel(d.Id), progress.GearCount(d.Id), new Vector2(GridTile, GridTile), () => TapItem(d), worn);
                t.anchorMin = new Vector2(col / (float)Columns, 1f);
                t.anchorMax = new Vector2((col + 1) / (float)Columns, 1f);
                t.pivot = new Vector2(0.5f, 1f);
                t.anchoredPosition = new Vector2(0f, -(GridGap + row * (GridTile + GridGap)));
                t.sizeDelta = new Vector2(-GridGap, GridTile);
            }
            scroll.verticalNormalizedPosition = keep;
        }

        /// <summary>Tap on a list piece: wear it, swap it, or take a worn piece off. A piece not found yet does nothing.</summary>
        public void TapItem(GearDef d)
        {
            if (d == null || !progress.OwnsGear(d.Id)) return;
            if (progress.EquippedId(d.Slot) == d.Id) progress.Unequip(d.Slot);
            else progress.Equip(d.Id);
            Refresh();
        }

        /// <summary>Redraws what depends on the owned and worn gear (doll, slot tiles, forge, list) and keeps the scroll position.</summary>
        public void Refresh()
        {
            BuildTop();
            BuildTabs();
            BuildGrid();
        }

        // ---- boost summary ----

        public void OpenBoostSummary()
        {
            if (host.NewOverlay == null) return;
            RectTransform overlay = host.NewOverlay();
            RectTransform panel = UiKit.BuildPopupFrame(overlay, "", art, new Vector2(860f, 1090f), host.CloseOverlay);
            int[] boost = progress.GearBoostPercents();
            var list = new List<PowerUpDef>(progress.PowerUpDefs.Implemented);
            for (int i = 0; i < list.Count; i++)
            {
                PowerUpDef def = list[i];
                int pct = boost[(int)def.Type];
                float y = 170f + i * 130f;
                Image ic = PowerUpUi.AddIcon(panel, "Power" + i, def.Type, new Vector2(0f, 1f), new Vector2(60f, -y), 112f, pct <= 0);
                ic.rectTransform.pivot = new Vector2(0f, 1f);
                Image bar = UiKit.AddPanel(panel, "RowsBacking", new Color(0.93f, 0.95f, 1f, 1f));
                Anchor(bar.rectTransform, new Vector2(0f, 1f), new Vector2(200f, -(y + 6f)), new Vector2(600f, 100f));
                float frac = Mathf.Clamp01(pct / 200f);
                Image fill = UiKit.AddPanel(bar.transform, "Fill", pct > 0 ? new Color(0.17f, 0.74f, 0.47f, 1f) : new Color(0.7f, 0.74f, 0.84f, 1f));
                fill.rectTransform.anchorMin = new Vector2(0f, 0f);
                fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.05f, frac), 1f);
                fill.rectTransform.offsetMin = new Vector2(6f, 6f);
                fill.rectTransform.offsetMax = new Vector2(-6f, -6f);
                Text t = UiKit.AddText(bar.transform, "Percent", pct > 0 ? "+" + pct + "%" : "0", 52, Theme.Ink, TextAnchor.MiddleCenter, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.OnPanel(t);
                UiKit.Stretch((RectTransform)t.transform);
            }
        }
    }
}
