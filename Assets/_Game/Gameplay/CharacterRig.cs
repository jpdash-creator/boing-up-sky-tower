using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    [Serializable]
    sealed class RigJson
    {
        public string id;
        public string[] order;
        public int[] canvas;
    }

    /// <summary>
    /// The parts of a character (docs/19): every part is a full-canvas picture (1365 x 1649, centre pivot, 100 ppu) drawn back to front in <see cref="Order"/>.
    /// The order comes from the character's rig.json; Momo keeps his nine parts, the polygon characters use back, legL, legR, body, handL, handR, head.
    /// </summary>
    public sealed class CharacterRig
    {
        public static readonly string[] DefaultOrder = { "back", "legL", "legR", "footL", "footR", "torso", "armL", "armR", "head" };

        public string[] Order = DefaultOrder;
        public float CanvasW = 1365f, CanvasH = 1649f;

        public static CharacterRig FromJson(string json)
        {
            var rig = new CharacterRig();
            if (string.IsNullOrEmpty(json)) return rig;
            RigJson f = JsonUtility.FromJson<RigJson>(json);
            if (f == null) return rig;
            if (f.order != null && f.order.Length > 0) rig.Order = f.order;
            if (f.canvas != null && f.canvas.Length >= 2) { rig.CanvasW = f.canvas[0]; rig.CanvasH = f.canvas[1]; }
            return rig;
        }
    }

    /// <summary>One picture to draw, back to front, in canvas fractions (y up). Shared by the UI doll and the run.</summary>
    public struct PartDraw
    {
        public Sprite Sprite;
        public Vector2 Min, Max;
        public string Name;
        public float Z;

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

    /// <summary>The one place that decides what is drawn: the character's parts back to front. The UI doll and the run both draw this list.</summary>
    public static class PartsAssembler
    {
        public static List<PartDraw> Build(CharacterDef who, ArtCatalog art)
        {
            var list = new List<PartDraw>(10);
            string[] order = who.Rig.Order;
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
            }
            return list;
        }
    }

    /// <summary>One playable character: its parts and its rig.</summary>
    public sealed class CharacterDef
    {
        public string Id;
        public string Name;
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
    }

    /// <summary>The characters. Momo and the Sporeling for now; each region will unlock one more and the rest come from gems or chests (later).</summary>
    public static class CharacterCatalog
    {
        const string SelectedKey = "settings.character";

        public static readonly CharacterDef Momo = new CharacterDef { Id = "momo", Name = "Momo" };
        public static readonly CharacterDef Sporeling = new CharacterDef { Id = "sporeling", Name = "Sporeling" };

        static readonly CharacterDef[] All = { Momo, Sporeling };

        /// <summary>The characters the player can pick (bought or received). Everything we have is available until buying and unlocking exist.</summary>
        public static CharacterDef[] Available() { return All; }

        public static CharacterDef Get(string id)
        {
            foreach (CharacterDef c in All) if (c.Id == id) return c;
            return Momo;
        }

        /// <summary>The chosen character (Momo until another is picked).</summary>
        public static CharacterDef Selected
        {
            get { return Get(PlayerPrefs.GetString(SelectedKey, Momo.Id)); }
            set
            {
                PlayerPrefs.SetString(SelectedKey, value != null ? value.Id : Momo.Id);
                PlayerPrefs.Save();
            }
        }
    }

    /// <summary>
    /// The character as a UI doll: every part picture is a full-canvas Image drawn back to front (the same list the run draws, only larger).
    /// Missing part art draws nothing (CUX-10).
    /// </summary>
    public static class MomoDoll
    {
        /// <summary>Builds the doll in a box of the given height (width follows the canvas). Returns the box. <paramref name="who"/> null = Momo.</summary>
        public static RectTransform Build(Transform parent, string name, float height, CharacterDef who = null, ArtCatalog art = null)
        {
            who = who ?? CharacterCatalog.Momo;
            RectTransform box = UiKit.Rect(parent, name);
            CharacterRig rig = who.Rig;
            box.sizeDelta = new Vector2(height * rig.CanvasW / rig.CanvasH, height);
            foreach (PartDraw d in PartsAssembler.Build(who, art ?? GameSession.Art))
            {
                Image img = UiKit.AddImage(box, "Part_" + d.Name, d.Sprite, Color.white);
                img.preserveAspect = false;
                img.raycastTarget = false;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = d.Min;
                rt.anchorMax = d.Max;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            return box;
        }
    }
}
