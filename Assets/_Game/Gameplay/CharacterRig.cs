using System;
using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Resources/Characters/&lt;id&gt;/rig.json: the canvas size and the order of the character parts, back to front.</summary>
    [Serializable]
    sealed class RigJson
    {
        public string id;
        public string[] order;
        public int[] canvas;
    }

    /// <summary>
    /// The polygon rig (docs/19 section B): every character has the same parts, each a full-canvas picture (1365 x 1649, centre pivot, the same pixels per unit as
    /// Momo's picture) in Resources/Characters/&lt;id&gt;/ (back optional, legL, legR, body, handL, handR, head) plus rig.json with the canvas and the part order.
    /// Gear is drawn from its own full-canvas part pictures in Resources/Characters/gear/&lt;pieceId&gt;/ between the character parts, see <see cref="PartsAssembler"/>.
    /// </summary>
    public sealed class CharacterRig
    {
        public static readonly string[] DefaultOrder = { "back", "legL", "legR", "body", "handL", "handR", "head" };
        /// <summary>The parts a character must have for the rig to be used; without them the character is drawn as one picture (back is optional).</summary>
        public static readonly string[] RequiredParts = { "legL", "legR", "body", "handL", "handR", "head" };

        public string[] Order = DefaultOrder;
        public float CanvasW = 1365f, CanvasH = 1649f;

        public static CharacterRig FromJson(string json)
        {
            var rig = new CharacterRig();
            if (string.IsNullOrEmpty(json)) return rig;
            RigJson f = JsonUtility.FromJson<RigJson>(json);
            if (f == null) return rig;
            if (HasAllRequired(f.order)) rig.Order = f.order;   // an old (nine part) order is ignored
            if (f.canvas != null && f.canvas.Length >= 2 && f.canvas[0] > 0 && f.canvas[1] > 0) { rig.CanvasW = f.canvas[0]; rig.CanvasH = f.canvas[1]; }
            return rig;
        }

        static bool HasAllRequired(string[] order)
        {
            if (order == null) return false;
            foreach (string need in RequiredParts) if (Array.IndexOf(order, need) < 0) return false;
            return true;
        }

        /// <summary>Index of a part in the draw order, or -1.</summary>
        public int IndexOf(string part) { return Array.IndexOf(Order, part); }
    }

    /// <summary>One picture to draw, back to front. Every picture is a full canvas, so the UI stretches it over the doll box and the run scales it to the canvas.</summary>
    public struct PartDraw
    {
        public Sprite Sprite;
        public string Name;
        public float Z;
        public bool Gear;
        public GearSlot Slot;

        /// <summary>
        /// Local scale of this part as a child of the player transform, whose unit is the character canvas: <paramref name="canvasSize"/> is the size of one
        /// full-canvas picture in world units (the first part's sprite bounds). A picture of the same size gets scale 1; a differently sized one is fitted to the canvas.
        /// </summary>
        public Vector3 LocalScale(Vector2 canvasSize)
        {
            Vector2 s = Sprite != null ? Sprite.bounds.size : canvasSize;
            return new Vector3(s.x > 0f ? canvasSize.x / s.x : 1f, s.y > 0f ? canvasSize.y / s.y : 1f, 1f);
        }
    }

    /// <summary>One part picture of a gear piece.</summary>
    public sealed class GearPartEntry
    {
        public string Part;
        public Sprite Sprite;
    }

    /// <summary>
    /// Finds the part pictures of a gear piece: Resources/Characters/gear/&lt;pieceId&gt;/{head | body, sleeveL, sleeveR | bootL, bootR}.png. A piece without its
    /// required parts has none (<see cref="GearCatalog.HasArt"/> is false). Worn pieces are cached; the cache is cleared when art changes.
    /// </summary>
    public static class GearParts
    {
        static readonly Dictionary<string, List<GearPartEntry>> cache = new Dictionary<string, List<GearPartEntry>>();

        /// <summary>Tests replace the loader (no art files needed). Null restores Resources.</summary>
        public static Func<GearDef, List<GearPartEntry>> Override;

        public static void ClearCache() { cache.Clear(); }

        public static bool IsCached(GearDef d) { return d != null && cache.ContainsKey(d.Id); }

        /// <summary>The cached parts of a piece in the order head | body, sleeveL, sleeveR | bootL, bootR; null when the required parts are missing.</summary>
        public static List<GearPartEntry> Find(GearDef d)
        {
            if (d == null) return null;
            List<GearPartEntry> set;
            if (cache.TryGetValue(d.Id, out set)) return set;
            set = Load(d);
            cache[d.Id] = set;
            return set;
        }

        /// <summary>Loads the parts without caching them (the icon builder unloads them again). Null when a required part is missing.</summary>
        public static List<GearPartEntry> Load(GearDef d)
        {
            if (d == null) return null;
            if (Override != null) return Override(d);
            string root = GearCatalog.ArtRoot + d.ArtId + "/";
            var list = new List<GearPartEntry>(3);
            foreach (string part in GearCatalog.PartNames(d.Slot))
            {
                Sprite sp = Resources.Load<Sprite>(root + part);
                if (sp != null) list.Add(new GearPartEntry { Part = part, Sprite = sp });
            }
            foreach (string need in GearCatalog.RequiredParts(d.Slot))
            {
                bool found = false;
                foreach (GearPartEntry e in list) if (e.Part == need) { found = true; break; }
                if (!found) return null;
            }
            return list;
        }
    }

    /// <summary>
    /// The one place that decides what is drawn: the character's parts back to front and the worn gear between them (docs/19 section B). Draw order: back, legs,
    /// boots, body, body gear, hands, sleeves, head, head gear. The character screen, the inventory and the run all draw this list. A character without its parts
    /// (Momo until his polygon parts arrive) is one picture, and gear then draws nothing (nothing to fit it to).
    /// </summary>
    public static class PartsAssembler
    {
        public static List<PartDraw> Build(CharacterDef who, ArtCatalog art, Func<GearSlot, GearDef> worn)
        {
            var list = new List<PartDraw>(12);
            CharacterRig rig = who.Rig;
            if (who.HasParts)
            {
                string[] order = rig.Order;
                for (int i = 0; i < order.Length; i++)
                {
                    Sprite sp = who.PartSprite(order[i]);
                    if (sp == null) continue;
                    list.Add(new PartDraw { Sprite = sp, Name = order[i], Z = i });
                }
                if (worn != null)
                {
                    for (int s = 0; s < GearCatalog.SlotCount; s++)
                    {
                        GearDef d = worn((GearSlot)s);
                        List<GearPartEntry> parts = d != null ? GearParts.Find(d) : null;
                        if (parts == null) continue;
                        foreach (GearPartEntry e in parts)
                        {
                            float z = GearZ(rig, e.Part);
                            if (z < 0f) continue;
                            list.Add(new PartDraw { Sprite = e.Sprite, Name = d.Slot + "_" + e.Part, Gear = true, Slot = d.Slot, Z = z });
                        }
                    }
                }
            }
            else if (art != null && art.Player != null)
            {
                // missing part art never crashes (CUX-10): the single picture stands in
                list.Add(new PartDraw { Sprite = art.Player, Name = "body", Z = 0 });
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

        /// <summary>Draw position of a gear part: right above the character part it covers (boots above the legs and below the body, sleeves above the hands). -1 when unknown.</summary>
        static float GearZ(CharacterRig rig, string part)
        {
            switch (part)
            {
                case "head": return rig.IndexOf("head") + 0.5f;
                case "body": return rig.IndexOf("body") + 0.5f;
                case "sleeveL": return Mathf.Max(rig.IndexOf("handL"), rig.IndexOf("handR")) + 0.5f;
                case "sleeveR": return Mathf.Max(rig.IndexOf("handL"), rig.IndexOf("handR")) + 0.51f;
                case "bootL": return Mathf.Max(rig.IndexOf("legL"), rig.IndexOf("legR")) + 0.5f;
                case "bootR": return Mathf.Max(rig.IndexOf("legL"), rig.IndexOf("legR")) + 0.51f;
                default: return -1f;
            }
        }
    }

    /// <summary>One playable character: its parts and its rig.</summary>
    public sealed class CharacterDef
    {
        public string Id;
        public string Name;
        /// <summary>False for the development test character.</summary>
        public bool Production;
        CharacterRig rig;
        readonly Dictionary<string, Sprite> parts = new Dictionary<string, Sprite>();
        int hasParts;   // 0 unknown, 1 yes, 2 no

        /// <summary>Tests supply the part pictures instead of Resources. Null loads Resources/Characters/&lt;id&gt;/&lt;part&gt;.</summary>
        public Func<string, Sprite> PartLoader;

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
            s = PartLoader != null ? PartLoader(part) : Resources.Load<Sprite>("Characters/" + Id + "/" + part);
            parts[part] = s;
            return s;
        }

        /// <summary>True when every required polygon part exists. Without them the character is a single picture and cannot wear gear.</summary>
        public bool HasParts
        {
            get
            {
                if (hasParts == 0)
                {
                    hasParts = 1;
                    foreach (string p in CharacterRig.RequiredParts) if (PartSprite(p) == null) { hasParts = 2; break; }
                }
                return hasParts == 1;
            }
        }

        /// <summary>Forgets the cached part checks (art files arrived or were removed).</summary>
        public void ClearCache()
        {
            parts.Clear();
            hasParts = 0;
            rig = null;
        }

        /// <summary>True for the development test character (a TEST tag is drawn near it).</summary>
        public bool IsTest { get { return !Production; } }

        /// <summary>Production characters can always be picked (Momo falls back to his picture); a test character only once its parts exist.</summary>
        public bool Selectable { get { return Production || HasParts; } }
    }

    /// <summary>The characters. Production builds show Momo only; development builds add the test character Sporeling (docs/19), locked until its parts exist.</summary>
    public static class CharacterCatalog
    {
        const string SelectedKey = "settings.character";

        public static readonly CharacterDef Momo = new CharacterDef { Id = "momo", Name = "Momo", Production = true };
        public static readonly CharacterDef Sporeling = new CharacterDef { Id = "sporeling", Name = "Sporeling", Production = false };

        static readonly CharacterDef[] All = { Momo, Sporeling };

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

        /// <summary>The chosen character; Momo when test characters are hidden or the chosen one has no parts.</summary>
        public static CharacterDef Selected
        {
            get
            {
                if (!ShowTest) return Momo;
                CharacterDef c = Get(PlayerPrefs.GetString(SelectedKey, Momo.Id));
                return c.Selectable ? c : Momo;
            }
            set
            {
                PlayerPrefs.SetString(SelectedKey, value != null ? value.Id : Momo.Id);
                PlayerPrefs.Save();
            }
        }
    }
}
