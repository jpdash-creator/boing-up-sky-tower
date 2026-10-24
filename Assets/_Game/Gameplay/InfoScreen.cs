using System.Collections.Generic;
using System.Globalization;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>One power-up row of the info screen: its in-game value before and after the worn gear, and the worn pieces that boost it.</summary>
    public sealed class InfoRow
    {
        public PowerUp Type;
        /// <summary>The value in the unit of <see cref="Seconds"/>: seconds of effect, or the magnet's extra pull radius.</summary>
        public float Before, After;
        public bool Seconds;
        public int Percent;
        public readonly List<GearDef> Pieces = new List<GearDef>(4);
    }

    /// <summary>
    /// The numbers behind the info screen. The value of a power-up is read from the very SimConfig a run uses (<see cref="ProgressService.BuildRunConfig"/>):
    /// the field the gear boost really scales (see <see cref="GearBoosts.Apply"/>), so what the screen shows is what the run gives.
    /// </summary>
    public static class GearInfo
    {
        /// <summary>The in-game value a boost changes: effect seconds for the timed power-ups, the extra pull radius for the Magnet.</summary>
        public static float Value(SimConfig cfg, PowerUp p, out bool seconds)
        {
            seconds = true;
            const float tps = SimConfig.TicksPerSecond;
            switch (p)
            {
                case PowerUp.Flight: return cfg.FlightTicks / tps;
                case PowerUp.Rocket: return cfg.RocketTicks / tps;
                case PowerUp.DoubleJump: return cfg.DoubleJumpWindowTicks / tps;
                case PowerUp.WallStick: return cfg.WallStickWindowTicks / tps;
                case PowerUp.Shield: return cfg.ShieldTicks / tps;
                case PowerUp.SlowTime: return cfg.SlowTimeTicks / tps;
                case PowerUp.Magnet:
                    seconds = false;
                    return cfg.MagnetExtraRadius.F();
                default: return 0f;
            }
        }

        /// <summary>One row per implemented power-up with the value before gear (the base config) and after (the run config built from the worn gear).</summary>
        public static List<InfoRow> Rows(ProgressService progress, SimConfig baseCfg)
        {
            var rows = new List<InfoRow>(8);
            SimConfig run = progress.BuildRunConfig(baseCfg);
            int[] boost = progress.GearBoostPercents();
            foreach (PowerUpDef def in progress.PowerUpDefs.Implemented)
            {
                var r = new InfoRow { Type = def.Type, Percent = boost[(int)def.Type] };
                r.Before = Value(baseCfg, def.Type, out r.Seconds);
                r.After = Value(run, def.Type, out r.Seconds);
                for (int s = 0; s < GearCatalog.SlotCount; s++)
                {
                    GearDef d = progress.EquippedDef((GearSlot)s);
                    if (d != null && d.Boost == def.Type) r.Pieces.Add(d);
                }
                rows.Add(r);
            }
            return rows;
        }

        public static string Format(float v, bool seconds)
        {
            return v.ToString("0.0", CultureInfo.InvariantCulture) + (seconds ? " s" : "");
        }
    }

    /// <summary>
    /// The character info screen (owner issue 21.5): the character on the land stage, then per power-up an icon row with its value before and after the worn gear
    /// and a plus percent badge; tapping a row shows the worn pieces that boost it; a strip sums up the worn gear. Icons and numbers only. Menu screen.
    /// </summary>
    public sealed class InfoScreen
    {
        const float RowsTop = 600f, RowH = 100f, RowGap = 8f, DetailH = 130f, StripH = 120f, Side = 20f;

        readonly RectTransform screen;
        readonly ProgressService progress;
        readonly ArtCatalog art;
        readonly GearHost host;
        RectTransform body;

        public PowerUp? Selected { get; private set; }
        public List<InfoRow> Rows { get; private set; }
        public Text CoinText { get; private set; }
        public Text GemText { get; private set; }

        public InfoScreen(RectTransform screen, ProgressService progress, ArtCatalog art, GearHost host)
        {
            this.screen = screen;
            this.progress = progress;
            this.art = art;
            this.host = host;
        }

        public void Build()
        {
            if (body != null) UnityEngine.Object.Destroy(body.gameObject);
            if (host.Stage != null && host.Stage.Root != null)
            {
                for (int i = host.Stage.Root.childCount - 1; i >= 0; i--)
                    if (host.Stage.Root.GetChild(i).name == "Doll") UnityEngine.Object.Destroy(host.Stage.Root.GetChild(i).gameObject);
                CharacterDef who = CharacterCatalog.Selected;
                RectTransform doll = MomoDoll.Build(host.Stage.Root, "Doll", who.Body(art), progress, 380f, who);
                host.Stage.Stand(doll);
            }
            body = UiKit.Rect(screen, "InfoBody");
            UiKit.Stretch(body);

            RectTransform wallet = UiKit.Rect(body, "Wallet");
            wallet.anchorMin = new Vector2(0f, 1f);
            wallet.anchorMax = new Vector2(1f, 1f);
            wallet.pivot = new Vector2(0.5f, 1f);
            wallet.offsetMin = new Vector2(190f, -118f);
            wallet.offsetMax = new Vector2(-16f, -18f);
            var hl = wallet.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            CoinText = ToyKit.WalletPillFlex(wallet, "CoinsPill", art != null ? art.CoinIcon : null, Color.white, 100f);
            GemText = ToyKit.WalletPillFlex(wallet, "GemsPill", art != null ? art.GemIcon : null, new Color(0.75f, 0.92f, 1f), 100f);
            UiKit.SetText(CoinText, progress.Coins.ToString());
            UiKit.SetText(GemText, progress.Gems.ToString());

            Rows = GearInfo.Rows(progress, GameSession.Cfg);
            float y = RowsTop;
            for (int i = 0; i < Rows.Count; i++)
            {
                InfoRow r = Rows[i];
                bool open = Selected.HasValue && Selected.Value == r.Type;
                BuildRow(r, y, open);
                y += RowH + RowGap;
                if (open)
                {
                    BuildDetail(r, y);
                    y += DetailH + RowGap;
                }
            }
            BuildStrip(y + 8f);
        }

        /// <summary>Tapping a row opens the pieces that boost it; tapping it again closes them.</summary>
        public void Select(PowerUp type)
        {
            Selected = Selected.HasValue && Selected.Value == type ? (PowerUp?)null : type;
            Build();
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Text Num(Transform parent, string name, string text, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Text t = UiKit.AddText(parent, name, text, 48, color, align, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            Anchor((RectTransform)t.transform, anchor, pos, size);
            return t;
        }

        void BuildRow(InfoRow r, float yTop, bool open)
        {
            Image row = UiKit.AddImage(body, "Row" + r.Type, Theme.RoundSprite, open ? new Color(0.20f, 0.30f, 0.56f, 1f) : new Color(0.10f, 0.14f, 0.30f, 0.94f));
            row.type = Image.Type.Sliced;
            row.preserveAspect = false;
            row.pixelsPerUnitMultiplier = 2.2f;
            Anchor(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -yTop), new Vector2(-2f * Side, RowH));
            row.rectTransform.anchorMin = new Vector2(0f, 1f);
            row.rectTransform.anchorMax = new Vector2(1f, 1f);
            row.rectTransform.pivot = new Vector2(0.5f, 1f);
            row.rectTransform.offsetMin = new Vector2(Side, -(yTop + RowH));
            row.rectTransform.offsetMax = new Vector2(-Side, -yTop);
            row.raycastTarget = true;
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            PowerUp type = r.Type;
            b.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); Select(type); });
            string spoken = PowerUpUi.Name(r.Type) + ", " + GearInfo.Format(r.Before, r.Seconds) + " to " + GearInfo.Format(r.After, r.Seconds) + (r.Percent > 0 ? ", plus " + r.Percent + " percent" : "");
            row.gameObject.AddComponent<A11yLabel>().Text = spoken;

            Image ic = PowerUpUi.AddIcon(row.transform, "Icon", r.Type, new Vector2(0f, 0.5f), new Vector2(14f, 0f), 84f, r.Percent <= 0);
            ic.rectTransform.pivot = new Vector2(0f, 0.5f);

            bool boosted = r.Percent > 0;
            Color dim = new Color(0.78f, 0.84f, 1f, 1f);
            Num(row.transform, "Before", GearInfo.Format(r.Before, r.Seconds), boosted ? dim : Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(116f, 0f), new Vector2(210f, 80f));
            if (boosted)
            {
                Image arrow = UiKit.AddImage(row.transform, "Arrow", Glyphs.Left, Color.white);
                arrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                Anchor(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(338f, 0f), new Vector2(56f, 56f));
                arrow.rectTransform.pivot = new Vector2(0f, 0.5f);
                Num(row.transform, "After", GearInfo.Format(r.After, r.Seconds), new Color(0.55f, 1f, 0.62f, 1f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(408f, 0f), new Vector2(210f, 80f));
                Image badge = UiKit.AddImage(row.transform, "Badge", Theme.RoundSprite, new Color(0.17f, 0.74f, 0.47f, 1f));
                badge.type = Image.Type.Sliced;
                badge.preserveAspect = false;
                Anchor(badge.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(170f, 76f));
                badge.rectTransform.pivot = new Vector2(1f, 0.5f);
                Text pt = UiKit.AddText(badge.transform, "Percent", "+" + r.Percent + "%", 48, UiKit.ButtonInk, TextAnchor.MiddleCenter, true);
                pt.horizontalOverflow = HorizontalWrapMode.Overflow;
                var po = pt.GetComponent<Outline>();
                if (po != null) po.enabled = false;
                UiKit.Stretch((RectTransform)pt.transform);
            }
        }

        /// <summary>The worn pieces that boost the opened power-up, as gear tiles with their rarity pips (a dim cross when nothing worn boosts it).</summary>
        void BuildDetail(InfoRow r, float yTop)
        {
            RectTransform strip = UiKit.Rect(body, "Detail");
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.offsetMin = new Vector2(Side, -(yTop + DetailH));
            strip.offsetMax = new Vector2(-Side, -yTop);
            const float tile = 122f;
            if (r.Pieces.Count == 0)
            {
                Image none = UiKit.AddImage(strip, "None", Glyphs.Cross, new Color(1f, 1f, 1f, 0.45f));
                Anchor(none.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
                none.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                return;
            }
            float total = r.Pieces.Count * tile + (r.Pieces.Count - 1) * 16f;
            for (int i = 0; i < r.Pieces.Count; i++)
            {
                GearDef d = r.Pieces[i];
                RectTransform t = GearUi.Tile(strip, "Piece" + i, d, GearTileState.Owned, progress.GearLevel(d.Id), 1, new Vector2(tile, tile));
                Anchor(t, new Vector2(0.5f, 0.5f), new Vector2(-total * 0.5f + i * (tile + 16f), 0f), new Vector2(tile, tile));
                t.pivot = new Vector2(0f, 0.5f);
                t.anchoredPosition = new Vector2(-total * 0.5f + i * (tile + 16f), 0f);
            }
        }

        /// <summary>Totals of what is worn: pieces worn of four, the best rarity (frame colour and pips) and the summed boost of all power-ups.</summary>
        void BuildStrip(float yTop)
        {
            int worn = 0, best = -1, sum = 0;
            for (int s = 0; s < GearCatalog.SlotCount; s++)
            {
                GearDef d = progress.EquippedDef((GearSlot)s);
                if (d == null) continue;
                worn++;
                if ((int)d.Rarity > best) best = (int)d.Rarity;
            }
            foreach (InfoRow r in Rows) sum += r.Percent;

            Image strip = UiKit.AddImage(body, "Totals", Theme.RoundSprite, new Color(0.08f, 0.11f, 0.26f, 0.96f));
            strip.type = Image.Type.Sliced;
            strip.preserveAspect = false;
            strip.pixelsPerUnitMultiplier = 2.2f;
            strip.raycastTarget = false;
            strip.rectTransform.anchorMin = new Vector2(0f, 1f);
            strip.rectTransform.anchorMax = new Vector2(1f, 1f);
            strip.rectTransform.pivot = new Vector2(0.5f, 1f);
            strip.rectTransform.offsetMin = new Vector2(Side, -(yTop + StripH));
            strip.rectTransform.offsetMax = new Vector2(-Side, -yTop);
            strip.gameObject.AddComponent<A11yLabel>().Text = "Worn " + worn + " of 4 pieces" + (best >= 0 ? ", best " + GearCatalog.RarityNames[best] : "") + ", total boost " + sum + " percent";

            // pieces worn: a shirt glyph and "n/4"
            Image g = UiKit.AddImage(strip.transform, "WornIcon", Glyphs.Shirt, Color.white);
            Anchor(g.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(72f, 72f));
            g.rectTransform.pivot = new Vector2(0f, 0.5f);
            Num(strip.transform, "Worn", worn + "/4", Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(150f, 80f));

            // best rarity: a swatch in its colour with its pips
            Color rc = best >= 0 ? GearUi.Fill((GearRarity)best) : new Color(0.3f, 0.34f, 0.5f, 1f);
            Image sw = UiKit.AddImage(strip.transform, "BestRarity", Theme.RoundSprite, rc);
            sw.type = Image.Type.Sliced;
            sw.preserveAspect = false;
            Anchor(sw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-40f, 0f), new Vector2(230f, 76f));
            sw.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            for (int i = 0; i <= best; i++)
            {
                Image p = UiKit.AddImage(sw.transform, "Pip" + i, Theme.RoundSprite, Color.white);
                p.type = Image.Type.Sliced;
                p.preserveAspect = false;
                Anchor(p.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f + i * 40f, 0f), new Vector2(28f, 28f));
                p.rectTransform.pivot = new Vector2(0f, 0.5f);
            }

            // summed boost
            Image bolt = UiKit.AddImage(strip.transform, "BoostIcon", ToyKit.Get("icon_bolt"), Color.white);
            Anchor(bolt.rectTransform, new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(72f, 72f));
            bolt.rectTransform.pivot = new Vector2(1f, 0.5f);
            Num(strip.transform, "Sum", "+" + sum + "%", new Color(0.55f, 1f, 0.62f, 1f), TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(200f, 80f));
        }
    }
}
