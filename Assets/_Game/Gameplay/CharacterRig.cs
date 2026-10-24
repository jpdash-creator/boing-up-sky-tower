using BoingUpSkyTower.Economy;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>One anchor box of a character: how the shared gear layer of that slot is scaled and moved to fit this body (docs/18 section B). Identity = 1, 1, 0, 0.</summary>
    [System.Serializable]
    public sealed class RigBox
    {
        /// <summary>Scale of the layer about the box centre (1 = same size as on the Momo canvas).</summary>
        public float sx = 1f, sy = 1f;
        /// <summary>Offset in canvas fractions (x to the right, y upward).</summary>
        public float ox, oy;
    }

    [System.Serializable]
    sealed class RigFile
    {
        public RigBox head, torso, legs, feet;
        public float[] tint;
    }

    /// <summary>
    /// The rig of one character (template T1): four anchor boxes, head, torso, legs, feet, as scale and offset relative to the Momo canvas
    /// (1365 x 1649). Gear layers are painted once on the Momo shaped mannequin; the doll and the run both place a layer through
    /// <see cref="NormalizedRect"/>, so a second body with other proportions wears the same layers. Momo is the identity.
    /// </summary>
    public sealed class CharacterRig
    {
        public RigBox Head = new RigBox(), Torso = new RigBox(), Legs = new RigBox(), Feet = new RigBox();
        /// <summary>Colour multiplied over the body picture of a recoloured stand-in (white for a character with its own painted body).</summary>
        public Color Tint = Color.white;

        public static CharacterRig Identity { get { return new CharacterRig(); } }

        public RigBox For(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Head: return Head;
                case GearSlot.Body: return Torso;
                case GearSlot.Pants: return Legs;
                default: return Feet;
            }
        }

        /// <summary>Vertical centre of each slot's mannequin box as a fraction of the canvas height from the top (head 0 to 52 percent, torso 52 to 78, legs 72 to 94, feet 90 to 100).</summary>
        public static float PivotFromTop(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Head: return 0.26f;
                case GearSlot.Body: return 0.65f;
                case GearSlot.Pants: return 0.83f;
                default: return 0.95f;
            }
        }

        /// <summary>
        /// The rectangle of a slot's layer on the character canvas, in canvas fractions with y upward (0..1 = the whole canvas). The layer is scaled about the
        /// slot's box centre and moved by the offset. Identity gives (0,0) to (1,1): the same rect as the body picture.
        /// </summary>
        public void NormalizedRect(GearSlot slot, out Vector2 min, out Vector2 max)
        {
            RigBox b = For(slot);
            Vector2 c = new Vector2(0.5f, 1f - PivotFromTop(slot));
            Vector2 s = new Vector2(b.sx, b.sy);
            min = new Vector2(c.x + b.ox - c.x * s.x, c.y + b.oy - c.y * s.y);
            max = min + s;
        }

        /// <summary>
        /// Puts a world sprite layer (a child of the body sprite's transform) where the rig says: scaled to the body sprite's own bounds, so the gear has the same
        /// proportions as on the character screens whatever pixels-per-unit each picture was imported with.
        /// </summary>
        public void PlaceSprite(Transform layer, Sprite layerSprite, Sprite body, GearSlot slot)
        {
            Vector2 nmin, nmax;
            NormalizedRect(slot, out nmin, out nmax);
            Bounds pb = body.bounds;
            Vector2 min = (Vector2)pb.min + Vector2.Scale(nmin, pb.size);
            Vector2 size = Vector2.Scale(nmax - nmin, pb.size);
            Vector2 lb = layerSprite.bounds.size;
            Vector2 sc = new Vector2(size.x / lb.x, size.y / lb.y);
            layer.localScale = new Vector3(sc.x, sc.y, 1f);
            Vector2 centre = min + size * 0.5f;
            layer.localPosition = new Vector3(centre.x - layerSprite.bounds.center.x * sc.x, centre.y - layerSprite.bounds.center.y * sc.y, 0f);
        }

        public static CharacterRig FromJson(string json)
        {
            var rig = new CharacterRig();
            if (string.IsNullOrEmpty(json)) return rig;
            RigFile f = JsonUtility.FromJson<RigFile>(json);
            if (f == null) return rig;
            rig.Head = Fix(f.head);
            rig.Torso = Fix(f.torso);
            rig.Legs = Fix(f.legs);
            rig.Feet = Fix(f.feet);
            if (f.tint != null && f.tint.Length >= 3) rig.Tint = new Color(f.tint[0], f.tint[1], f.tint[2], 1f);
            return rig;
        }

        static RigBox Fix(RigBox b)
        {
            if (b == null) return new RigBox();
            if (b.sx <= 0f) b.sx = 1f;
            if (b.sy <= 0f) b.sy = 1f;
            return b;
        }
    }

    /// <summary>One playable character: a body picture (no gear) and its rig.</summary>
    public sealed class CharacterDef
    {
        public string Id;
        public string Name;
        /// <summary>False for the development test character.</summary>
        public bool Production;
        CharacterRig rig;
        bool bodyLooked;
        Sprite ownBody;

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

        /// <summary>The character's own painted body (Resources/Characters/id/body) or null; <see cref="Body"/> then falls back to the Momo picture, recoloured by the rig tint.</summary>
        public Sprite OwnBody
        {
            get
            {
                if (!bodyLooked) { ownBody = Resources.Load<Sprite>("Characters/" + Id + "/body"); bodyLooked = true; }
                return ownBody;
            }
        }

        /// <summary>The body picture to draw: the own picture, else the Momo picture from the art catalog.</summary>
        public Sprite Body(ArtCatalog art)
        {
            return OwnBody != null ? OwnBody : (art != null ? art.Player : null);
        }

        /// <summary>Colour over the body: white for an own picture and for Momo, the rig tint for a recoloured stand-in (the clearly marked test character).</summary>
        public Color BodyColor { get { return OwnBody != null || Production ? Color.white : Rig.Tint; } }

        /// <summary>True while the body is Momo's picture recoloured (no own picture): the UI marks it TEST.</summary>
        public bool IsStandIn { get { return !Production && OwnBody == null; } }
    }

    /// <summary>The characters. Production builds show Momo only; development builds add the test character Pip (docs/18 section E.5).</summary>
    public static class CharacterCatalog
    {
        const string SelectedKey = "settings.character";

        public static readonly CharacterDef Momo = new CharacterDef { Id = "momo", Name = "Momo", Production = true };
        public static readonly CharacterDef Pip = new CharacterDef { Id = "pip", Name = "Pip", Production = false };

        /// <summary>Tests set this to audit the development build behaviour in the editor (where GameSettings.DevBuild is false).</summary>
        public static bool TestOverride;

        public static bool ShowTest { get { return GameSettings.DevBuild || TestOverride; } }

        public static CharacterDef[] Available()
        {
            return ShowTest ? new[] { Momo, Pip } : new[] { Momo };
        }

        public static CharacterDef Get(string id)
        {
            return id == Pip.Id ? Pip : Momo;
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
