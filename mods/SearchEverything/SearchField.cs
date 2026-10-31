using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SearchEverything
{
    /// <summary>Copies of the build menu's search box, so ours look and type like the game's own.</summary>
    internal static class SearchField
    {
        public static GuiInputField Create(Transform parent, string name, string placeholder)
        {
            GuiInputField template = Hud.instance ? Hud.instance.m_buildUi.m_searchField : null;
            if (!template)
            {
                Plugin.Log.LogWarning("The build menu's search box wasn't found; search boxes are disabled");
                return null;
            }

            GameObject go = Object.Instantiate(template.gameObject, parent, false);
            go.name = name;
            go.SetActive(true);
            // Its key hint shows the build menu's focus key, which does nothing here
            Transform hint = go.transform.Find("SearchBarHint");
            if (hint) hint.gameObject.SetActive(false);
            foreach (Localize localize in go.GetComponentsInChildren<Localize>(true)) Object.DestroyImmediate(localize);

            var field = go.GetComponent<GuiInputField>();
            // Fresh events: drop anything wired to the build menu in its prefab
            field.onValueChanged = new TMP_InputField.OnChangeEvent();
            field.onSelect = new TMP_InputField.SelectionEvent();
            field.onDeselect = new TMP_InputField.SelectionEvent();
            field.onSubmit = new TMP_InputField.SubmitEvent();
            field.onEndEdit = new TMP_InputField.SubmitEvent();
            field.OnInputSubmit = new OnInputSubmitEvent();
            field.text = "";
            field.restoreOriginalTextOnEscape = false;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            SetPlaceholder(field, placeholder);
            InputGuard.Register(field);
            return field;
        }

        public static void SetPlaceholder(GuiInputField field, string text)
        {
            if (field && field.placeholder is TMP_Text placeholder) placeholder.text = text;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>The panel a box was put on and the box itself, in the log (Diagnostics setting).</summary>
        public static void LogLayout(string what, RectTransform box)
        {
            if (!Settings.LogLayout.Value || !box) return;
            Plugin.Log.LogInfo($"{what}: {Describe(box)}");
            if (box.parent is RectTransform parent)
            {
                Plugin.Log.LogInfo($"  in {parent.name}: {Describe(parent)}");
                foreach (RectTransform child in parent)
                    if (child != box && child.gameObject.activeSelf) Plugin.Log.LogInfo($"    {child.name}: {Describe(child)}");
            }
        }

        private static string Describe(RectTransform rect) =>
            $"rect {rect.rect.xMin:0},{rect.rect.yMin:0} {rect.rect.width:0}x{rect.rect.height:0} anchored {rect.anchoredPosition} " +
            $"anchors {rect.anchorMin}-{rect.anchorMax} pivot {rect.pivot}";
    }
}
