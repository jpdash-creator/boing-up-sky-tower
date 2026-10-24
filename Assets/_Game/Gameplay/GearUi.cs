using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The picture of one gear piece: ONLY the gear's own picture on a rarity coloured tile (docs/19 section D). Rarity is never colour alone: the tile colour AND 1 to 5
    /// small pips say it (CUX-6). There are no words and no stand-in glyphs; a piece without art has no tile at all. Built in code, menu screens only.
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

        /// <summary>Spoken label: rarity, name and what it boosts.</summary>
        public static string Spoken(GearDef d)
        {
            if (d == null) return "Empty slot";
            return GearCatalog.RarityNames[(int)d.Rarity] + " " + d.Name + ", boosts " + PowerUpUi.Name(d.Boost) + " by " + d.BoostPercent + " percent";
        }

        /// <summary>One gear tile. It is a button when <paramref name="onClick"/> is given; <paramref name="equipped"/> adds the worn ring and tick.</summary>
        public static RectTransform Tile(Transform parent, string name, GearDef def, Vector2 size, Action onClick = null, bool equipped = false)
        {
            Image root = UiKit.AddImage(parent, name, Theme.RoundSprite, def != null ? Frame(def.Rarity) : new Color(0.10f, 0.13f, 0.27f, 1f));
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
                root.gameObject.AddComponent<A11yLabel>().Text = Spoken(def) + (equipped ? ", worn" : "");
            }
            float m = Mathf.Min(size.x, size.y);
            float inset = Mathf.Max(6f, m * 0.045f);

            Image fill = UiKit.AddImage(root.transform, "Fill", Theme.RoundSprite, def != null ? Fill(def.Rarity) : new Color(0.17f, 0.21f, 0.36f, 1f));
            fill.type = Image.Type.Sliced;
            fill.preserveAspect = false;
            fill.pixelsPerUnitMultiplier = root.pixelsPerUnitMultiplier * 1.15f;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(inset, inset);
            fill.rectTransform.offsetMax = new Vector2(-inset, -inset);

            // the gear's own picture (a tight crop of its parts composite); nothing is drawn while it has none
            Sprite icon = def != null ? GearIcons.Find(def) : null;
            if (icon != null)
            {
                Image pic = UiKit.AddImage(root.transform, "GearPic", icon, Color.white);
                pic.preserveAspect = true;
                pic.raycastTarget = false;
                Corner(pic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, m * 0.76f);
            }
            if (def == null) return rt;

            // rarity pips, bottom left (shape count, not colour alone)
            float pip = m * 0.07f, pipGap = m * 0.02f;
            for (int i = 0; i <= (int)def.Rarity; i++)
            {
                Image p = UiKit.AddImage(root.transform, "Pip" + i, Theme.RoundSprite, Color.white);
                p.type = Image.Type.Sliced;
                p.preserveAspect = false;
                p.raycastTarget = false;
                p.rectTransform.anchorMin = p.rectTransform.anchorMax = new Vector2(0f, 0f);
                p.rectTransform.pivot = new Vector2(0f, 0f);
                p.rectTransform.sizeDelta = new Vector2(pip, pip);
                p.rectTransform.anchoredPosition = new Vector2(inset * 1.4f + i * (pip + pipGap), inset * 1.4f);
            }

            if (equipped)
            {
                Image ring = UiKit.AddImage(root.transform, "Worn", Theme.RingSprite, Color.white);
                ring.type = Image.Type.Sliced;
                ring.preserveAspect = false;
                ring.raycastTarget = false;
                ring.rectTransform.anchorMin = Vector2.zero;
                ring.rectTransform.anchorMax = Vector2.one;
                ring.rectTransform.offsetMin = new Vector2(-3f, -3f);
                ring.rectTransform.offsetMax = new Vector2(3f, 3f);
                Image tick = UiKit.AddImage(root.transform, "WornTick", Glyphs.Check, Color.white);
                tick.raycastTarget = false;
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

        /// <summary>A centred row of small non-interactive gear tiles (results screen); pieces without art are left out. Returns the row.</summary>
        public static RectTransform TileRow(Transform panel, string name, IList<string> ids, float yFromTop, float tile, float gap)
        {
            var shown = new List<GearDef>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                GearDef d = GearCatalog.Get(ids[i]);
                if (d != null && GearCatalog.HasArt(d)) shown.Add(d);
            }
            RectTransform row = UiKit.Rect(panel, name);
            row.anchorMin = new Vector2(0.5f, 1f);
            row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -yFromTop);
            float total = Mathf.Max(0, shown.Count) * tile + Mathf.Max(0, shown.Count - 1) * gap;
            row.sizeDelta = new Vector2(total, tile);
            for (int i = 0; i < shown.Count; i++)
            {
                RectTransform t = Tile(row, "Drop" + i, shown[i], new Vector2(tile, tile));
                t.anchorMin = t.anchorMax = new Vector2(0f, 0.5f);
                t.pivot = new Vector2(0f, 0.5f);
                t.anchoredPosition = new Vector2(i * (tile + gap), 0f);
            }
            return row;
        }
    }

    /// <summary>
    /// The character with the worn gear: every part picture of the character and of the worn gear is a full-canvas Image stretched over one box, drawn back to front
    /// in the order <see cref="PartsAssembler"/> gives. It is the same list the run draws, only larger. Missing part art draws nothing (CUX-10).
    /// </summary>
    public static class CharacterDoll
    {
        /// <summary>Builds the doll in a box of the given height (width follows the canvas). Returns the box. <paramref name="who"/> null = Momo.</summary>
        public static RectTransform Build(Transform parent, string name, ProgressService progress, float height, CharacterDef who = null, ArtCatalog art = null)
        {
            who = who ?? CharacterCatalog.Momo;
            RectTransform box = UiKit.Rect(parent, name);
            CharacterRig rig = who.Rig;
            box.sizeDelta = new Vector2(height * rig.CanvasW / rig.CanvasH, height);
            Refresh(box, progress, who, art);
            return box;
        }

        /// <summary>Rebuilds every part image from the character and what is worn now.</summary>
        public static void Refresh(RectTransform box, ProgressService progress, CharacterDef who = null, ArtCatalog art = null)
        {
            who = who ?? CharacterCatalog.Momo;
            for (int i = box.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(box.GetChild(i).gameObject);
            Func<GearSlot, GearDef> worn = progress != null ? (Func<GearSlot, GearDef>)(slot => progress.EquippedDef(slot)) : null;
            List<PartDraw> draws = PartsAssembler.Build(who, art ?? GameSession.Art, worn);
            foreach (PartDraw d in draws)
            {
                Image img = UiKit.AddImage(box, (d.Gear ? "Layer_" : "Part_") + d.Name, d.Sprite, Color.white);
                img.preserveAspect = false;
                img.raycastTarget = false;
                UiKit.Stretch(img.rectTransform);
            }
        }
    }

    /// <summary>
    /// Gear tile pictures: a tight crop of the piece's own parts composite on a transparent background, built once per piece at menu time (never during a run) and cached.
    /// The parts are read back through a render texture at half size, laid over each other in draw order, cropped to what is painted and scaled to at most 256 px.
    /// A piece that is not worn is unloaded again right after, so browsing 60 pieces does not keep 60 full-canvas pictures in memory. Null when the piece has no art.
    /// </summary>
    public static class GearIcons
    {
        const int MaxEdge = 256;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Tests replace the picture of a piece (no art files needed). Null restores the real composite.</summary>
        public static Func<GearDef, Sprite> Override;

        public static void ClearCache() { cache.Clear(); }

        public static Sprite Find(GearDef d)
        {
            if (d == null) return null;
            if (Override != null) return Override(d);
            Sprite s;
            if (cache.TryGetValue(d.Id, out s)) return s;
            s = Build(d);
            cache[d.Id] = s;
            return s;
        }

        static Sprite Build(GearDef d)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;   // no GPU (batch mode without graphics): no picture
            bool wasCached = GearParts.IsCached(d);
            List<GearPartEntry> parts = wasCached ? GearParts.Find(d) : GearParts.Load(d);
            if (parts == null || parts.Count == 0) return null;
            Color32[] acc = null;
            int w = 0, h = 0;
            Texture2D read = null;
            foreach (GearPartEntry e in parts)
            {
                Texture tex = e.Sprite.texture;
                if (acc == null)
                {
                    w = Mathf.Max(1, tex.width / 2);
                    h = Mathf.Max(1, tex.height / 2);
                    acc = new Color32[w * h];
                    read = new Texture2D(w, h, TextureFormat.RGBA32, false);
                }
                else if (tex.width / 2 != w || tex.height / 2 != h) continue;   // a part on another canvas size cannot be laid over
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Color32[] src = read.GetPixels32();
                for (int i = 0; i < acc.Length; i++) acc[i] = Over(src[i], acc[i]);
            }
            if (read != null) UnityEngine.Object.Destroy(read);
            if (!wasCached)
                foreach (GearPartEntry e in parts) Resources.UnloadAsset(e.Sprite.texture);
            if (acc == null) return null;

            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (acc[y * w + x].a <= 8) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
            if (x1 < x0 || y1 < y0) return null;
            int pad = Mathf.Max(2, (x1 - x0 + y1 - y0) / 60);
            x0 = Mathf.Max(0, x0 - pad); y0 = Mathf.Max(0, y0 - pad);
            x1 = Mathf.Min(w - 1, x1 + pad); y1 = Mathf.Min(h - 1, y1 + pad);
            int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
            float k = Mathf.Min(1f, MaxEdge / (float)Mathf.Max(cw, ch));
            int ow = Mathf.Max(1, Mathf.RoundToInt(cw * k)), oh = Mathf.Max(1, Mathf.RoundToInt(ch * k));
            var outPx = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
            {
                for (int x = 0; x < ow; x++)
                {
                    float sx = x0 + (x + 0.5f) / ow * cw - 0.5f, sy = y0 + (y + 0.5f) / oh * ch - 0.5f;
                    outPx[y * ow + x] = Bilinear(acc, w, h, sx, sy);
                }
            }
            var tex2 = new Texture2D(ow, oh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = d.Id + "_icon" };
            tex2.SetPixels32(outPx);
            tex2.Apply();
            Sprite sp = Sprite.Create(tex2, new Rect(0, 0, ow, oh), new Vector2(0.5f, 0.5f), 100f);
            sp.name = d.Id + "_icon";
            return sp;
        }

        /// <summary>Straight-alpha "over": the source laid on the destination.</summary>
        static Color32 Over(Color32 s, Color32 d)
        {
            if (s.a == 0) return d;
            if (s.a == 255 || d.a == 0) return s;
            float sa = s.a / 255f, da = d.a / 255f, oa = sa + da * (1f - sa);
            float ds = da * (1f - sa) / oa, ss = sa / oa;
            return new Color32((byte)(s.r * ss + d.r * ds), (byte)(s.g * ss + d.g * ds), (byte)(s.b * ss + d.b * ds), (byte)(oa * 255f));
        }

        static Color32 Bilinear(Color32[] px, int w, int h, float x, float y)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, w - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = Mathf.Clamp01(x - x0), fy = Mathf.Clamp01(y - y0);
            Color32 a = px[y0 * w + x0], b = px[y0 * w + x1], c = px[y1 * w + x0], e = px[y1 * w + x1];
            // premultiplied blend so transparent pixels do not darken the edge
            float wa = (1 - fx) * (1 - fy), wb = fx * (1 - fy), wc = (1 - fx) * fy, we = fx * fy;
            float al = a.a * wa + b.a * wb + c.a * wc + e.a * we;
            if (al <= 0.001f) return new Color32(0, 0, 0, 0);
            float r = (a.r * a.a * wa + b.r * b.a * wb + c.r * c.a * wc + e.r * e.a * we) / al;
            float g = (a.g * a.a * wa + b.g * b.a * wb + c.g * c.a * wc + e.g * e.a * we) / al;
            float bl = (a.b * a.a * wa + b.b * b.a * wb + c.b * c.a * wc + e.b * e.a * we) / al;
            return new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(bl, 0, 255), (byte)Mathf.Clamp(al, 0, 255));
        }
    }
}
