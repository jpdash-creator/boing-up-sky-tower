using System.IO;
using SkyHopper.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SkyHopper.Tests
{
    // Checks the sliced Tier 1 art (roadmap T1) is present and imports as mobile-ready sprites.
    public class ArtImportTests
    {
        const string Root = "Assets/Art";

        static string[] SpriteSheetFolders()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var f in Directory.GetFiles(Root, "sprites.json", SearchOption.AllDirectories)) list.Add(Path.GetDirectoryName(f).Replace("\\", "/"));
            return list.ToArray();
        }

        [Test]
        public void EverySlicedAssetHasItsSpritesListedInSpritesJson()
        {
            var folders = SpriteSheetFolders();
            Assert.GreaterOrEqual(folders.Length, 44, "44 sheet/single assets were sliced");
            int total = 0;
            foreach (var folder in folders)
            {
                var text = File.ReadAllText(folder + "/sprites.json");
                var json = Newtonsoft.Json.Linq.JObject.Parse(text);
                var sprites = (Newtonsoft.Json.Linq.JArray)json["sprites"];
                Assert.Greater(sprites.Count, 0, folder);
                foreach (var s in sprites)
                {
                    var file = folder + "/" + (string)s["file"];
                    Assert.IsTrue(File.Exists(file), "missing " + file);
                    total++;
                }
            }
            Assert.GreaterOrEqual(total, 290);
        }

        [Test]
        public void SpritesImportAsSpritesWithMobileCompression()
        {
            var path = "Assets/Art/Pickups/pck_coin_sheet/pck_coin_sheet_01.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.IsNotNull(ti, path);
            Assert.AreEqual(TextureImporterType.Sprite, ti.textureType);
            Assert.AreEqual(256f, ti.spritePixelsPerUnit);
            Assert.IsFalse(ti.mipmapEnabled);
            var android = ti.GetPlatformTextureSettings("Android");
            Assert.IsTrue(android.overridden);
            Assert.AreEqual(TextureImporterFormat.ASTC_6x6, android.format);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }

        [Test]
        public void EveryRegionTilesetHasAValidMap()
        {
            for (int r = 1; r <= 5; r++)
            {
                var files = Directory.GetFiles(Root + "/Regions", "tileset_map.json", SearchOption.AllDirectories);
                Assert.AreEqual(5, files.Length);
            }
            foreach (var f in Directory.GetFiles(Root + "/Regions", "tileset_map.json", SearchOption.AllDirectories))
            {
                var j = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(f));
                int count = (int)j["spriteCount"];
                foreach (var key in new[] { "normal", "narrow", "moving", "crumble", "bounce", "wallL", "wallR" })
                {
                    int idx = (int)j[key];
                    Assert.That(idx, Is.InRange(1, count), f + " " + key);
                }
            }
        }

        [Test]
        public void BackgroundsMapAndAppIconAreImported()
        {
            foreach (var p in new[]
            {
                "Assets/Art/Map/map_world_v1.png",
                "Assets/Art/Marketing/icon/app_icon_concept_v1.png",
                "Assets/Art/Regions/region_001_meadow_springs/bg_far/bg_meadow_springs_far_v1.png",
                "Assets/Art/Regions/region_005_bamboo_temple/bg_near/bg_bamboo_temple_near_v1.png"
            })
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(p), p);
            }
        }
    }
}
