using System;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The character screen (owner reference 3, docs/17 section A): Momo big in the middle on a calm scene with two round icon buttons, the inventory and
    /// the roster of more characters. Only Momo exists, so the roster shows him as the chosen one and the rest as locked silhouettes. No words.
    /// </summary>
    public sealed class CharactersScreen
    {
        const int RosterSlots = 8, Columns = 4;
        /// <summary>Doll height on screen: about 29 percent of the 1920 px reference, a touch larger than Momo in the run.</summary>
        public const float DollH = 560f;

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly GearHost host;
        RectTransform body;

        public bool RosterOpen { get; private set; }
        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }

        public CharactersScreen(RectTransform screen, ProgressService progress, ArtCatalog art, GearHost host)
        {
            this.screen = screen;
            this.progress = progress;
            this.art = art;
            this.host = host;
        }

        public void Build(bool roster = false)
        {
            RosterOpen = roster;
            if (body != null) UnityEngine.Object.Destroy(body.gameObject);
            ClearStage();
            body = UiKit.Rect(screen, "CharactersBody");
            UiKit.Stretch(body);

            RectTransform row = UiKit.Rect(body, "Wallet");
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

            if (roster) BuildRoster();
            else BuildStage();
        }

        /// <summary>The doll lives on the stage (not in the screen body), so it is removed when the screen is rebuilt.</summary>
        void ClearStage()
        {
            if (host.Stage == null || host.Stage.Root == null) return;
            for (int i = host.Stage.Root.childCount - 1; i >= 0; i--)
            {
                string n = host.Stage.Root.GetChild(i).name;
                if (n == "Doll" || n == "TestTag") UnityEngine.Object.Destroy(host.Stage.Root.GetChild(i).gameObject);
            }
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>Momo in the middle of the stage and the two round buttons under him.</summary>
        void BuildStage()
        {
            // the chosen character stands on the dirt oval of the land stage, a little larger than in the run so the fit of the gear can be seen
            CharacterDef who = CharacterCatalog.Selected;
            RectTransform doll = CharacterDoll.Build(host.Stage.Root, "Doll", progress, DollH, who, art);
            host.Stage.Stand(doll);
            if (who.IsTest) AddTestTag(host.Stage.Root, new Vector2(-330f, -(host.Stage.OvalY - 30f)));

            // inventory (left), info (middle) and more characters (right): the round icon buttons
            Button inv = ToyKit.IconButton(body, "Inventory", "Inventory", 1, Glyphs.Shirt, new Vector2(170f, 170f), () => { if (host.OpenInventory != null) host.OpenInventory(); }, 0.6f);
            Anchor((RectTransform)inv.transform, new Vector2(0.5f, 0f), new Vector2(-250f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)inv.transform).pivot = new Vector2(0.5f, 0f);
            Button info = ToyKit.IconButton(body, "Info", "Character info: what my gear boosts", 2, ToyKit.Get("icon_bolt"), new Vector2(170f, 170f), () => { if (host.OpenInfo != null) host.OpenInfo(); }, 0.6f);
            Anchor((RectTransform)info.transform, new Vector2(0.5f, 0f), new Vector2(0f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)info.transform).pivot = new Vector2(0.5f, 0f);
            Button more = ToyKit.IconButton(body, "MoreCharacters", "More characters", 0, ToyKit.Get("icon_heroes"), new Vector2(170f, 170f), () => Build(true), 0.6f);
            Anchor((RectTransform)more.transform, new Vector2(0.5f, 0f), new Vector2(250f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)more.transform).pivot = new Vector2(0.5f, 0f);
        }

        /// <summary>Small TEST tag (development builds only) under the test character, never shipped.</summary>
        public static void AddTestTag(RectTransform parent, Vector2 topPos)
        {
            Image tag = UiKit.AddPanel(parent, "TestTag", new Color(0.95f, 0.25f, 0.25f, 0.95f));
            RectTransform rt = tag.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(150f, 56f);
            rt.anchoredPosition = topPos;
            Text t = UiKit.AddText(tag.transform, "Label", "TEST", 40, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch((RectTransform)t.transform);
        }

        /// <summary>The roster: the available characters (the chosen one ticked) and locked silhouettes until more characters exist. Production shows Momo only.</summary>
        void BuildRoster()
        {
            float w = 190f, gap = 16f;
            CharacterDef[] chars = CharacterCatalog.Available();
            CharacterDef chosen = CharacterCatalog.Selected;
            for (int i = 0; i < RosterSlots; i++)
            {
                int r = i / Columns, c = i % Columns;
                CharacterDef who = i < chars.Length ? chars[i] : null;
                bool open = who != null && who.Selectable;   // a test character without its parts is listed but locked
                bool isChosen = open && who.Id == chosen.Id;
                Image tile = UiKit.AddImage(body, who != null ? who.Name : "Locked" + i, Theme.RoundSprite, open ? new Color(0.30f, 0.58f, 1f, 1f) : new Color(0.10f, 0.13f, 0.27f, 0.92f));
                tile.type = Image.Type.Sliced;
                tile.preserveAspect = false;
                tile.pixelsPerUnitMultiplier = 1.2f;
                float total = Columns * w + (Columns - 1) * gap;
                Anchor(tile.rectTransform, new Vector2(0.5f, 1f), new Vector2(-total * 0.5f + c * (w + gap), -(240f + r * (w + gap))), new Vector2(w, w));
                tile.rectTransform.pivot = new Vector2(0f, 1f);
                tile.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + c * (w + gap), -(240f + r * (w + gap)));
                var b = tile.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                tile.raycastTarget = true;
                CharacterDef pick = who;
                b.onClick.AddListener(() => { if (pick != null && pick.Selectable) { CharacterCatalog.Selected = pick; Build(false); } });
                tile.gameObject.AddComponent<A11yLabel>().Text = open ? who.Name + (isChosen ? ", chosen" : "") : (who != null ? who.Name + ", parts missing" : "Locked character");
                if (open)
                {
                    // the character's own parts, no gear
                    RectTransform face = CharacterDoll.Build(tile.transform, "Face", null, w * 0.66f, who, art);
                    face.anchorMin = face.anchorMax = new Vector2(0.5f, 1f);
                    face.pivot = new Vector2(0.5f, 1f);
                    face.anchoredPosition = new Vector2(0f, -w * 0.04f);
                }
                else
                {
                    Image face = UiKit.AddImage(tile.transform, "Face", art != null ? art.Player : null, new Color(0.03f, 0.04f, 0.10f, 0.95f));
                    face.rectTransform.anchorMin = new Vector2(0.1f, 0.08f);
                    face.rectTransform.anchorMax = new Vector2(0.9f, 0.92f);
                    face.rectTransform.offsetMin = face.rectTransform.offsetMax = Vector2.zero;
                }
                if (!open)
                {
                    Image lk = UiKit.AddImage(tile.transform, "Lock", ToyKit.Get("icon_lock"), Color.white);
                    Anchor(lk.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w * 0.42f, w * 0.42f));
                    lk.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                }
                else if (isChosen)
                {
                    Image tick = UiKit.AddImage(tile.transform, "Chosen", Glyphs.Check, Color.white);
                    Anchor(tick.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(w * 0.3f, w * 0.3f));
                }
                if (who != null && who.IsTest)
                {
                    Text t = UiKit.AddText(tile.transform, "Test", "TEST", 40, Color.white, TextAnchor.MiddleCenter, true);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var trt = (RectTransform)t.transform;
                    trt.anchorMin = new Vector2(0f, 0f);
                    trt.anchorMax = new Vector2(1f, 0.28f);
                    trt.offsetMin = trt.offsetMax = Vector2.zero;
                }
            }
            // back to the stage
            Button back = ToyKit.GlyphButton(body, "ToStage", "Back to Momo", 2, Glyphs.Left, new Vector2(170f, 170f), () => Build(false));
            Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0f);
        }
    }
}
