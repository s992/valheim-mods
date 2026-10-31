using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SearchEverything
{
    /// <summary>A button above the player's inventory, copied from Take All, that opens the nearby search.</summary>
    internal static class InventoryButton
    {
        private static Button s_button;

        public static void Update(InventoryGui gui)
        {
            bool show = Settings.Finder.Value && Settings.InventoryButton.Value;
            if (!show)
            {
                if (s_button && s_button.gameObject.activeSelf) s_button.gameObject.SetActive(false);
                return;
            }
            if (!s_button) s_button = Create(gui);
            if (s_button && !s_button.gameObject.activeSelf) s_button.gameObject.SetActive(true);
        }

        private static Button Create(InventoryGui gui)
        {
            if (!gui.m_takeAllButton) return null;
            GameObject go = Object.Instantiate(gui.m_takeAllButton.gameObject, gui.m_player, false);
            go.name = "SearchEverything_FinderButton";
            // Take All's gamepad hint would also press this button
            foreach (UIGamePad pad in go.GetComponentsInChildren<UIGamePad>(true)) Object.DestroyImmediate(pad);
            foreach (Transform child in go.transform)
                if (child.name.StartsWith("gamepad_hint")) Object.Destroy(child.gameObject);
            foreach (Localize localize in go.GetComponentsInChildren<Localize>(true)) Object.DestroyImmediate(localize);

            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (Finder.Instance) Finder.Instance.Open();
            });
            button.interactable = true;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text)
            {
                text.text = $"Search chests ({Settings.FinderKey.Value})";
                text.enableAutoSizing = true;
                text.fontSizeMin = 10f;
                text.fontSizeMax = text.fontSize;
            }
            // Above the inventory's top-right corner, in the margin at the top of the screen
            SearchField.Place((RectTransform)go.transform, new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 4f),
                new Vector2(190f, 32f));
            SearchField.LogLayout("Nearby search button", (RectTransform)go.transform);
            Settings.FinderKey.SettingChanged += (_, __) =>
            {
                if (text) text.text = $"Search chests ({Settings.FinderKey.Value})";
            };
            return button;
        }
    }
}
