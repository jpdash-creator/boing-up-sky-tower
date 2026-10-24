using System;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The character screen: the chosen character on the green land stage and one round button that opens the roster of the characters the player has
    /// (bought or received). The rest of the roster are locked silhouettes. No words.
    /// </summary>
    public sealed class CharactersScreen
    {
        const int RosterSlots = 8, Columns = 4;
        /// <summary>Doll height on screen: about 29 percent of the 1920 px reference, a touch larger than Momo in the run.</summary>
        public const float DollH = 560f;

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly Host host;
        RectTransform body;

        public bool RosterOpen { get; private set; }
        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }

        /// <summary>What the screen needs from the menu: the land stage it stands on.</summary>
        public sealed class Host
        {
            public Stage Stage;
        }

        public CharactersScreen(RectTransform screen, ProgressService progress, ArtCatalog art, Host host)
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
                if (n == "Doll") UnityEngine.Object.Destroy(host.Stage.Root.GetChild(i).gameObject);
            }
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>The chosen character in the middle of the stage and the roster button under it.</summary>
        void BuildStage()
        {
            // the chosen character stands on the dirt oval of the land stage, a little larger than in the run
            CharacterDef who = CharacterCatalog.Selected;
            RectTransform doll = MomoDoll.Build(host.Stage.Root, "Doll", DollH, who, art);
            host.Stage.Stand(doll);

            Button more = ToyKit.IconButton(body, "MoreCharacters", "Choose a character", 0, ToyKit.Get("icon_heroes"), new Vector2(170f, 170f), () => Build(true), 0.6f);
            Anchor((RectTransform)more.transform, new Vector2(0.5f, 0f), new Vector2(0f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)more.transform).pivot = new Vector2(0.5f, 0f);
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
                bool open = who != null;
                bool isChosen = open && who.Id == chosen.Id;
                Image tile = UiKit.AddImage(body, open ? who.Name : "Locked" + i, Theme.RoundSprite, open ? new Color(0.30f, 0.58f, 1f, 1f) : new Color(0.10f, 0.13f, 0.27f, 0.92f));
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
                b.onClick.AddListener(() => { if (pick != null) { CharacterCatalog.Selected = pick; Build(false); } });
                tile.gameObject.AddComponent<A11yLabel>().Text = open ? who.Name + (isChosen ? ", chosen" : "") : "Locked character";
                if (open)
                {
                    RectTransform face = MomoDoll.Build(tile.transform, "Face", w * 0.66f, who, art);
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
                else
                {
                    if (isChosen)
                    {
                        Image tick = UiKit.AddImage(tile.transform, "Chosen", Glyphs.Check, Color.white);
                        Anchor(tick.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(w * 0.3f, w * 0.3f));
                    }
                }
            }
            // back to the stage
            Button back = ToyKit.GlyphButton(body, "ToStage", "Back to Momo", 2, Glyphs.Left, new Vector2(170f, 170f), () => Build(false));
            Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0f);
        }
    }
}
