using System;
using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>The rectangle every character sits in: a rounded box in the rarity colour with a seam (border) around it (docs/21).</summary>
    public static class RarityUi
    {
        /// <summary>Common light grey, Uncommon light green, Rare blue, Epic purple, Legendary yellow gold.</summary>
        public static Color Fill(CharacterRarity r)
        {
            switch (r)
            {
                case CharacterRarity.Uncommon: return new Color(0.70f, 0.90f, 0.62f, 1f);
                case CharacterRarity.Rare: return new Color(0.33f, 0.58f, 0.98f, 1f);
                case CharacterRarity.Epic: return new Color(0.62f, 0.36f, 0.88f, 1f);
                case CharacterRarity.Legendary: return new Color(1f, 0.82f, 0.22f, 1f);
                default: return new Color(0.84f, 0.85f, 0.87f, 1f);
            }
        }

        /// <summary>White seam for Common, Uncommon and Rare; golden seam for Epic and Legendary.</summary>
        public static Color Seam(CharacterRarity r)
        {
            return r >= CharacterRarity.Epic ? new Color(0.86f, 0.60f, 0.06f, 1f) : Color.white;
        }

        /// <summary>A box with its seam; returns the inner area (children go there). <paramref name="size"/> is the outer size.</summary>
        public static RectTransform Box(Transform parent, string name, CharacterRarity rarity, bool locked, Vector2 size)
        {
            Image outer = UiKit.AddImage(parent, name, Theme.RoundSprite, locked ? new Color(0.45f, 0.48f, 0.60f, 1f) : Seam(rarity));
            outer.type = Image.Type.Sliced;
            outer.preserveAspect = false;
            outer.pixelsPerUnitMultiplier = 1.4f;
            outer.rectTransform.sizeDelta = size;
            Image inner = UiKit.AddImage(outer.transform, "Fill", Theme.RoundSprite, locked ? new Color(0.11f, 0.14f, 0.28f, 0.96f) : Fill(rarity));
            inner.type = Image.Type.Sliced;
            inner.preserveAspect = false;
            inner.pixelsPerUnitMultiplier = 1.6f;
            inner.raycastTarget = false;
            inner.rectTransform.anchorMin = Vector2.zero;
            inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = new Vector2(8f, 8f);
            inner.rectTransform.offsetMax = new Vector2(-8f, -8f);
            return inner.rectTransform;
        }
    }

    /// <summary>
    /// The character screen (docs/21): the chosen character on the green land stage on top, and below it a scrolling list of characters in rarity boxes,
    /// five to a row. Owned ones are in colour (tap to choose), the rest are dark with a lock. No words.
    /// </summary>
    public sealed class CharactersScreen
    {
        const int Columns = 5, Slots = 25;
        const float Tile = 190f, Gap = 14f;
        /// <summary>Character height on the stage.</summary>
        public const float DollH = 420f;

        /// <summary>What the screen needs from the menu: the land stage it stands on.</summary>
        public sealed class Host
        {
            public Stage Stage;
        }

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly Host host;
        RectTransform body;

        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }

        public CharactersScreen(RectTransform screen, ProgressService progress, ArtCatalog art, Host host)
        {
            this.screen = screen;
            this.progress = progress;
            this.art = art;
            this.host = host;
        }

        public void Build()
        {
            if (body != null) UnityEngine.Object.Destroy(body.gameObject);
            ClearStage();
            body = UiKit.Rect(screen, "CharactersBody");
            UiKit.Stretch(body);

            // the dark panel under the stage holds the list
            Image panel = UiKit.AddImage(body, "ListPanel", null, new Color(0.17f, 0.23f, 0.42f, 1f));
            panel.preserveAspect = false;
            panel.raycastTarget = false;
            panel.rectTransform.anchorMin = Vector2.zero;
            panel.rectTransform.anchorMax = Vector2.one;
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = new Vector2(0f, -host.Stage.GroundBottom);

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

            BuildStage();
            BuildList();
        }

        /// <summary>The doll lives on the stage (not in the screen body), so it is removed when the screen is rebuilt.</summary>
        void ClearStage()
        {
            if (host.Stage == null || host.Stage.Root == null) return;
            for (int i = host.Stage.Root.childCount - 1; i >= 0; i--)
            {
                if (host.Stage.Root.GetChild(i).name == "Doll") UnityEngine.Object.Destroy(host.Stage.Root.GetChild(i).gameObject);
            }
        }

        /// <summary>The chosen character on the dirt oval.</summary>
        void BuildStage()
        {
            RectTransform doll = MomoDoll.Build(host.Stage.Root, "Doll", DollH, CharacterCatalog.Selected, art);
            host.Stage.Stand(doll);
        }

        void BuildList()
        {
            RectTransform vp = UiKit.Rect(body, "ListViewport");
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(0f, ToyKit.TabBarReserve + 8f);
            vp.offsetMax = new Vector2(0f, -(host.Stage.GroundBottom + 18f));
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            float width = Columns * Tile + (Columns - 1) * Gap;
            int rows = (Slots + Columns - 1) / Columns;
            RectTransform content = UiKit.Rect(vp, "ListContent");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(width, rows * (Tile + Gap));
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = vp;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 60f;

            CharacterDef chosen = CharacterCatalog.Selected;
            for (int i = 0; i < Slots; i++)
            {
                CharacterDef who = i < CharacterCatalog.All.Length ? CharacterCatalog.All[i] : null;
                bool owned = who != null && CharacterCatalog.Owns(progress, who);
                CharacterRarity rarity = who != null ? who.Rarity : CharacterRarity.Common;
                RectTransform inner = RarityUi.Box(content, who != null ? "Char_" + who.Id : "Locked" + i, rarity, !owned, new Vector2(Tile, Tile));
                RectTransform outer = (RectTransform)inner.parent;
                outer.anchorMin = outer.anchorMax = new Vector2(0f, 1f);
                outer.pivot = new Vector2(0f, 1f);
                outer.anchoredPosition = new Vector2((i % Columns) * (Tile + Gap), -(i / Columns) * (Tile + Gap));

                if (owned)
                {
                    RectTransform face = MomoDoll.Build(inner, "Face", Tile * 0.78f, who, art);
                    face.anchorMin = face.anchorMax = new Vector2(0.5f, 0.5f);
                    face.pivot = new Vector2(0.5f, 0.5f);
                    face.anchoredPosition = Vector2.zero;
                    if (who == chosen)
                    {
                        Image tick = UiKit.AddImage(outer, "Chosen", Glyphs.Check, Color.white);
                        tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(1f, 1f);
                        tick.rectTransform.pivot = new Vector2(1f, 1f);
                        tick.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
                        tick.rectTransform.sizeDelta = new Vector2(56f, 56f);
                    }
                }
                else
                {
                    Image lk = UiKit.AddImage(inner, "Lock", ToyKit.Get("icon_lock"), Color.white);
                    lk.rectTransform.anchorMin = lk.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    lk.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    lk.rectTransform.anchoredPosition = Vector2.zero;
                    lk.rectTransform.sizeDelta = new Vector2(Tile * 0.4f, Tile * 0.4f);
                }

                var b = outer.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                outer.GetComponent<Image>().raycastTarget = true;
                CharacterDef pick = who;
                if (owned) b.onClick.AddListener(() => { CharacterCatalog.Selected = pick; Build(); });
                outer.gameObject.AddComponent<A11yLabel>().Text = owned
                    ? who.Name + ", " + CharacterDefs.RarityNames[(int)rarity] + (who == chosen ? ", chosen" : "")
                    : "Locked character";
            }
        }

        /// <summary>Picks a character as the tests do (the same as a tap on its tile).</summary>
        public void Choose(CharacterDef who)
        {
            if (!CharacterCatalog.Owns(progress, who)) return;
            CharacterCatalog.Selected = who;
            Build();
        }
    }
}
