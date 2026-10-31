using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SearchEverything
{
    /// <summary>Small builders for UI made in code, borrowing the inventory's font and sprites so it looks like the game.</summary>
    internal static class Ui
    {
        public static readonly Color TextColor = new Color(0.92f, 0.9f, 0.86f);
        public static readonly Color DimTextColor = new Color(0.68f, 0.66f, 0.62f);

        public static Image AddImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        public static TMP_Text AddText(Transform parent, string name, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            TMP_Text model = InventoryGui.instance ? InventoryGui.instance.m_containerName : null;
            if (model)
            {
                text.font = model.font;
                text.fontSharedMaterial = model.fontSharedMaterial;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        public static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>The background of an inventory slot.</summary>
        public static Sprite SlotSprite()
        {
            InventoryGui gui = InventoryGui.instance;
            if (!gui || !gui.m_playerGrid || !gui.m_playerGrid.m_elementPrefab) return null;
            var image = gui.m_playerGrid.m_elementPrefab.GetComponent<Image>();
            return image ? image.sprite : null;
        }

        /// <summary>Gives an image the look of the chest panel's background (falls back to a dark panel).</summary>
        public static void CopyPanelLook(Image target)
        {
            InventoryGui gui = InventoryGui.instance;
            Transform bkg = gui && gui.m_container ? gui.m_container.Find("Bkg") : null;
            var source = bkg ? bkg.GetComponent<Image>() : null;
            if (!source || !source.sprite)
            {
                target.color = new Color(0.1f, 0.09f, 0.08f, 0.95f);
                return;
            }
            target.sprite = source.sprite;
            target.type = source.type;
            target.color = source.color;
            target.material = source.material;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }
    }
}
