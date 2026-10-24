using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    // ---- JSON shapes (Resources/Characters/<id>/rig.json and Resources/Characters/gear_<set>/gear.json, written by Tools/art_pipeline/cut_character_parts.py) ----

    /// <summary>A fit: move by (dx, dy) canvas px and scale about a point. Scale 0 means "absent" (identity).</summary>
    [Serializable]
    public sealed class FitOffset
    {
        public float dx, dy, scale;
    }

    [Serializable]
    public sealed class PartFits
    {
        public FitOffset back, legL, legR, footL, footR, torso, armL, armR, head;

        public FitOffset For(string part)
        {
            switch (part)
            {
                case "back": return back;
                case "legL": return legL;
                case "legR": return legR;
                case "footL": return footL;
                case "footR": return footR;
                case "torso": return torso;
                case "armL": return armL;
                case "armR": return armR;
                case "head": return head;
                default: return null;
            }
        }
    }

    [Serializable]
    public sealed class SlotFit
    {
        public float dx, dy, scale;
        public PartFits parts;
    }

    [Serializable]
    public sealed class GearFits
    {
        public SlotFit head, body, pants, boots;

        public SlotFit For(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Head: return head;
                case GearSlot.Body: return body;
                case GearSlot.Pants: return pants;
                default: return boots;
            }
        }
    }

    [Serializable]
    sealed class RigJson
    {
        public string id;
        public string[] order;
        public int[] canvas;
        public GearFits gear_offsets;
    }

    [Serializable]
    sealed class GearPartJson
    {
        public string part, file, above;
        public int[] bbox;
    }

    [Serializable]
    sealed class GearSlotJson
    {
        public GearPartJson[] parts;
    }

    [Serializable]
    sealed class GearSlotsJson
    {
        public GearSlotJson head, body, pants, boots;
    }

    [Serializable]
    sealed class GearAnchorsJson
    {
        public int[] head, body, pants, boots;
    }

    [Serializable]
    sealed class GearJson
    {
        public string id;
        public GearSlotsJson slots;
        public GearAnchorsJson anchors;
    }

    /// <summary>
    /// The parts of a character and how gear is fitted to it (docs/18, part based). A character is nine full-canvas pictures (1365 x 1649, centre pivot, 100 ppu)
    /// drawn back to front in <see cref="Order"/>; every gear part sits right above the character part it covers. <see cref="GearFits"/> holds, per slot (and per
    /// sleeve), the scale and offset that fit the shared gear parts to THIS body: p' = anchor + (dx, dy) + scale * (p - anchor), then the optional per-part fit
    /// about the part's own moved centre (the same maths as Tools/art_pipeline/render_character_composites.py, which is the reference).
    /// </summary>
    public sealed class CharacterRig
    {
        public static readonly string[] DefaultOrder = { "back", "legL", "legR", "footL", "footR", "torso", "armL", "armR", "head" };

        public string[] Order = DefaultOrder;
        public float CanvasW = 1365f, CanvasH = 1649f;
        public GearFits Fits;

        public static CharacterRig Identity { get { return new CharacterRig(); } }

        public static CharacterRig FromJson(string json)
        {
            var rig = new CharacterRig();
            if (string.IsNullOrEmpty(json)) return rig;
            RigJson f = JsonUtility.FromJson<RigJson>(json);
            if (f == null) return rig;
            if (f.order != null && f.order.Length > 0) rig.Order = f.order;
            if (f.canvas != null && f.canvas.Length >= 2) { rig.CanvasW = f.canvas[0]; rig.CanvasH = f.canvas[1]; }
            rig.Fits = f.gear_offsets;
            return rig;
        }

        /// <summary>
        /// The rectangle a gear part lands on, in canvas fractions with y upward (0,0 to 1,1 is the whole canvas). <paramref name="anchor"/> is the gear slot anchor in
        /// canvas px (y down), <paramref name="partCentre"/> the centre of the part's picture in canvas px. No fit gives (0,0) to (1,1).
        /// </summary>
        public void GearRect(GearSlot slot, Vector2 anchor, string part, Vector2 partCentre, out Vector2 min, out Vector2 max)
        {
            SlotFit off = Fits != null ? Fits.For(slot) : null;
            float s = 1f;
            Vector2 t = Vector2.zero;
            if (off != null && off.scale > 0f)
            {
                s = off.scale;
                t = new Vector2(anchor.x + off.dx - s * anchor.x, anchor.y + off.dy - s * anchor.y);
                FitOffset po = off.parts != null ? off.parts.For(part) : null;
                if (po != null && po.scale > 0f)
                {
                    Vector2 c = s * partCentre + t;
                    Vector2 t2 = c * (1f - po.scale) + new Vector2(po.dx, po.dy);
                    t = po.scale * t + t2;
                    s *= po.scale;
                }
            }
            min = new Vector2(t.x / CanvasW, 1f - (t.y + s * CanvasH) / CanvasH);
            max = new Vector2((t.x + s * CanvasW) / CanvasW, 1f - t.y / CanvasH);
        }
    }

    /// <summary>One picture to draw, back to front, in canvas fractions (y up). Shared by the UI doll and the run.</summary>
    public struct PartDraw
    {
        public Sprite Sprite;
        public Vector2 Min, Max;
        public string Name;
        public float Z;
        public bool Gear;
        public GearSlot Slot;

        /// <summary>
        /// Local scale and position of this part as a child of the player transform, whose unit is the character canvas: <paramref name="canvasSize"/> is the size of
        /// one full-canvas part picture in world units (sprite bounds). The run uses exactly this.
        /// </summary>
        public void LocalTransform(Vector2 canvasSize, out Vector3 position, out Vector3 scale)
        {
            Vector2 c = (Min + Max) * 0.5f - new Vector2(0.5f, 0.5f);
            scale = new Vector3(Max.x - Min.x, Max.y - Min.y, 1f);
            position = new Vector3(c.x * canvasSize.x, c.y * canvasSize.y, 0f);
        }
    }

    /// <summary>One part picture of a gear piece with the character part it is drawn above.</summary>
    public sealed class GearPartEntry
    {
        public string Part, Above;
        public Sprite Sprite;
        public Vector2 Centre;
    }

    /// <summary>The parts of one worn piece: its pictures and its anchor on the Momo canvas.</summary>
    public sealed class GearPartSet
    {
        public readonly List<GearPartEntry> Entries = new List<GearPartEntry>(3);
        public Vector2 Anchor;
    }

    /// <summary>
    /// Finds the parts of a gear piece. A piece with a parts folder (Resources/Characters/gear_&lt;set&gt;/gear.json, now the dev test pieces) is cut into parts;
    /// a piece that only has a single worn layer (the pilot pieces) is one part drawn above the character part its slot covers, fitted by the same slot fit.
    /// </summary>
    public static class GearParts
    {
        static readonly Dictionary<string, GearPartSet> cache = new Dictionary<string, GearPartSet>();

        static readonly Dictionary<GearSlot, Vector2> DefaultAnchors = new Dictionary<GearSlot, Vector2>
        {
            { GearSlot.Head, new Vector2(520f, 935f) }, { GearSlot.Body, new Vector2(520f, 1000f) },
            { GearSlot.Pants, new Vector2(520f, 1390f) }, { GearSlot.Boots, new Vector2(520f, 1600f) },
        };

        /// <summary>The gear set a piece's parts live in ("test" for the dev test pieces), or null for a single layer piece.</summary>
        public static string SetOf(GearDef d) { return GearCatalog.IsTestPiece(d) ? "test" : null; }

        public static GearPartSet Find(GearDef d)
        {
            if (d == null) return null;
            string key = d.Id;
            GearPartSet set;
            if (cache.TryGetValue(key, out set)) return set;
            set = Load(d);
            cache[key] = set;
            return set;
        }

        static GearPartSet Load(GearDef d)
        {
            string gset = SetOf(d);
            if (gset != null)
            {
                string root = "Characters/gear_" + gset + "/";
                TextAsset ta = Resources.Load<TextAsset>(root + "gear");
                if (ta == null) return null;
                GearJson g = JsonUtility.FromJson<GearJson>(ta.text);
                if (g == null || g.slots == null) return null;
                GearSlotJson sj = Slot(g.slots, d.Slot);
                if (sj == null || sj.parts == null) return null;
                var set = new GearPartSet { Anchor = Anchor(g.anchors, d.Slot) };
                foreach (GearPartJson p in sj.parts)
                {
                    Sprite sp = Resources.Load<Sprite>(root + p.file.Replace(".png", ""));
                    if (sp == null) continue;
                    Vector2 c = p.bbox != null && p.bbox.Length >= 4 ? new Vector2((p.bbox[0] + p.bbox[2]) * 0.5f, (p.bbox[1] + p.bbox[3]) * 0.5f) : set.Anchor;
                    set.Entries.Add(new GearPartEntry { Part = p.part, Above = p.above, Sprite = sp, Centre = c });
                }
                return set.Entries.Count > 0 ? set : null;
            }
            Sprite layer = GearLayers.Find(d);
            if (layer == null) return null;
            var one = new GearPartSet { Anchor = DefaultAnchors[d.Slot] };
            string above = d.Slot == GearSlot.Head ? "head" : (d.Slot == GearSlot.Boots ? "footR" : "torso");
            one.Entries.Add(new GearPartEntry { Part = "layer", Above = above, Sprite = layer, Centre = one.Anchor });
            return one;
        }

        static GearSlotJson Slot(GearSlotsJson s, GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Head: return s.head;
                case GearSlot.Body: return s.body;
                case GearSlot.Pants: return s.pants;
                default: return s.boots;
            }
        }

        static Vector2 Anchor(GearAnchorsJson a, GearSlot slot)
        {
            int[] v = null;
            if (a != null)
            {
                switch (slot)
                {
                    case GearSlot.Head: v = a.head; break;
                    case GearSlot.Body: v = a.body; break;
                    case GearSlot.Pants: v = a.pants; break;
                    default: v = a.boots; break;
                }
            }
            return v != null && v.Length >= 2 ? new Vector2(v[0], v[1]) : DefaultAnchors[slot];
        }
    }

    /// <summary>
    /// The one place that decides what is drawn and where: the character's parts back to front, each worn gear part right above the character part it covers
    /// (pants and boots are cut so they never cover the same pixels). The UI doll (character screen, inventory, info screen) and the run both draw this list.
    /// </summary>
    public static class PartsAssembler
    {
        /// <summary>Gear draw order inside one anchor part: pants, body, boots, head (render_character_composites.py).</summary>
        static readonly GearSlot[] SlotOrder = { GearSlot.Pants, GearSlot.Body, GearSlot.Boots, GearSlot.Head };

        public static List<PartDraw> Build(CharacterDef who, ArtCatalog art, Func<GearSlot, GearDef> worn)
        {
            var list = new List<PartDraw>(20);
            CharacterRig rig = who.Rig;
            string[] order = rig.Order;
            for (int i = 0; i < order.Length; i++)
            {
                Sprite sp = who.PartSprite(order[i]);
                if (sp == null) continue;
                list.Add(new PartDraw { Sprite = sp, Min = Vector2.zero, Max = Vector2.one, Name = order[i], Z = i });
            }
            if (list.Count == 0 && art != null && art.Player != null)
            {
                // missing part art never crashes (CUX-10): the single Momo picture stands in
                list.Add(new PartDraw { Sprite = art.Player, Min = Vector2.zero, Max = Vector2.one, Name = "body", Z = 0 });
                order = new[] { "body" };
            }
            if (worn != null)
            {
                for (int k = 0; k < SlotOrder.Length; k++)
                {
                    GearSlot slot = SlotOrder[k];
                    GearDef d = worn(slot);
                    GearPartSet set = d != null ? GearParts.Find(d) : null;
                    if (set == null) continue;
                    foreach (GearPartEntry e in set.Entries)
                    {
                        int at = Array.IndexOf(order, e.Above);
                        Vector2 min, max;
                        rig.GearRect(slot, set.Anchor, e.Part, e.Centre, out min, out max);
                        list.Add(new PartDraw
                        {
                            Sprite = e.Sprite, Min = min, Max = max, Name = slot + "_" + e.Part, Gear = true, Slot = slot,
                            Z = (at < 0 ? order.Length : at) + 0.5f + k * 0.01f
                        });
                    }
                }
            }
            // insertion sort by Z (stable, the list is short)
            for (int i = 1; i < list.Count; i++)
            {
                PartDraw v = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].Z > v.Z) { list[j + 1] = list[j]; j--; }
                list[j + 1] = v;
            }
            return list;
        }
    }

    /// <summary>One playable character: its parts and its rig.</summary>
    public sealed class CharacterDef
    {
        public string Id;
        public string Name;
        /// <summary>False for the development test characters.</summary>
        public bool Production;
        CharacterRig rig;
        readonly Dictionary<string, Sprite> parts = new Dictionary<string, Sprite>();

        public CharacterRig Rig
        {
            get
            {
                if (rig != null) return rig;
                TextAsset ta = Resources.Load<TextAsset>("Characters/" + Id + "/rig");
                rig = CharacterRig.FromJson(ta != null ? ta.text : null);
                return rig;
            }
        }

        /// <summary>The full-canvas picture of one part (back, legL ... head), or null when it is missing.</summary>
        public Sprite PartSprite(string part)
        {
            Sprite s;
            if (parts.TryGetValue(part, out s)) return s;
            s = Resources.Load<Sprite>("Characters/" + Id + "/" + part);
            parts[part] = s;
            return s;
        }

        /// <summary>True for the development test characters (a TEST tag is drawn near them).</summary>
        public bool IsTest { get { return !Production; } }
    }

    /// <summary>The characters. Production builds show Momo only; development builds add the three test characters (docs/18 section E.5).</summary>
    public static class CharacterCatalog
    {
        const string SelectedKey = "settings.character";

        public static readonly CharacterDef Momo = new CharacterDef { Id = "momo", Name = "Momo", Production = true };
        public static readonly CharacterDef Sporeling = new CharacterDef { Id = "sporeling", Name = "Sporeling", Production = false };
        public static readonly CharacterDef Moth = new CharacterDef { Id = "moth", Name = "Moth", Production = false };
        public static readonly CharacterDef Rootling = new CharacterDef { Id = "rootling", Name = "Rootling", Production = false };

        static readonly CharacterDef[] All = { Momo, Sporeling, Moth, Rootling };

        /// <summary>Tests set this to audit the development build behaviour in the editor (where GameSettings.DevBuild is false).</summary>
        public static bool TestOverride;

        public static bool ShowTest { get { return GameSettings.DevBuild || TestOverride; } }

        public static CharacterDef[] Available()
        {
            if (ShowTest) return All;
            return new[] { Momo };
        }

        public static CharacterDef Get(string id)
        {
            foreach (CharacterDef c in All) if (c.Id == id) return c;
            return Momo;
        }

        /// <summary>The chosen character; always Momo when test characters are hidden.</summary>
        public static CharacterDef Selected
        {
            get { return ShowTest ? Get(PlayerPrefs.GetString(SelectedKey, Momo.Id)) : Momo; }
            set
            {
                PlayerPrefs.SetString(SelectedKey, value != null ? value.Id : Momo.Id);
                PlayerPrefs.Save();
            }
        }
    }
}
