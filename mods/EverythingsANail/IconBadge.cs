using System;
using UnityEngine;
using ValheimMods.Shared;

namespace EverythingsANail
{
    // Draws a tool's icon with a small hammer in the corner, so a hammer acting as a pickaxe
    // reads as "your hammer, as a pickaxe" in the inventory and hotbar
    internal static class IconBadge
    {
        private const int Size = 128;
        private const float BadgeScale = 0.5f;

        public static Sprite Compose(Sprite icon, Sprite badge)
        {
            if (!icon || !badge) return icon;
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                GL.PushMatrix();
                // Pixel coordinates with the origin at the top left, like GUI rects
                GL.LoadPixelMatrix(0, Size, Size, 0);
                Draw(icon, new Rect(0, 0, Size, Size));
                float badgeSize = Size * BadgeScale;
                Draw(badge, new Rect(Size - badgeSize, Size - badgeSize, badgeSize, badgeSize));
                GL.PopMatrix();

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = icon.name + "_nail" };
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                texture.Apply();
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = texture.name;
                return sprite;
            }
            catch (Exception e)
            {
                ModInfo.Log.LogWarning($"Couldn't draw the hammer badge on {icon.name}, using the plain icon: {e.Message}");
                return icon;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        // Icons usually live in a sprite atlas, so draw only the sprite's part of the texture
        private static void Draw(Sprite sprite, Rect dest)
        {
            Texture2D texture = sprite.texture;
            if (!sprite.packed || sprite.packingMode == SpritePackingMode.Rectangle)
            {
                Rect rect = sprite.textureRect;
                Graphics.DrawTexture(dest, texture,
                    new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height),
                    0, 0, 0, 0);
                return;
            }
            // Tight packing has no texture rect; the mesh's UV bounds are the closest thing
            Vector2[] uv = sprite.uv;
            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
            foreach (Vector2 point in uv)
            {
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
            Graphics.DrawTexture(dest, texture, new Rect(minX, minY, maxX - minX, maxY - minY), 0, 0, 0, 0);
        }
    }
}
