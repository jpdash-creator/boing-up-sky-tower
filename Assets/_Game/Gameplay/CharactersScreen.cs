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
            // a soft round spot of light under Momo
            Image spot = UiKit.AddImage(body, "Spot", Theme.ShadowSprite, new Color(1f, 0.95f, 0.75f, 0.55f));
            spot.preserveAspect = false;
            spot.rectTransform.anchorMin = spot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            spot.rectTransform.sizeDelta = new Vector2(820f, 300f);
            spot.rectTransform.anchoredPosition = new Vector2(0f, -330f);

            RectTransform doll = MomoDoll.Build(body, "Doll", art != null ? art.Player : null, progress, 980f);
            Anchor(doll, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), doll.sizeDelta);
            doll.pivot = new Vector2(0.5f, 0.5f);

            // inventory (left) and more characters (right): the two round icon buttons
            Button inv = ToyKit.IconButton(body, "Inventory", "Inventory", 1, Glyphs.Shirt, new Vector2(170f, 170f), () => { if (host.OpenInventory != null) host.OpenInventory(); }, 0.6f);
            Anchor((RectTransform)inv.transform, new Vector2(0.5f, 0f), new Vector2(-130f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)inv.transform).pivot = new Vector2(0.5f, 0f);
            Button more = ToyKit.IconButton(body, "MoreCharacters", "More characters", 0, ToyKit.Get("icon_heroes"), new Vector2(170f, 170f), () => Build(true), 0.6f);
            Anchor((RectTransform)more.transform, new Vector2(0.5f, 0f), new Vector2(130f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)more.transform).pivot = new Vector2(0.5f, 0f);
        }

        /// <summary>The roster: Momo (chosen) and locked silhouettes until more characters exist.</summary>
        void BuildRoster()
        {
            float w = 190f, gap = 16f;
            for (int i = 0; i < RosterSlots; i++)
            {
                int r = i / Columns, c = i % Columns;
                bool momo = i == 0;
                Image tile = UiKit.AddImage(body, momo ? "Momo" : "Locked" + i, Theme.RoundSprite, momo ? new Color(0.30f, 0.58f, 1f, 1f) : new Color(0.10f, 0.13f, 0.27f, 0.92f));
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
                int idx = i;
                b.onClick.AddListener(() => { if (idx == 0) Build(false); });
                tile.gameObject.AddComponent<A11yLabel>().Text = momo ? "Momo, chosen" : "Locked character";
                Image face = UiKit.AddImage(tile.transform, "Face", art != null ? art.Player : null, momo ? Color.white : new Color(0.03f, 0.04f, 0.10f, 0.95f));
                face.rectTransform.anchorMin = new Vector2(0.1f, 0.08f);
                face.rectTransform.anchorMax = new Vector2(0.9f, 0.92f);
                face.rectTransform.offsetMin = face.rectTransform.offsetMax = Vector2.zero;
                if (!momo)
                {
                    Image lk = UiKit.AddImage(tile.transform, "Lock", ToyKit.Get("icon_lock"), Color.white);
                    Anchor(lk.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w * 0.42f, w * 0.42f));
                    lk.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                }
                else
                {
                    Image tick = UiKit.AddImage(tile.transform, "Chosen", Glyphs.Check, Color.white);
                    Anchor(tick.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(w * 0.3f, w * 0.3f));
                }
            }
            // back to the stage
            Button back = ToyKit.GlyphButton(body, "ToStage", "Back to Momo", 2, Glyphs.Left, new Vector2(170f, 170f), () => Build(false));
            Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, ToyKit.TabBarReserve + 40f), new Vector2(170f, 170f));
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0f);
        }
    }
}
