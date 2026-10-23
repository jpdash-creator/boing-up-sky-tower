using System;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The loadout picker (docs/15 section 9.4): a row of three slot buttons inside a popup panel (level popup and endless start). Tap a slot to open the
    /// chooser (a second layer in the same popup) listing the owned power-ups with their counts; an empty stock explains where to get more
    /// (the shop). The loadout is stored in the save at once (ProgressService.SetLoadoutSlot). Built in code, menu screens only: no per-frame code.
    /// </summary>
    public sealed class LoadoutPicker
    {
        public const float RowW = 740f, CellW = 232f, CellH = 150f, CellGap = 22f, HintH = 132f;
        /// <summary>Height of the header line, the three slots and the shop hint, for popup layout.</summary>
        public const float BlockHeight = 64f + CellH + 8f + HintH;

        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly Action openShop;
        RectTransform popupRoot;
        Transform under;                 // the level card hidden while the chooser is open
        Transform panel;
        float top;
        RectTransform rowsHost;
        RectTransform chooser;

        public RectTransform ChooserRoot { get { return chooser; } }
        public int RowCount { get { return PowerUpCatalog.SlotCount; } }

        /// <param name="openShop">Closes the popup and opens the shop's power-up tab.</param>
        public LoadoutPicker(ProgressService progress, ArtCatalog art, Action openShop)
        {
            this.progress = progress;
            this.art = art;
            this.openShop = openShop;
        }

        /// <summary>Adds the header and the three rows to <paramref name="panelTransform"/>, <paramref name="yFromTop"/> below its top edge.</summary>
        public void Build(RectTransform popupRootRect, Transform panelTransform, float yFromTop)
        {
            popupRoot = popupRootRect;
            panel = panelTransform;
            top = yFromTop;
            Text head = UiKit.AddText(panel, "LoadoutHeader", "Power-ups (up to 3)", 42, Theme.Ink, TextAnchor.MiddleLeft, true);
            UiKit.OnPanel(head);
            UiKit.Place((RectTransform)head.transform, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(RowW, 56f));
            BuildRows();
        }

        void BuildRows()
        {
            if (rowsHost != null) UnityEngine.Object.Destroy(rowsHost.gameObject);
            rowsHost = UiKit.Rect(panel, "LoadoutRows");
            UiKit.Place(rowsHost, new Vector2(0.5f, 1f), new Vector2(0f, -(top + 64f)), new Vector2(RowW, CellH + 8f + HintH));
            string[] ids = progress.GetLoadout();
            bool anyStock = false;
            foreach (PowerUpDef d in progress.PowerUpDefs.Implemented) if (progress.PowerUpCount(d.Id) > 0) { anyStock = true; break; }
            for (int i = 0; i < PowerUpCatalog.SlotCount; i++) BuildCell(i, ids[i]);
            if (!anyStock)
            {
                // the empty-state text points to the shop (docs/15 FR-PWR-10)
                Text hint = UiKit.AddText(rowsHost, "ShopHint", "No power-ups yet. Get them in the Shop.", 42, Theme.InkSoft, TextAnchor.UpperLeft);
                UiKit.OnPanel(hint);
                hint.color = Theme.InkSoft;
                UiKit.Place((RectTransform)hint.transform, new Vector2(0f, 1f), new Vector2(0f, -(CellH + 8f)), new Vector2(RowW, HintH));
            }
        }

        /// <summary>One slot: a badge with the power-up code and the units owned ("x3"), or a plus sign when empty. The names are in the chooser.</summary>
        void BuildCell(int slot, string id)
        {
            int index = slot;
            PowerUpDef def = id != null ? progress.PowerUpDefs.Get(id) : null;
            string a11y = def != null
                ? "Slot " + (slot + 1) + ": " + PowerUpUi.Spoken(def.Type, progress.PowerUpCount(def.Id)) + ". Tap to change"
                : "Slot " + (slot + 1) + ": empty. Tap to choose a power-up";
            Button cell = UiKit.AddButton(rowsHost, "Slot" + (slot + 1), "", Theme.RoundSprite, Theme.SurfaceDim, new Vector2(CellW, CellH), () => OpenChooser(index), 42);
            RectTransform crt = (RectTransform)cell.transform;
            UiKit.Place(crt, new Vector2(0f, 1f), new Vector2(slot * (CellW + CellGap), 0f), new Vector2(CellW, CellH));
            cell.GetComponent<A11yLabel>().Text = a11y;
            Image rim = UiKit.AddImage(crt, "Rim", Theme.RingSprite, new Color(Theme.InkSoft.r, Theme.InkSoft.g, Theme.InkSoft.b, 0.45f));
            rim.type = Image.Type.Sliced;
            rim.preserveAspect = false;
            UiKit.Stretch(rim.rectTransform);

            if (def != null)
            {
                PowerUpUi.AddIcon(crt, "Icon", def.Type, new Vector2(0f, 0.5f), new Vector2(12f, 0f), 100f, false);
                Text count = UiKit.AddText(crt, "Count", "x" + progress.PowerUpCount(def.Id), 44, Theme.Ink, TextAnchor.MiddleCenter, true);
                UiKit.OnPanel(count);
                UiKit.Place((RectTransform)count.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(CellW - 124f, CellH - 20f));
            }
            else
            {
                Image plus = UiKit.AddImage(crt, "Icon", Theme.RoundSprite, new Color(0.82f, 0.84f, 0.9f, 1f));
                plus.type = Image.Type.Sliced;
                plus.preserveAspect = false;
                plus.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 50f);
                UiKit.Place(plus.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
                Text sign = UiKit.AddText(plus.transform, "Plus", "+", 64, Theme.Ink, TextAnchor.MiddleCenter, true);
                UiKit.OnPanel(sign);
                UiKit.Stretch((RectTransform)sign.transform);
            }
        }

        // ---------------- chooser (second layer inside the popup) ----------------

        public void OpenChooser(int slot)
        {
            CloseChooser();
            // The chooser replaces the level card while it is open (one modal at a time: nothing overlaps, CUX-3); closing brings the card back.
            under = popupRoot.Find("Panel");
            if (under != null) under.gameObject.SetActive(false);
            chooser = UiKit.Rect(popupRoot, "LoadoutChooser");
            UiKit.Stretch(chooser);
            var dim = chooser.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            RectTransform cp = UiKit.BuildPopupFrame(chooser, "Slot " + (slot + 1), art, new Vector2(860f, 1300f), CloseChooser);

            string[] now = progress.GetLoadout();
            var owned = new System.Collections.Generic.List<PowerUpDef>();
            foreach (PowerUpDef d in progress.PowerUpDefs.Implemented) if (progress.PowerUpCount(d.Id) > 0) owned.Add(d);

            if (owned.Count == 0)
            {
                Text none = UiKit.AddText(cp, "None", "You do not own any power-ups yet.\nBuy them in the Shop.", 46, Theme.Ink, TextAnchor.MiddleCenter);
                UiKit.OnPanel(none);
                UiKit.Place((RectTransform)none.transform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(740f, 300f));
                Button shop0 = ToyKit.IconButton(cp, "Open shop", "Open the shop to buy power-ups", 1, ToyKit.Get("icon_shop"), new Vector2(170f, 170f), OpenShop, 0.62f);
                UiKit.Place((RectTransform)shop0.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(170f, 170f));
                return;
            }

            // Big round power-up icons in a 3-column grid (owner issue 16.5): the count on a dark pill, the slot number when it is already used in
            // another slot, a gold ring around the current choice and the name as a one-line caption.
            const float cell = 210f, gapX = 36f, pitch = 300f;
            for (int i = 0; i < owned.Count; i++)
            {
                PowerUpDef d = owned[i];
                int col = i % 3, r = i / 3;
                int where = Array.IndexOf(now, d.Id);
                string id = d.Id;
                bool current = where == slot;
                float cx = (col - 1) * (cell + gapX);
                Button card = UiKit.AddButton(cp, "Choose" + d.Id, "", Theme.RoundSprite, current ? new Color(1f, 0.9f, 0.55f, 1f) : Theme.SurfaceDim, new Vector2(cell, cell), () => Choose(slot, id), 42);
                UiKit.Place((RectTransform)card.transform, new Vector2(0.5f, 1f), new Vector2(cx, -(190f + r * pitch)), new Vector2(cell, cell));
                card.GetComponent<A11yLabel>().Text = PowerUpUi.Spoken(d.Type, progress.PowerUpCount(id)) + (where >= 0 && !current ? ", in slot " + (where + 1) : (current ? ", current choice" : ""));
                Image ring = UiKit.AddImage(card.transform, "Ring", Glyphs.Ring, current ? new Color(1f, 0.8f, 0.15f, 1f) : new Color(Theme.InkSoft.r, Theme.InkSoft.g, Theme.InkSoft.b, 0.5f));
                ring.rectTransform.anchorMin = Vector2.zero;
                ring.rectTransform.anchorMax = Vector2.one;
                ring.rectTransform.offsetMin = new Vector2(-6f, -6f);
                ring.rectTransform.offsetMax = new Vector2(6f, 6f);
                Image icon = PowerUpUi.AddIcon(card.transform, "Icon", d.Type, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), cell - 34f, false);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                // count pill (dark, white number), bottom centre
                Image pill = UiKit.AddImage(card.transform, "CountBacking", Theme.RoundSprite, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.9f));
                pill.type = Image.Type.Sliced;
                pill.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 28f);
                UiKit.Place(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(110f, 54f));
                Text cnt = UiKit.AddText(pill.transform, "Count", "x" + progress.PowerUpCount(id), 42, Color.white, TextAnchor.MiddleCenter, true);
                cnt.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Stretch((RectTransform)cnt.transform);
                if (where >= 0 && !current)
                {
                    Image tag = UiKit.AddImage(card.transform, "SlotTag", Theme.RoundSprite, Theme.Sky);
                    tag.type = Image.Type.Sliced;
                    tag.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / 30f);
                    UiKit.Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(2f, 2f), new Vector2(60f, 60f));
                    tag.rectTransform.pivot = new Vector2(1f, 1f);
                    Text tg = UiKit.AddText(tag.transform, "Slot", (where + 1).ToString(), 42, Color.white, TextAnchor.MiddleCenter, true);
                    tg.horizontalOverflow = HorizontalWrapMode.Overflow;
                    UiKit.Stretch((RectTransform)tg.transform);
                }
                Text nm = UiKit.AddText(cp, "Name" + d.Id, d.Name, 42, Theme.Ink, TextAnchor.MiddleCenter, true);
                UiKit.OnPanel(nm);
                nm.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place((RectTransform)nm.transform, new Vector2(0.5f, 1f), new Vector2(cx, -(190f + r * pitch + cell + 6f)), new Vector2(cell + gapX, 54f));
                nm.rectTransform.pivot = new Vector2(0.5f, 1f);
            }

            Button clear = ToyKit.IconButton(cp, "Empty slot", "Leave slot " + (slot + 1) + " empty", 3, Glyphs.Cross, new Vector2(150f, 150f), () => Choose(slot, null), 0.56f);
            UiKit.Place((RectTransform)clear.transform, new Vector2(0f, 0f), new Vector2(70f, 50f), new Vector2(150f, 150f));
            Button shop = ToyKit.IconButton(cp, "Shop", "Open the shop to buy more power-ups", 0, ToyKit.Get("icon_shop"), new Vector2(150f, 150f), OpenShop, 0.62f);
            UiKit.Place((RectTransform)shop.transform, new Vector2(1f, 0f), new Vector2(-70f, 50f), new Vector2(150f, 150f));
        }

        Button ChooserButton(Transform parent, string label, bool primary, Vector2 anchor, Vector2 pos, Vector2 size, Action action)
        {
            Sprite sprite = ToyKit.Button(primary ? 1 : 2) ?? (art != null ? ArtCatalog.Pick(art.Buttons, primary ? 1 : 0) : null);
            Button b = UiKit.AddButton(parent, label, label, sprite, Color.white, size, action, 46);
            UiKit.Place((RectTransform)b.transform, anchor, pos, size);
            return b;
        }

        void Choose(int slot, string id)
        {
            progress.SetLoadoutSlot(slot, id);
            CloseChooser();
            BuildRows();
        }

        void OpenShop()
        {
            if (openShop != null) openShop();
        }

        public void CloseChooser()
        {
            if (chooser != null) UnityEngine.Object.Destroy(chooser.gameObject);
            chooser = null;
            if (under != null) under.gameObject.SetActive(true);
            under = null;
        }
    }
}
