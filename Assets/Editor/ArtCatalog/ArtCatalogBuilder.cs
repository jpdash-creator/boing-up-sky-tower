using System;
using System.IO;
using System.Linq;
using BoingUpSkyTower.Gameplay;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BoingUpSkyTower.Editor
{
    /// <summary>
    /// Fills Assets/Resources/ArtCatalog.asset from the sliced art in Assets/Art (see Assets/Art/NOTES.md).
    /// Menu: Tools/Boing Up: Sky Tower/Build Art Catalog. Headless: -executeMethod BoingUpSkyTower.Editor.ArtCatalogBuilder.BuildFromCommandLine
    /// </summary>
    public static class ArtCatalogBuilder
    {
        const string Art = "Assets/Art";
        const string AssetPath = "Assets/Resources/ArtCatalog.asset";

        [MenuItem("Tools/Boing Up: Sky Tower/Build Art Catalog")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArtCatalog>();
                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            var regions = new RegionArt[5];
            for (int r = 1; r <= 5; r++) regions[r - 1] = BuildRegion(r);
            catalog.Regions = regions;

            catalog.Player = One(Art + "/Characters/char_momo/char_momo/char_momo.png");
            catalog.CoinFrames = Folder(Art + "/Pickups/pck_coin_sheet");
            catalog.GemSizes = Folder(Art + "/Pickups/pck_gem_sheet");
            catalog.Orbs = Folder(Art + "/Pickups/pck_orbs");
            catalog.CoinIcon = One(Art + "/Currency/cur_coin_single/cur_coin_single.png");
            catalog.GemIcon = One(Art + "/Currency/cur_gem_single/cur_gem_single.png");
            catalog.StarFilled = One(Art + "/Currency/cur_star_filled/cur_star_filled.png");
            catalog.StarEmpty = One(Art + "/Currency/cur_star_empty/cur_star_empty.png");

            catalog.Buttons = Folder(Art + "/UI/ui_buttons_primary");
            catalog.RoundButtons = Folder(Art + "/UI/ui_buttons_icon_round");
            catalog.Panels = Folder(Art + "/UI/ui_panel_glass");
            catalog.Popup = Folder(Art + "/UI/ui_panel_popup");
            catalog.HudIcons = Folder(Art + "/UI/ui_hud_icons");
            catalog.LevelNodes = Folder(Art + "/UI/ui_level_nodes");
            catalog.MapNodes = Folder(Art + "/UI/ui_map_nodes");
            catalog.Ribbons = Folder(Art + "/UI/ui_banners_ribbons");
            catalog.RarityFrames = Folder(Art + "/UI/ui_frames_rarity");
            catalog.Toggles = Folder(Art + "/UI/ui_toggles_sliders");
            catalog.ProgressBars = Folder(Art + "/UI/ui_progress_bars");
            catalog.SettingsIcons = Folder(Art + "/UI/ui_settings_icons");
            catalog.Confetti = Folder(Art + "/VFX/vfx_confetti");
            catalog.Sparkles = Folder(Art + "/VFX/vfx_sparkles");
            catalog.DustPuff = Folder(Art + "/VFX/vfx_dust_puff");
            catalog.Trail = Folder(Art + "/Items/trail/trl_rainbow_ribbon");
            catalog.MapBackground = One(Art + "/Map/map_world_v1.png");
            catalog.LoadingArt = One(Art + "/UI/ui_loading_screen_art.png");
            catalog.PopupFrame = OptionalSprite(Art + "/UI/ui_popup_frame/ui_popup_frame_01.png");
            catalog.CloseIcon = OptionalSprite(Art + "/UI/ui_close_icon/ui_close_icon_01.png");
            catalog.AppIcon = One(Art + "/Marketing/icon/app_icon_concept_v1.png");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtCatalog] Built: " + Summary(catalog));
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[ArtCatalog] Failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        static RegionArt BuildRegion(int r)
        {
            string regionDir = Directory.GetDirectories(Art + "/Regions", "region_00" + r + "_*").FirstOrDefault();
            if (regionDir == null) throw new InvalidOperationException("No art folder for region " + r);
            regionDir = regionDir.Replace('\\', '/');
            var ra = new RegionArt { Index = r, Id = Path.GetFileName(regionDir) };
            ra.BgFar = First(regionDir + "/bg_far");
            ra.BgMid = First(regionDir + "/bg_mid");
            ra.BgNear = First(regionDir + "/bg_near");

            string tiles = regionDir + "/tiles/reg00" + r + "_tileset";
            Sprite[] t = Folder(tiles);
            JObject map = JObject.Parse(File.ReadAllText(tiles + "/tileset_map.json"));
            ra.Normal = Idx(t, (int)map["normal"]);
            ra.Narrow = Idx(t, (int)map["narrow"]);
            ra.Moving = Idx(t, (int)map["moving"]);
            ra.Crumble = Idx(t, (int)map["crumble"]);
            ra.Bounce = Idx(t, (int)map["bounce"]);
            ra.WallL = Idx(t, (int)map["wallL"]);
            string setDir = regionDir + "/tiles/reg00" + r + "_platforms";
            if (Directory.Exists(setDir) && File.Exists(setDir + "/platform_set.json"))
            {
                JArray pcs = (JArray)JObject.Parse(File.ReadAllText(setDir + "/platform_set.json"))["pieces"];
                ra.Pieces = new Sprite[pcs.Count];
                ra.PieceMetrics = new PlatformMetric[pcs.Count];
                for (int k = 0; k < pcs.Count; k++)
                {
                    ra.Pieces[k] = AssetDatabase.LoadAssetAtPath<Sprite>(setDir + "/" + (string)pcs[k]["file"]);
                    ra.PieceMetrics[k] = new PlatformMetric
                    {
                        W = (float)pcs[k]["w"], H = (float)pcs[k]["h"], BodyLeft = (float)pcs[k]["bodyLeft"], BodyRight = (float)pcs[k]["bodyRight"],
                        SurfaceY = (float)pcs[k]["surfaceY"], BodyBottom = (float)pcs[k]["bodyBottom"]
                    };
                }
            }
            string widthDir = regionDir + "/tiles/reg00" + r + "_widths";
            if (Directory.Exists(widthDir) && File.Exists(widthDir + "/platform_set.json"))
            {
                JArray wp = (JArray)JObject.Parse(File.ReadAllText(widthDir + "/platform_set.json"))["pieces"];
                ra.WidthPieces = new Sprite[wp.Count];
                ra.WidthMetrics = new PlatformMetric[wp.Count];
                ra.WidthUnits = new float[wp.Count];
                for (int k = 0; k < wp.Count; k++)
                {
                    ra.WidthPieces[k] = AssetDatabase.LoadAssetAtPath<Sprite>(widthDir + "/" + (string)wp[k]["file"]);
                    ra.WidthUnits[k] = (float)wp[k]["width"];
                    ra.WidthMetrics[k] = new PlatformMetric
                    {
                        W = (float)wp[k]["w"], H = (float)wp[k]["h"], BodyLeft = (float)wp[k]["bodyLeft"], BodyRight = (float)wp[k]["bodyRight"],
                        SurfaceY = (float)wp[k]["surfaceY"], BodyBottom = (float)wp[k]["bodyBottom"]
                    };
                }
            }
            string surfacePath = tiles + "/surface.json";
            if (File.Exists(surfacePath))
            {
                // Platforms are drawn from thin cropped strips (Tools/art_pipeline/platform_metrics.py) at their real aspect ratio.
                JObject root = JObject.Parse(File.ReadAllText(surfacePath));
                JObject sj = (JObject)root["platforms"];
                JToken pal = root["palette"];
                if (pal != null)
                {
                    ra.PlatTop = new Color((int)pal["top"][0] / 255f, (int)pal["top"][1] / 255f, (int)pal["top"][2] / 255f, 1f);
                    ra.PlatSide = new Color((int)pal["side"][0] / 255f, (int)pal["side"][1] / 255f, (int)pal["side"][2] / 255f, 1f);
                }
                string[] kinds = { "normal", "narrow", "moving", "crumble", "bounce" };
                ra.Metrics = new PlatformMetric[kinds.Length];
                Sprite[] strips = new Sprite[kinds.Length];
                for (int k = 0; k < kinds.Length; k++)
                {
                    JToken m = sj[kinds[k]];
                    if (m == null) continue;
                    strips[k] = AssetDatabase.LoadAssetAtPath<Sprite>(tiles + "/" + (string)m["file"]);
                    ra.Metrics[k] = new PlatformMetric
                    {
                        W = (float)m["w"], H = (float)m["h"], BodyLeft = (float)m["bodyLeft"], BodyRight = (float)m["bodyRight"],
                        SurfaceY = (float)m["surfaceY"], BodyBottom = (float)m["bodyBottom"]
                    };
                }
                JToken walls = root["walls"];
                if (walls != null)
                {
                    if (walls["L"] != null) ra.WallTileL = AssetDatabase.LoadAssetAtPath<Sprite>(tiles + "/" + (string)walls["L"]["file"]);
                    if (walls["R"] != null) ra.WallTileR = AssetDatabase.LoadAssetAtPath<Sprite>(tiles + "/" + (string)walls["R"]["file"]);
                }
                if (strips[0] != null) ra.Normal = strips[0];
                if (strips[1] != null) ra.Narrow = strips[1];
                if (strips[2] != null) ra.Moving = strips[2];
                if (strips[3] != null) ra.Crumble = strips[3];
                if (strips[4] != null) ra.Bounce = strips[4];
            }
            ra.WallR = Idx(t, (int)map["wallR"]);
            // Curated slab colours (art direction): sampled colours were too pale or blended into the backgrounds.
            Color[][] slab =
            {
                new[] { new Color(0.40f, 0.78f, 0.22f), new Color(0.55f, 0.34f, 0.18f) },
                new[] { new Color(0.78f, 0.36f, 0.92f), new Color(0.48f, 0.18f, 0.60f) },
                new[] { new Color(1.00f, 0.84f, 0.55f), new Color(0.78f, 0.50f, 0.28f) },
                new[] { new Color(0.62f, 0.96f, 0.86f), new Color(0.95f, 0.42f, 0.66f) },
                new[] { new Color(0.28f, 0.76f, 0.50f), new Color(0.66f, 0.20f, 0.20f) },
            };
            if (r >= 1 && r <= slab.Length) { ra.PlatTop = slab[r - 1][0]; ra.PlatSide = slab[r - 1][1]; }
            ra.Props = Folder(regionDir + "/props/reg00" + r + "_props");
            ra.Signature = Folder(regionDir + "/hazards/reg00" + r + "_signature");
            return ra;
        }

        static Sprite OptionalSprite(string path)
        {
            return File.Exists(path) ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null;
        }

        static Sprite Idx(Sprite[] list, int oneBased)
        {
            if (oneBased < 1 || oneBased > list.Length) throw new InvalidOperationException("tileset map index " + oneBased + " out of range " + list.Length);
            return list[oneBased - 1];
        }

        static Sprite[] Folder(string folder)
        {
            if (!Directory.Exists(folder)) throw new InvalidOperationException("Missing art folder " + folder);
            return Directory.GetFiles(folder, "*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f)).Where(s => s != null).ToArray();
        }

        static Sprite One(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) throw new InvalidOperationException("Missing sprite " + path);
            return s;
        }

        static Sprite First(string folder)
        {
            string f = Directory.GetFiles(folder, "*.png").Select(x => x.Replace('\\', '/')).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
            if (f == null) throw new InvalidOperationException("No png in " + folder);
            return One(f);
        }

        static string Summary(ArtCatalog c)
        {
            return c.Regions.Length + " regions, " + c.CoinFrames.Length + " coin frames, " + c.Buttons.Length + " buttons, " + c.LevelNodes.Length + " level nodes, " + c.MapNodes.Length + " map nodes";
        }
    }
}
