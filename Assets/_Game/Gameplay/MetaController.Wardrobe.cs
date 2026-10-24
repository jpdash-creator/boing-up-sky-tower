using System;
using System.Collections.Generic;
using System.Globalization;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The Gear screen (hats, shirts, pants and boots layered on the character) and the Heroes screen (the 20 characters, 4 per region), docs/15 sections 5, 7 and 9.
    /// Both are tabs of the persistent bottom bar. Placeholder art only: gear is drawn in code (<see cref="GearArt"/>), non-Momo heroes are Momo in another colour.
    /// </summary>
    public sealed partial class MetaController
    {
        string gearHeroId;
        GearSlot gearSlot = GearSlot.Hat;
        int heroRegionTab;
        string heroFocusId;
        RectTransform gearBody, heroBody;
        float gearScroll = 1f;

        static string Num(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        /// <summary>Grid rule of these screens: equal columns across the full width, anchored by fractions so every aspect fits.</summary>
        static void GridCell(RectTransform crt, float y, int col, int cols, float h, float gap)
        {
            crt.anchorMin = new Vector2(col / (float)cols, 1f);
            crt.anchorMax = new Vector2((col + 1) / (float)cols, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -y);
            crt.sizeDelta = new Vector2(-gap, h);
        }

        /// <summary>A vertical scroll area between two offsets (from the top and from the bottom of the screen); returns the content rect.</summary>
        static RectTransform ScrollArea(RectTransform parent, string name, float top, float bottom, float contentHeight, out ScrollRect scroll)
        {
            RectTransform vp = UiKit.Rect(parent, name);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, bottom);
            vp.offsetMax = new Vector2(0f, -top);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            RectTransform content = UiKit.Rect(vp, name + "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, contentHeight);
            scroll = vp.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;
            return content;
        }

        /// <summary>A kit button with an optional currency icon at the left and the label after it (used for prices).</summary>
        Button IconLabelButton(Transform parent, string name, int role, Sprite icon, string label, string a11y, Action onClick, int fontSize = 40)
        {
            Button b = UiKit.AddButton(parent, name, label, ToyKit.Button(role) ?? Btn(role), Color.white, new Vector2(200f, 126f), onClick, fontSize);
            b.GetComponent<A11yLabel>().Text = a11y;
            Text t = b.transform.Find("Label").GetComponent<Text>();
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (icon != null)
            {
                Image ic = UiKit.AddImage(b.transform, "Icon", icon, Color.white);
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                ic.rectTransform.pivot = new Vector2(0f, 0.5f);
                ic.rectTransform.anchoredPosition = new Vector2(14f, 0f);
                ic.rectTransform.sizeDelta = new Vector2(52f, 52f);
                t.rectTransform.offsetMin = new Vector2(62f, 0f);
                t.rectTransform.offsetMax = new Vector2(-8f, 0f);
            }
            return b;
        }

        static void StretchBottom(RectTransform rt, float left, float right, float bottom, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + height);
        }

        void NeedMoreCoins(int price, string what)
        {
            Message("Not enough coins", "You need " + Num(price) + " coins for " + what + " and have " + Num(progress.Coins) + ".\nEarn coins in levels or buy them with gems in the shop.");
        }

        /// <summary>A message popup tall enough for 130 percent text (the default message popup only fits one or two short lines).</summary>
        void Message(string title, string body)
        {
            OpenPopup(title, body, 1180f);
            PopupButton("OK", Btn(0), Color.white, 60f, ClosePopup);
        }

        // ================= Gear =================

        public string GearHeroId { get { return gearHeroId; } }

        /// <summary>The wardrobe: the character with the worn pieces on top, the four slot buttons and the pieces of the open slot.</summary>
        public void ShowGear(string heroId = null, GearSlot? slot = null)
        {
            if (heroId != null) gearHeroId = heroId;
            HeroDef hero = progress.HeroDefs.Get(gearHeroId);
            if (hero == null || !progress.HeroOwned(hero)) hero = progress.HeroDefs.Get(progress.SelectedHeroId);
            gearHeroId = hero.Id;
            if (slot.HasValue) gearSlot = slot.Value;
            gearScroll = 1f;
            NewScreen("Gear");
            SkyBackground();
            AddBack("Back", ShowMenu);
            AddPillRow();
            ToyKit.Ribbon(screen, "Gear", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(520f, 170f));
            gearBody = null;
            BuildGearBody();
            AddTabBar(ToyKit.TabGear);
        }

        const float GearPreviewTop = 330f, GearPreviewH = 400f, GearSlotsTop = 800f, GearGridTop = 950f;

        void BuildGearBody()
        {
            float keep = gearScroll;
            if (gearBody != null) { gearBody.gameObject.SetActive(false); Destroy(gearBody.gameObject); }
            gearBody = UiKit.Rect(screen, "GearBody");
            UiKit.Stretch(gearBody);
            HeroDef hero = progress.HeroDefs.Get(gearHeroId);

            RectTransform momoBox = GearArt.BuildDressedMomo(gearBody, "Preview", art != null ? art.Player : null, hero, progress, hero.Id, GearPreviewH);
            momoBox.anchorMin = momoBox.anchorMax = new Vector2(0.5f, 1f);
            momoBox.pivot = new Vector2(0.5f, 1f);
            momoBox.anchoredPosition = new Vector2(0f, -GearPreviewTop);

            Text who = UiKit.AddText(gearBody, "Who", hero.Name + (progress.OwnedGearCount() == 0 ? " - no gear yet" : ""), 46, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(who);
            who.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Place((RectTransform)who.transform, new Vector2(0.5f, 1f), new Vector2(0f, -(GearPreviewTop + GearPreviewH + 6f)), new Vector2(900f, 60f));
            who.rectTransform.pivot = new Vector2(0.5f, 1f);

            // Slot buttons: the open slot is yellow with a tick, the others blue (the tick is the cue besides colour).
            RectTransform row = UiKit.Rect(gearBody, "Slots");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(28f, -(GearSlotsTop + 126f));
            row.offsetMax = new Vector2(-28f, -GearSlotsTop);
            for (int s = 0; s < 4; s++)
            {
                var slot = (GearSlot)s;
                bool on = slot == gearSlot;
                string worn = progress.EquippedGear(hero.Id, slot);
                Button b = UiKit.AddButton(row, "Slot" + RarityInfo.SlotName(slot), RarityInfo.SlotName(slot), ToyKit.Button(on ? 0 : 2), Color.white, new Vector2(200f, 126f), () => { gearSlot = slot; gearScroll = 1f; BuildGearBody(); }, 42);
                b.GetComponent<A11yLabel>().Text = RarityInfo.SlotName(slot) + ", " + (worn != null ? "wearing " + progress.GearDefs.Get(worn).Name : "nothing worn") + (on ? ", open" : "");
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(s / 4f, 0f);
                rt.anchorMax = new Vector2((s + 1) / 4f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(6f, 0f);
                rt.offsetMax = new Vector2(-6f, 0f);
                Text label = b.transform.Find("Label").GetComponent<Text>();
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (on)
                {
                    Image tick = UiKit.AddImage(b.transform, "Tick", Glyphs.Check, Color.white);
                    tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    tick.rectTransform.pivot = new Vector2(0f, 0.5f);
                    tick.rectTransform.anchoredPosition = new Vector2(10f, 0f);
                    tick.rectTransform.sizeDelta = new Vector2(48f, 48f);
                    label.rectTransform.offsetMin = new Vector2(52f, 0f);
                }
            }

            List<GearDef> pieces = progress.GearDefs.InSlot(gearSlot);
            const int cols = 3;
            const float cardH = 520f, gap = 16f;
            int rows = (pieces.Count + cols - 1) / cols;
            ScrollRect sr;
            RectTransform list = ScrollArea(gearBody, "GearList", GearGridTop, ToyKit.TabBarReserve + 4f, rows * (cardH + gap) + 12f, out sr);
            for (int i = 0; i < pieces.Count; i++)
            {
                RectTransform crt = UiKit.Rect(list, "Panel");
                GridCell(crt, 6f + (i / cols) * (cardH + gap), i % cols, cols, cardH, gap);
                // The sides keep a 28 px margin like every other list: the cell rectangles are inset by padding on the content.
                BuildGearCard(crt, hero, pieces[i]);
            }
            list.offsetMin = new Vector2(28f, list.offsetMin.y);
            list.offsetMax = new Vector2(-28f, list.offsetMax.y);
            sr.verticalNormalizedPosition = keep;
            gearScrollRect = sr;
        }

        ScrollRect gearScrollRect;

        void BuildGearCard(RectTransform crt, HeroDef hero, GearDef piece)
        {
            Theme.CardBacking(crt);
            bool owned = progress.OwnsGear(piece.Id);
            bool worn = progress.EquippedGear(hero.Id, piece.Slot) == piece.Id;

            Image icon = UiKit.AddImage(crt, "Piece", GearArt.PieceSprite(piece), Color.white);
            icon.preserveAspect = true;
            UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(250f, 110f));
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            if (!owned) icon.color = new Color(1f, 1f, 1f, 0.55f);

            Text name = UiKit.AddText(crt, "Name", piece.Name, 42, Theme.Ink, TextAnchor.MiddleCenter, true);
            DropOutline(name);
            StretchTop(name.rectTransform, 6f, 6f, 136f, 136f);
            Text rar = UiKit.AddText(crt, "Rarity", RarityInfo.Name(piece.Rarity) + " " + new string('|', RarityInfo.Stripes(piece.Rarity)), 42, Theme.InkSoft, TextAnchor.MiddleCenter);
            DropOutline(rar);
            rar.horizontalOverflow = HorizontalWrapMode.Overflow;
            StretchTop(rar.rectTransform, 6f, 6f, 276f, 60f);

            Button b;
            if (worn)
            {
                b = IconLabelButton(crt, "TakeOff", 2, Glyphs.Check, "Worn", "Take off " + piece.Name, () => { progress.UnequipGear(hero.Id, piece.Slot); AudioManager.Play(Sfx.Tap); gearScroll = CurrentGearScroll(); BuildGearBody(); });
            }
            else if (owned)
            {
                b = IconLabelButton(crt, "Wear", 1, null, "Wear", "Wear " + piece.Name, () => { progress.EquipGear(hero.Id, piece.Id); gearScroll = CurrentGearScroll(); BuildGearBody(); });
            }
            else if (piece.CoinPrice > 0)
            {
                b = IconLabelButton(crt, "Buy", 1, art != null ? art.CoinIcon : null, Num(piece.CoinPrice), "Buy " + piece.Name + " for " + Num(piece.CoinPrice) + " coins", () => TapBuyGear(hero, piece));
            }
            else
            {
                Text only = UiKit.AddText(crt, "ChestOnly", "Chests only", 42, Theme.InkSoft, TextAnchor.MiddleCenter);
                DropOutline(only);
                only.horizontalOverflow = HorizontalWrapMode.Overflow;
                StretchBottom(only.rectTransform, 6f, 6f, 24f, 100f);
                return;
            }
            StretchBottom((RectTransform)b.transform, 14f, 14f, 16f, 126f);
        }

        float CurrentGearScroll() { return gearScrollRect != null ? gearScrollRect.verticalNormalizedPosition : 1f; }

        static void StretchTop(RectTransform rt, float left, float right, float fromTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -(fromTop + height));
            rt.offsetMax = new Vector2(-right, -fromTop);
        }

        /// <summary>Tap on a price: not enough coins explains it, otherwise the piece is bought and worn at once.</summary>
        public void TapBuyGear(HeroDef hero, GearDef piece)
        {
            StoreBuyResult r = progress.TryBuyGear(piece.Id);
            switch (r)
            {
                case StoreBuyResult.Ok:
                    progress.EquipGear(hero.Id, piece.Id);
                    AudioManager.Play(Sfx.PowerUp);
                    gearScroll = CurrentGearScroll();
                    BuildGearBody();
                    break;
                case StoreBuyResult.NotEnoughCoins:
                    NeedMoreCoins(piece.CoinPrice, piece.Name);
                    break;
                default:
                    Message("Not available", piece.Name + " cannot be bought right now.");
                    break;
            }
        }

        // ================= Heroes =================

        /// <summary>The 20 characters, 4 per region: pick a region, pick a character, see its ability, select or buy it, or dress it.</summary>
        public void ShowHeroes(string focusId = null)
        {
            HeroDef focus = progress.HeroDefs.Get(focusId ?? progress.SelectedHeroId) ?? progress.HeroDefs.Get(HeroCatalog.DefaultId);
            heroFocusId = focus.Id;
            heroRegionTab = focus.Region;
            NewScreen("Heroes");
            SkyBackground();
            AddBack("Back", ShowMenu);
            AddPillRow();
            heroBody = null;
            BuildHeroBody();
            AddTabBar(ToyKit.TabHeroes);
        }

        public string HeroFocusId { get { return heroFocusId; } }

        static readonly string[] RegionShort = { "Meadow", "Grove", "Beach", "Candy", "Bamboo" };

        const float HeroTabsTop = 178f, HeroThumbsTop = 324f, HeroThumbsH = 250f, HeroPreviewTop = 588f, HeroPreviewH = 340f, HeroInfoTop = 943f;

        void BuildHeroBody()
        {
            if (heroBody != null) { heroBody.gameObject.SetActive(false); Destroy(heroBody.gameObject); }
            heroBody = UiKit.Rect(screen, "HeroBody");
            UiKit.Stretch(heroBody);
            HeroDef focus = progress.HeroDefs.Get(heroFocusId);

            // Region tabs
            RectTransform tabs = UiKit.Rect(heroBody, "RegionTabs");
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.offsetMin = new Vector2(20f, -(HeroTabsTop + 126f));
            tabs.offsetMax = new Vector2(-20f, -HeroTabsTop);
            for (int r = 1; r <= 5; r++)
            {
                int region = r;
                bool on = region == heroRegionTab;
                RegionDef rd = repo.TryGetRegionByIndex(region);
                bool open = rd != null && progress.HeroRegionOpen(progress.HeroDefs.OfRegion(region)[0]);
                Button b = UiKit.AddButton(tabs, "Region" + region, RegionShort[region - 1], ToyKit.Button(on ? 0 : 2), Color.white, new Vector2(180f, 126f), () => { heroRegionTab = region; heroFocusId = FirstHeroOfRegion(region); BuildHeroBody(); }, 40);
                b.GetComponent<A11yLabel>().Text = (rd != null ? rd.Name : "Region " + region) + (open ? "" : ", locked") + (on ? ", open" : "");
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2((r - 1) / 5f, 0f);
                rt.anchorMax = new Vector2(r / 5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(4f, 0f);
                rt.offsetMax = new Vector2(-4f, 0f);
                Text label = b.transform.Find("Label").GetComponent<Text>();
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (!open) b.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.75f);
            }

            // The four characters of the region: a row of four (the shop grid rule: 4 in a row).
            List<HeroDef> four = progress.HeroDefs.OfRegion(heroRegionTab);
            RectTransform thumbs = UiKit.Rect(heroBody, "Thumbs");
            thumbs.anchorMin = new Vector2(0f, 1f);
            thumbs.anchorMax = new Vector2(1f, 1f);
            thumbs.pivot = new Vector2(0.5f, 1f);
            thumbs.offsetMin = new Vector2(20f, -(HeroThumbsTop + HeroThumbsH));
            thumbs.offsetMax = new Vector2(-20f, -HeroThumbsTop);
            for (int i = 0; i < four.Count; i++)
            {
                HeroDef h = four[i];
                bool on = h.Id == focus.Id;
                HeroState st = progress.StateOf(h);
                Button b = UiKit.AddButton(thumbs, "Hero" + h.Name.Replace(" ", ""), "", ToyKit.Button(on ? 0 : 2), Color.white, new Vector2(200f, 200f), () => { heroFocusId = h.Id; BuildHeroBody(); }, 40);
                b.GetComponent<A11yLabel>().Text = h.Name + ", " + RarityInfo.Name(h.Rarity) + ", " + (st == HeroState.Owned ? "owned" : (st == HeroState.ForSale ? "for sale" : "locked")) + (h.Id == progress.SelectedHeroId ? ", selected" : "");
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i / 4f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 4f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(5f, 0f);
                rt.offsetMax = new Vector2(-5f, 0f);
                Image face = UiKit.AddImage(b.transform, "Face", art != null ? art.Player : null, st == HeroState.RegionLocked ? Color.Lerp(GearArt.HeroTint(h), new Color(0.55f, 0.57f, 0.65f, 1f), 0.55f) : GearArt.HeroTint(h));
                face.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                face.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                face.rectTransform.pivot = new Vector2(0.5f, 1f);
                face.rectTransform.anchoredPosition = new Vector2(0f, -14f);
                face.rectTransform.sizeDelta = new Vector2(130f, 130f);
                Text nm = UiKit.AddText(b.transform, "Name", h.Name, 40, UiKit.ButtonInk, TextAnchor.LowerCenter, true);
                DropOutline(nm);
                nm.horizontalOverflow = HorizontalWrapMode.Overflow;
                nm.rectTransform.anchorMin = new Vector2(0f, 0f);
                nm.rectTransform.anchorMax = new Vector2(1f, 0f);
                nm.rectTransform.pivot = new Vector2(0.5f, 0f);
                nm.rectTransform.offsetMin = new Vector2(0f, 10f);
                nm.rectTransform.offsetMax = new Vector2(0f, 70f);
                // State mark in the corner: a tick when owned, a lock when its region is closed (shape, not colour).
                Sprite mark = st == HeroState.Owned ? Glyphs.Check : (st == HeroState.RegionLocked ? ToyKit.Get("icon_lock") : null);
                if (mark != null)
                {
                    Image mk = UiKit.AddImage(b.transform, "State", mark, Color.white);
                    mk.rectTransform.anchorMin = mk.rectTransform.anchorMax = new Vector2(1f, 1f);
                    mk.rectTransform.pivot = new Vector2(1f, 1f);
                    mk.rectTransform.anchoredPosition = new Vector2(-6f, -6f);
                    mk.rectTransform.sizeDelta = new Vector2(52f, 52f);
                }
            }

            // Big preview of the focused character with what it wears.
            RectTransform box = GearArt.BuildDressedMomo(heroBody, "Preview", art != null ? art.Player : null, progress.StateOf(focus) == HeroState.Owned ? focus : focus, progress, focus.Id, HeroPreviewH);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.anchoredPosition = new Vector2(0f, -HeroPreviewTop);
            if (progress.StateOf(focus) == HeroState.RegionLocked)
            {
                foreach (Image im in box.GetComponentsInChildren<Image>()) if (im.name == "Momo") im.color = Color.Lerp(GearArt.HeroTint(focus), new Color(0.55f, 0.57f, 0.65f, 1f), 0.55f);
            }

            // Info: scrolls when the text is long (130 percent text), so nothing can run into the buttons below.
            const float buttonsH = 126f, buttonsGap = 14f;
            float bottom = ToyKit.TabBarReserve + 4f + buttonsH + buttonsGap;
            ScrollRect sr;
            RectTransform info = ScrollArea(heroBody, "HeroInfo", HeroInfoTop, bottom, 100f, out sr);
            info.offsetMin = new Vector2(28f, info.offsetMin.y);
            info.offsetMax = new Vector2(-28f, info.offsetMax.y);
            BuildHeroInfo(info, focus);

            // Buttons: Dress (left) and Select / Buy (right), full width halves in the thumb zone.
            RectTransform bar = UiKit.Rect(heroBody, "HeroButtons");
            StretchBottom(bar, 28f, 28f, ToyKit.TabBarReserve + 4f, buttonsH);
            HeroState state = progress.StateOf(focus);
            bool owned = state == HeroState.Owned;
            Button dress = UiKit.AddButton(bar, "Dress", "Dress", ToyKit.Button(owned ? 2 : 0), Color.white, new Vector2(200f, buttonsH), owned ? (Action)(() => ShowGear(focus.Id)) : null, 44);
            dress.GetComponent<A11yLabel>().Text = owned ? "Dress " + focus.Name : "Dress, own this character first";
            dress.interactable = owned;
            if (!owned) dress.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
            var drt = (RectTransform)dress.transform;
            drt.anchorMin = new Vector2(0f, 0f);
            drt.anchorMax = new Vector2(0.4f, 1f);
            drt.offsetMin = new Vector2(0f, 0f);
            drt.offsetMax = new Vector2(-8f, 0f);

            Button act;
            if (owned)
            {
                bool selected = focus.Id == progress.SelectedHeroId;
                act = IconLabelButton(bar, "Select", selected ? 0 : 1, selected ? Glyphs.Check : null, selected ? "Selected" : "Select", selected ? focus.Name + " is selected" : "Select " + focus.Name, selected ? null : (Action)(() => { progress.SelectHero(focus.Id); AudioManager.Play(Sfx.Tap); BuildHeroBody(); }), 44);
                act.interactable = !selected;
            }
            else if (state == HeroState.ForSale)
            {
                bool coins = focus.CoinPrice > 0;
                int price = coins ? focus.CoinPrice : focus.GemPrice;
                act = IconLabelButton(bar, "Buy", 1, art != null ? (coins ? art.CoinIcon : art.GemIcon) : null, Num(price), "Buy " + focus.Name + " for " + Num(price) + (coins ? " coins" : " gems"), () => TapBuyHero(focus), 44);
            }
            else
            {
                act = IconLabelButton(bar, "Locked", 0, ToyKit.Get("icon_lock"), "Locked", focus.Name + " is locked until its region opens", null, 44);
                act.interactable = false;
                act.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
            }
            var art2 = (RectTransform)act.transform;
            art2.anchorMin = new Vector2(0.4f, 0f);
            art2.anchorMax = new Vector2(1f, 1f);
            art2.offsetMin = new Vector2(8f, 0f);
            art2.offsetMax = new Vector2(0f, 0f);
        }

        string FirstHeroOfRegion(int region)
        {
            List<HeroDef> l = progress.HeroDefs.OfRegion(region);
            if (l.Contains(progress.HeroDefs.Get(progress.SelectedHeroId))) return progress.SelectedHeroId;
            return l[0].Id;
        }

        /// <summary>The character's text card: a vertical flow that grows with its text, so 130 percent text scrolls instead of overlapping.</summary>
        void BuildHeroInfo(RectTransform info, HeroDef h)
        {
            Theme.CardBacking(info);
            foreach (Transform t in info) t.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // the backing is not part of the flow
            var vl = info.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(24, 24, 18, 24);
            vl.spacing = 10f;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            var fit = info.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HeroState st = progress.StateOf(h);
            RegionDef rd = repo.TryGetRegionByIndex(h.Region);
            string status;
            if (st == HeroState.Owned) status = h.Id == progress.SelectedHeroId ? "Selected" : "Owned";
            else if (st == HeroState.ForSale) status = "Price: " + Num(h.CoinPrice > 0 ? h.CoinPrice : h.GemPrice) + (h.CoinPrice > 0 ? " coins" : " gems");
            else status = "Open " + (rd != null ? rd.Name : "region " + h.Region) + " first";

            FlowText(info, "Head", h.Name + " - " + RarityInfo.Name(h.Rarity) + " " + h.Species, 46, Theme.Ink, true);
            FlowText(info, "Ability", h.AbilityName + ": " + h.AbilityText, 42, Theme.Ink, false);
            FlowText(info, "Stats", h.StatsText, 42, Theme.InkSoft, false);
            FlowText(info, "State", status, 44, st == HeroState.Owned ? new Color(0.03f, 0.48f, 0.27f, 1f) : Theme.Ink, true);
            if (!HeroCatalog.AbilitiesActive) FlowText(info, "Note", "Abilities and bonuses switch on in a later update.", 42, Theme.InkSoft, false);
        }

        static Text FlowText(Transform parent, string name, string text, int size, Color color, bool display)
        {
            Text t = UiKit.AddText(parent, name, text, size, color, TextAnchor.UpperLeft, display);
            DropOutline(t);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        public void TapBuyHero(HeroDef hero)
        {
            StoreBuyResult r = progress.TryBuyHero(hero.Id);
            bool coins = hero.CoinPrice > 0;
            switch (r)
            {
                case StoreBuyResult.Ok:
                    progress.SelectHero(hero.Id);
                    AudioManager.Play(Sfx.PowerUp);
                    BuildHeroBody();
                    break;
                case StoreBuyResult.NotEnoughCoins:
                    NeedMoreCoins(hero.CoinPrice, hero.Name);
                    break;
                case StoreBuyResult.NotEnoughGems:
                    Message("Not enough gems", "You need " + Num(hero.GemPrice) + " gems for " + hero.Name + " and have " + Num(progress.Gems) + ".\nGet gems in the shop.");
                    break;
                case StoreBuyResult.Locked:
                    Message("Locked", hero.Name + " is for sale once its region is open.");
                    break;
                default:
                    Message("Not available", hero.Name + " cannot be bought right now.");
                    break;
            }
        }
    }
}
