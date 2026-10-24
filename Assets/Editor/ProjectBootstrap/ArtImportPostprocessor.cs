using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    // Applies mobile-friendly sprite import settings to everything under Assets/Art (doc 03 section 8).
    // Generated art is 2K; ASTC 6x6 on mobile keeps the install small (doc 01: base install under 150 MB).
    public class ArtImportPostprocessor : AssetPostprocessor
    {
        // Thin platform sheets (<id>_platforms): pixels per unit and the 9-slice side borders come from platform_set.json, written by
        // Tools/art_pipeline/platform_metrics.py, so every platform piece has the same world thickness and caps that never stretch.
        void ApplyPlatformSet(TextureImporter ti)
        {
            if (!assetPath.Contains("_platforms/")) return;
            string json = Path.Combine(Path.GetDirectoryName(assetPath), "platform_set.json");
            if (!File.Exists(json)) return;
            var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(json));
            string name = Path.GetFileName(assetPath);
            foreach (var item in (Newtonsoft.Json.Linq.JArray)root["pieces"])
            {
                if ((string)item["file"] != name) continue;
                ti.spritePixelsPerUnit = (float)item["ppu"];
                ti.spriteBorder = new Vector4((float)item["borderL"], 0f, (float)item["borderR"], 0f);
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
            }
        }

        // World-map chunks (1080 x up to 3840) are plain textures shown through a RawImage, never scaled down below 4096.
        void ApplyWorldMap(TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = false;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.Compressed;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 4096;
                ps.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(ps);
            }
        }

        public override uint GetVersion() { return 8; }   // bump to re-import the art when the import rules change

        /// <summary>Small square icons (power-ups): a single sprite, no mipmaps, 256 px, compressed.</summary>
        static void ApplyIcon(TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = false;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 256;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        /// <summary>Chest pictures and worn-gear layers (docs/17): transparent sprites, no mipmaps, compressed; layers keep Momo's size so they line up.</summary>
        static void ApplyPicture(TextureImporter ti, int maxSize)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = false;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        /// <summary>Toy-plate UI kit (docs/16): one sprite per part, 9-slice borders per part; only the flat middle of a slice may stretch.</summary>
        static void ApplyUiKit(TextureImporter ti, string path)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = false;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Compressed;
            string n = System.IO.Path.GetFileNameWithoutExtension(path);
            Vector4 border = Vector4.zero;
            if (n.StartsWith("btn_") && n != "btn_back") border = new Vector4(150f, 150f, 150f, 150f);
            else if (n.StartsWith("panel_")) border = new Vector4(170f, 170f, 170f, 170f);
            else if (n == "pill_dark") border = new Vector4(100f, 90f, 100f, 90f);
            else if (n == "tabbar") border = new Vector4(180f, 120f, 180f, 120f);
            else if (n == "plate_pill" || n == "plate_gold") border = new Vector4(215f, 150f, 215f, 150f);
            else if (n == "plate_cream" || n == "plate_sky") border = new Vector4(190f, 150f, 190f, 150f);
            else if (n == "plate_chip") border = new Vector4(150f, 150f, 150f, 150f);
            if (n == "icon_star") ti.isReadable = true;   // the dimmed "empty star" is made from it at runtime
            ti.spriteBorder = border;
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st);
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Resources/Art/Pins/")) { ApplyIcon((TextureImporter)assetImporter); return; }
            if (assetPath.StartsWith("Assets/Resources/Art/Ui/")) { ApplyUiKit((TextureImporter)assetImporter, assetPath); return; }
            if (assetPath.StartsWith("Assets/Resources/Map/")) { ApplyWorldMap((TextureImporter)assetImporter); return; }
            if (assetPath.StartsWith("Assets/Resources/Art/Chests/")) { ApplyPicture((TextureImporter)assetImporter, 1024); return; }
            if (assetPath.StartsWith("Assets/Resources/Characters/")) { ApplyPicture((TextureImporter)assetImporter, 2048); return; }
            if (assetPath.StartsWith("Assets/Resources/Art/PowerUps/")) { ApplyIcon((TextureImporter)assetImporter); return; }
            if (!assetPath.StartsWith("Assets/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 256f;
            ApplyPlatformSet(ti);
            if (assetPath.Contains("ui_popup_frame"))
            {
                // 9-slice popup frame: 200 px corners (rounded corner plus rim), the flat middle stretches.
                ti.spritePixelsPerUnit = 100f;
                ti.spriteBorder = new Vector4(200f, 200f, 200f, 200f);
                var fs = new TextureImporterSettings();
                ti.ReadTextureSettings(fs);
                fs.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(fs);
            }
            if (assetPath.Contains("ui_buttons_primary"))
            {
                // The four square glossy buttons are drawn as wide pills: 9-slice them (corners keep their shape, only the middle stretches).
                ti.spriteBorder = new Vector4(200f, 200f, 200f, 200f);
                var bs = new TextureImporterSettings();
                ti.ReadTextureSettings(bs);
                bs.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(bs);
            }
            ti.mipmapEnabled = false;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Compressed;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 2048;
                ps.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(ps);
            }
        }
    }
}
