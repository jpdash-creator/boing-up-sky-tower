using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// The two merged bottom-bar icons of the shop (Bug List 1, 4c to 4e): <see cref="CharacterPower"/> (Character / Power-ups) and <see cref="GemsCoins"/>
    /// (Gems / Coins). Each prefers the final art in Resources/Art/Ui (icon_character_power, icon_gems_coins); until that PNG exists a placeholder is composed
    /// once in code from existing sprites (Momo's head with the bolt glyph at the bottom right; the gem and the coin side by side). The map's bottom bar
    /// (agent D) calls <see cref="CharacterPower"/> too. Menu use only, built once and cached.
    /// </summary>
    public static class ShopIcons
    {
        public const string CharacterPowerResource = "icon_character_power";
        public const string GemsCoinsResource = "icon_gems_coins";

        static Sprite characterPower, gemsCoins;

        /// <summary>Character / Power-ups tab icon.</summary>
        public static Sprite CharacterPower
        {
            get
            {
                if (characterPower != null) return characterPower;
                Sprite real = ToyKit.Get(CharacterPowerResource);
                if (real != null) return characterPower = real;
                Sprite head = CharacterCatalog.Momo != null ? CharacterCatalog.Momo.PartSprite("head") : null;
                if (head == null) head = ToyKit.Get("icon_heroes");
                // rects are fractions of the icon, top-down: x, y, width, height
                return characterPower = Compose(new[] { head, ToyKit.Get("icon_bolt") }, new[] { new Rect(0.02f, 0.02f, 0.78f, 0.78f), new Rect(0.5f, 0.48f, 0.5f, 0.5f) }, ToyKit.Get("icon_heroes"));
            }
        }

        /// <summary>Gems / Coins tab icon.</summary>
        public static Sprite GemsCoins
        {
            get
            {
                if (gemsCoins != null) return gemsCoins;
                Sprite real = ToyKit.Get(GemsCoinsResource);
                if (real != null) return gemsCoins = real;
                return gemsCoins = Compose(new[] { ToyKit.Get("icon_gem"), ToyKit.Get("icon_coin") }, new[] { new Rect(0.0f, 0.2f, 0.56f, 0.6f), new Rect(0.44f, 0.2f, 0.56f, 0.6f) }, ToyKit.Get("icon_gem"));
            }
        }

        /// <summary>Draws the layers (aspect kept, centred in their rect) into a 256 px texture; falls back to <paramref name="fallback"/> if that fails.</summary>
        static Sprite Compose(Sprite[] layers, Rect[] dst, Sprite fallback)
        {
            const int N = 256;
            RenderTexture prev = RenderTexture.active;
            RenderTexture rt = null;
            try
            {
                rt = RenderTexture.GetTemporary(N, N, 0, RenderTextureFormat.ARGB32);
                RenderTexture.active = rt;
                GL.Clear(false, true, new Color(0f, 0f, 0f, 0f));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, N, N, 0f);
                bool any = false;
                for (int i = 0; i < layers.Length; i++)
                {
                    Sprite s = layers[i];
                    if (s == null || s.texture == null) continue;
                    Texture t = s.texture;
                    Rect r = s.textureRect;
                    Rect src = new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height);
                    float aspect = r.height > 0f ? r.width / r.height : 1f;
                    Rect box = new Rect(dst[i].x * N, dst[i].y * N, dst[i].width * N, dst[i].height * N);
                    float w = box.width, h = w / aspect;
                    if (h > box.height) { h = box.height; w = h * aspect; }
                    Rect d = new Rect(box.x + (box.width - w) * 0.5f, box.y + (box.height - h) * 0.5f, w, h);
                    Graphics.DrawTexture(d, t, src, 0, 0, 0, 0);
                    any = true;
                }
                GL.PopMatrix();
                if (!any) return fallback;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.ReadPixels(new Rect(0, 0, N, N), 0, 0);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (System.Exception)
            {
                return fallback;   // CUX-10: missing art or a failed compose never crashes the shop
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
