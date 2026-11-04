using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>Calls back once per frame while the Rewards screen lives (the countdown and the midnight / Monday rollover). Removed with the screen.</summary>
    public sealed class RewardsTicker : MonoBehaviour
    {
        public Action OnTick;
        void Update() { if (OnTick != null) OnTick(); }
    }

    /// <summary>
    /// The pictures of the Rewards screen (Resources/Art/Rewards). Two generations live here:
    /// the older OpenArt files (icon_*.png, panel_*.png and panel_slices.json) and the NAMED SLOTS of the sleek redesign (docs/26 section 4, "RewardsStyle"):
    /// scene_daily / scene_weekly (scene_main is no longer used), task_&lt;id&gt;, chest_&lt;bronze|gold|purple&gt;_&lt;closed|open&gt;.
    /// Every lookup returns null while a file is missing and the screen then draws an existing sprite or a plain colour plate (CUX-10),
    /// so it looks right both before and after the art lands. Drop a PNG in with the exact name: it is picked up with no code change
    /// (a texture imported as "Default" is wrapped into a sprite at load time).
    /// </summary>
    public static class RewardArt
    {
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> panels = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> slots = new Dictionary<string, Sprite>();
        static readonly HashSet<string> missing = new HashSet<string>();
        static Dictionary<string, Vector4> borders;

        // ---------------- the named slots of the redesign ----------------

        /// <summary>Any picture under Resources/Art/Rewards by file name without extension (cached, a miss is remembered). Null when the file is missing.</summary>
        public static Sprite Slot(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            Sprite s;
            if (slots.TryGetValue(file, out s) && s != null) return s;
            if (missing.Contains(file)) return null;
            s = Resources.Load<Sprite>("Art/Rewards/" + file);
            if (s == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("Art/Rewards/" + file);
                if (tex != null)
                {
                    s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    s.name = file;
                }
            }
            if (s == null) { missing.Add(file); return null; }
            slots[file] = s;
            return s;
        }

        /// <summary>The header scene of a tab: "daily" or "weekly" (scene_daily.png, 1568 x 672). Null while the art is missing.</summary>
        public static Sprite Scene(string tab) { return Slot("scene_" + tab); }

        /// <summary>The big picture of a task by its id (task_d_login.png ...). Null while the art is missing: the caller falls back to an existing icon.</summary>
        public static Sprite Task(string id) { return string.IsNullOrEmpty(id) ? null : Slot("task_" + id); }

        /// <summary>A milestone chest: kind "bronze", "gold" or "purple" (chest_gold_closed.png / chest_gold_open.png). Null while the art is missing.</summary>
        public static Sprite Chest(string kind, bool open) { return Slot("chest_" + kind + (open ? "_open" : "_closed")); }

        /// <summary>The chest kind of milestone <paramref name="index"/> of <paramref name="count"/>: the last one is purple, the first half bronze, the rest gold.</summary>
        public static string ChestKind(int index, int count)
        {
            if (count > 1 && index >= count - 1) return "purple";
            return index < count / 2 ? "bronze" : "gold";
        }

        // ---------------- older OpenArt files ----------------

        public static Sprite Icon(string name)
        {
            Sprite s;
            if (icons.TryGetValue(name, out s) && s != null) return s;
            s = Resources.Load<Sprite>("Art/Rewards/icon_" + name);
            icons[name] = s;
            return s;
        }

        /// <summary>A panel by short name (card_cream, box_grey, tag_claim ...) with its 9-slice border applied; null when the file is missing.</summary>
        public static Sprite Panel(string name)
        {
            Sprite s;
            if (panels.TryGetValue(name, out s) && s != null) return s;
            s = Resources.Load<Sprite>("Art/Rewards/panel_" + name);
            if (s != null && s.border.sqrMagnitude < 0.5f)
            {
                Vector4 b;
                if (Borders().TryGetValue(name, out b) || Borders().TryGetValue("panel_" + name, out b))
                {
                    Sprite withBorder = Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect, b);
                    withBorder.name = "panel_" + name;   // the layout audit treats panel_ sprites as surfaces, not content
                    s = withBorder;
                }
            }
            panels[name] = s;
            return s;
        }

        // {"card_cream": {"w": 512, "h": 256, "border": [l, b, r, t]}, ...}: read with a small pattern, the Gameplay assembly has no JSON library
        static readonly Regex Entry = new Regex("\"([^\"]+)\"\\s*:\\s*\\{[^}]*?\"border\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]", RegexOptions.Singleline);

        static Dictionary<string, Vector4> Borders()
        {
            if (borders != null) return borders;
            borders = new Dictionary<string, Vector4>();
            TextAsset ta = Resources.Load<TextAsset>("Art/Rewards/panel_slices");
            if (ta == null) return borders;
            foreach (Match m in Entry.Matches(ta.text))
                borders[m.Groups[1].Value] = new Vector4(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value));
            return borders;
        }

        /// <summary>Gives the image the panel art (9-sliced, corners drawn about <paramref name="corner"/> units wide) or, when the file is missing, a plain rounded shape in the fallback colour.</summary>
        public static Image Skin(Image img, string panel, Color fallback, float corner)
        {
            Sprite sp = Panel(panel);
            img.preserveAspect = false;
            if (sp == null)
            {
                img.sprite = UiKit.PillSprite;
                img.color = fallback;
                img.type = Image.Type.Sliced;
                return img;
            }
            img.sprite = sp;
            img.color = Color.white;
            img.type = Image.Type.Sliced;
            float borderUnits = Mathf.Max(sp.border.x, sp.border.y, sp.border.z, sp.border.w) / (sp.pixelsPerUnit / 100f);
            img.pixelsPerUnitMultiplier = Mathf.Max(1f, borderUnits / Mathf.Max(8f, corner));
            return img;
        }
    }
}
