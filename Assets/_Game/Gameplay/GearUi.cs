using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    public enum GearTileState
    {
        /// <summary>An owned piece: rarity coloured.</summary>
        Owned,
        /// <summary>A piece not owned yet: a dark silhouette (docs/17 section E).</summary>
        Missing,
        /// <summary>An empty slot: a dark plate with the dim slot picture.</summary>
        Empty
    }

    /// <summary>
    /// The picture of one gear piece, drawn from rarity colour, slot glyph and pips (docs/17: icons everywhere, placeholders until the OpenArt pieces
    /// arrive). Rarity is never colour alone: the frame colour AND 1 to 5 pips say it (CUX-6). Built in code, menu screens only.
    /// </summary>
    public static class GearUi
    {
        public static readonly Color Dark = new Color(0.05f, 0.07f, 0.18f, 1f);

        public static Color Fill(GearRarity r)
        {
            switch (r)
            {
                case GearRarity.Uncommon: return new Color(0.34f, 0.80f, 0.38f, 1f);
                case GearRarity.Rare: return new Color(0.30f, 0.58f, 1f, 1f);
                case GearRarity.Epic: return new Color(0.68f, 0.42f, 0.96f, 1f);
                case GearRarity.Legendary: return new Color(1f, 0.76f, 0.18f, 1f);
                default: return new Color(0.62f, 0.66f, 0.74f, 1f);
            }
        }

        public static Color Frame(GearRarity r) { return Color.Lerp(Fill(r), Dark, 0.55f); }

        /// <summary>Spoken label: rarity, slot, name and what it boosts.</summary>
        public static string Spoken(GearDef d, int level, int count)
        {
            if (d == null) return "Empty slot";
            string s = GearCatalog.RarityNames[(int)d.Rarity] + " " + d.Name + ", boosts " + PowerUpUi.Name(d.Boost) + " by " + GearCatalog.BoostAtLevel(d, level) + " percent";
            if (level > 1) s += ", level " + level;
            if (count > 1) s += ", " + count + " owned";
            return s;
        }

        /// <summary>
        /// One gear tile. <paramref name="def"/> null with state Empty draws an empty slot of <paramref name="emptySlot"/>. Numbers (level, copies) show only on tiles
        /// large enough for 16 sp text. Returns the tile root; it is a button when <paramref name="onClick"/> is given.
        /// </summary>
        public static RectTransform Tile(Transform parent, string name, GearDef def, GearTileState state, int level, int count, Vector2 size, Action onClick = null, bool equipped = false, GearSlot emptySlot = GearSlot.Head, bool showLevel = false)
        {
            Image root = UiKit.AddImage(parent, name, Theme.RoundSprite, state == GearTileState.Owned && def != null ? Frame(def.Rarity) : new Color(0.10f, 0.13f, 0.27f, 1f));
            root.type = Image.Type.Sliced;
            root.preserveAspect = false;
            root.pixelsPerUnitMultiplier = Mathf.Max(0.2f, 44f / (Mathf.Min(size.x, size.y) * 0.18f));
            var rt = root.rectTransform;
            rt.sizeDelta = size;
            if (onClick != null)
            {
                root.raycastTarget = true;
                var b = root.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { AudioManager.Play(Sfx.Tap); onClick(); });
                root.gameObject.AddComponent<A11yLabel>().Text = state == GearTileState.Owned ? Spoken(def, level, count) : (state == GearTileState.Empty ? "Empty " + GearCatalog.SlotNames[(int)emptySlot] + " slot" : "Not found yet: " + (def != null ? def.Name : ""));
            }
            float m = Mathf.Min(size.x, size.y);
            float inset = Mathf.Max(6f, m * 0.045f);

            Image fill = UiKit.AddImage(root.transform, "Fill", Theme.RoundSprite, state == GearTileState.Owned && def != null ? Fill(def.Rarity) : new Color(0.17f, 0.21f, 0.36f, 1f));
            fill.type = Image.Type.Sliced;
            fill.preserveAspect = false;
            fill.pixelsPerUnitMultiplier = root.pixelsPerUnitMultiplier * 1.15f;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(inset, inset);
            fill.rectTransform.offsetMax = new Vector2(-inset, -inset);

            // the real gear picture (a tight crop of the worn layer); only an EMPTY slot shows the slot glyph (owner 2026-10-24)
            Sprite icon = state != GearTileState.Empty && def != null ? GearIcons.Find(def) : null;
            if (icon != null)
            {
                Color iconTint = state == GearTileState.Owned ? Color.white : new Color(0.07f, 0.09f, 0.20f, 0.95f);
                Image pic = UiKit.AddImage(root.transform, "GearPic", icon, iconTint);
                pic.preserveAspect = true;
                Corner(pic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -m * 0.02f), m * 0.74f);
            }
            else
            {
                int slot = state == GearTileState.Empty ? (int)emptySlot : (def != null ? (int)def.Slot : 0);
                Color glyphTint = state == GearTileState.Owned ? Color.white : (state == GearTileState.Empty ? new Color(0.36f, 0.42f, 0.60f, 0.9f) : new Color(0.07f, 0.09f, 0.20f, 0.95f));
                Image g = UiKit.AddImage(root.transform, "Slot", Glyphs.ForSlot(slot), glyphTint);
                Corner(g.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -m * 0.03f), m * 0.52f);
            }

            if (state != GearTileState.Owned || def == null) return rt;

            // boost: the small picture of the power-up this piece makes stronger, top right
            Sprite pw = PowerUpUi.IconSprite(def.Boost);
            if (pw != null)
            {
                Image bi = UiKit.AddImage(root.transform, "Boost", pw, Color.white);
                Corner(bi.rectTransform, new Vector2(1f, 1f), new Vector2(-inset * 0.4f, -inset * 0.4f), m * 0.34f);
                bi.rectTransform.pivot = new Vector2(1f, 1f);
            }

            // rarity pips, bottom left
            float pip = m * 0.07f, pipGap = m * 0.02f;
            for (int i = 0; i <= (int)def.Rarity; i++)
            {
                Image p = UiKit.AddImage(root.transform, "Pip" + i, Theme.RoundSprite, Color.white);
                p.type = Image.Type.Sliced;
                p.preserveAspect = false;
                p.rectTransform.anchorMin = p.rectTransform.anchorMax = new Vector2(0f, 0f);
                p.rectTransform.pivot = new Vector2(0f, 0f);
                p.rectTransform.sizeDelta = new Vector2(pip, pip);
                p.rectTransform.anchoredPosition = new Vector2(inset * 1.4f + i * (pip + pipGap), inset * 1.4f);
            }

            bool numbers = m >= 170f;
            if (numbers && (level > 1 || showLevel))
                NumberBadge(root.transform, "Level", level.ToString(), new Vector2(0f, 1f), new Vector2(inset * 0.6f, -inset * 0.6f), m);
            if (numbers && count > 1)
                NumberBadge(root.transform, "Count", "x" + count, new Vector2(1f, 0f), new Vector2(-inset * 0.6f, inset * 0.6f), m);
            if (equipped)
            {
                Image ring = UiKit.AddImage(root.transform, "Worn", Theme.RingSprite, Color.white);
                ring.type = Image.Type.Sliced;
                ring.preserveAspect = false;
                ring.rectTransform.anchorMin = Vector2.zero;
                ring.rectTransform.anchorMax = Vector2.one;
                ring.rectTransform.offsetMin = new Vector2(-3f, -3f);
                ring.rectTransform.offsetMax = new Vector2(3f, 3f);
                Image tick = UiKit.AddImage(root.transform, "WornTick", Glyphs.Check, Color.white);
                Corner(tick.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -inset * 0.3f), m * 0.26f);
                tick.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            return rt;
        }

        static void Corner(RectTransform rt, Vector2 anchor, Vector2 pos, float size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
        }

        static void NumberBadge(Transform tile, string name, string text, Vector2 anchor, Vector2 pos, float m)
        {
            Image back = UiKit.AddPanel(tile, "NameBacking", new Color(0.04f, 0.06f, 0.16f, 0.82f));
            back.name = "NameBacking";
            RectTransform brt = back.rectTransform;
            brt.anchorMin = brt.anchorMax = anchor;
            brt.pivot = anchor;
            brt.anchoredPosition = pos;
            brt.sizeDelta = new Vector2(Mathf.Max(0.30f * m, 40f + text.Length * 16f), 0.30f * m);
            Text t = UiKit.AddText(back.transform, name, text, 42, Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            UiKit.Stretch((RectTransform)t.transform);
        }

        /// <summary>A centred row of small non-interactive gear tiles (results screen). Returns the row.</summary>
        public static RectTransform TileRow(Transform panel, string name, IList<string> ids, float yFromTop, float tile, float gap)
        {
            RectTransform row = UiKit.Rect(panel, name);
            row.anchorMin = new Vector2(0.5f, 1f);
            row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -yFromTop);
            float total = ids.Count * tile + (ids.Count - 1) * gap;
            row.sizeDelta = new Vector2(total, tile);
            for (int i = 0; i < ids.Count; i++)
            {
                RectTransform t = Tile(row, "Drop" + i, GearCatalog.Get(ids[i]), GearTileState.Owned, 1, 1, new Vector2(tile, tile));
                t.anchorMin = t.anchorMax = new Vector2(0f, 0.5f);
                t.pivot = new Vector2(0f, 0.5f);
                t.anchoredPosition = new Vector2(i * (tile + gap), 0f);
            }
            return row;
        }
    }

    /// <summary>
    /// The character with the worn gear drawn over him (docs/17 section C, docs/18 section B): the body picture plus one layer per worn slot, all placed through the
    /// character's rig (identity for Momo, so every layer has exactly the body's rect), drawn in the fixed order pants, boots, body, head. It is the same
    /// composite as in the run, only larger. A piece with no layer art draws nothing (CUX-10).
    /// </summary>
    public static class MomoDoll
    {
        /// <summary>Slots in draw order after Momo's body: pants, boots, then the jacket (body slot), then head gear.</summary>
        public static readonly GearSlot[] DrawOrder = { GearSlot.Pants, GearSlot.Boots, GearSlot.Body, GearSlot.Head };

        /// <summary>Resolves the worn-layer picture of a piece, or null while it has none. The pilot layers are named slot_n (head_2, body_3 ...).</summary>
        public static Func<GearDef, Sprite> LayerFor = GearLayers.Find;

        /// <summary>Builds the doll in a box of the given height (width follows the picture). Returns the box. <paramref name="who"/> null = Momo.</summary>
        public static RectTransform Build(Transform parent, string name, Sprite body, ProgressService progress, float height, CharacterDef who = null)
        {
            RectTransform box = UiKit.Rect(parent, name);
            float aspect = body != null ? body.rect.width / body.rect.height : 0.8f;
            box.sizeDelta = new Vector2(height * aspect, height);
            Color tint = who != null ? who.BodyColor : Color.white;
            Image baseImg = UiKit.AddImage(box, "Momo", body, body != null ? tint : new Color(1f, 0.85f, 0.4f, 1f));
            baseImg.preserveAspect = true;
            UiKit.Stretch(baseImg.rectTransform);
            Refresh(box, progress, who);
            return box;
        }

        /// <summary>Rebuilds the layers above the body from what is worn now.</summary>
        public static void Refresh(RectTransform box, ProgressService progress, CharacterDef who = null)
        {
            for (int i = box.childCount - 1; i >= 0; i--)
                if (box.GetChild(i).name.StartsWith("Layer")) UnityEngine.Object.Destroy(box.GetChild(i).gameObject);
            if (progress == null) return;
            CharacterRig rig = who != null ? who.Rig : CharacterRig.Identity;
            foreach (GearSlot slot in DrawOrder)
            {
                GearDef d = progress.EquippedDef(slot);
                if (d == null) continue;
                Sprite s = LayerFor != null ? LayerFor(d) : null;
                if (s == null) continue;
                Image img = UiKit.AddImage(box, "Layer_" + slot, s, Color.white);
                img.preserveAspect = false;
                RectTransform rt = img.rectTransform;
                Vector2 nmin, nmax;
                rig.NormalizedRect(slot, out nmin, out nmax);
                rt.anchorMin = nmin;
                rt.anchorMax = nmax;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// Worn-layer art lookup. Layers are PNGs the size of the Momo picture, copied to Resources/Art/GearLayers. A piece's layer is found by its
    /// own art id first (gear_head_c01), then by the pilot naming slot_n where n is the piece number (head_1 ...). Missing art gives null.
    /// </summary>
    public static class GearLayers
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Find(GearDef d)
        {
            if (d == null) return null;
            Sprite s = Load(d.ArtId);
            if (s != null) return s;
            // the pilot sheets hold six looks per slot, Common to Legendary in order: a piece wears the look of its rarity until its own art is painted
            return Load(GearCatalog.LookKey(d));
        }

        static Sprite Load(string file)
        {
            Sprite s;
            if (cache.TryGetValue(file, out s)) return s;
            s = Resources.Load<Sprite>("Art/GearLayers/" + file);
            cache[file] = s;
            return s;
        }
    }

    /// <summary>
    /// Gear tile pictures: a tight crop of the worn layer on a transparent square (Tools/art_pipeline/export_gear_icons.py), in Resources/Art/GearIcons with the
    /// same naming as <see cref="GearLayers"/>. Null when the piece has no art (<see cref="GearCatalog.HasArt"/> is then false).
    /// </summary>
    public static class GearIcons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Find(GearDef d)
        {
            if (d == null) return null;
            Sprite s = Load(d.ArtId);
            return s != null ? s : Load(GearCatalog.LookKey(d));
        }

        static Sprite Load(string file)
        {
            Sprite s;
            if (cache.TryGetValue(file, out s)) return s;
            s = Resources.Load<Sprite>("Art/GearIcons/" + file);
            cache[file] = s;
            return s;
        }
    }
}
