using System;
using System.Collections.Generic;
using System.Linq;
using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SearchEverything
{
    /// <summary>
    /// The nearby search window: one row per item in nearby chests (total and how many chests), expandable to the
    /// chests themselves, nearest first. Picking a row marks the chests (Markers) and closes the window.
    /// </summary>
    internal class Finder : MonoBehaviour
    {
        private const int VisibleRows = 10;
        private const float RowHeight = 36f;
        private const float PanelWidth = 640f;
        private const float RowsTop = 132f;
        private const float RescanSeconds = 1f;

        private sealed class Group
        {
            public string Key;
            public string Name;
            public Sprite Icon;
            public int Total;
            public int Rank;
            public readonly List<Entry> Chests = new List<Entry>();
        }

        private sealed class Entry
        {
            public Container Chest;
            public string Name;
            public int Count;
            public float Distance;
            public float Height;
        }

        private sealed class RowView
        {
            public RectTransform Rect;
            public Image Background;
            public GameObject Toggle;
            public TMP_Text ToggleText;
            public Image Icon;
            public TMP_Text Name;
            public TMP_Text Detail;
        }

        public static Finder Instance { get; private set; }

        private GameObject m_root;
        private RectTransform m_panel;
        private GuiInputField m_field;
        private TMP_Text m_status;
        private TMP_Text m_empty;
        private TMP_Text m_footer;
        private readonly List<RowView> m_rows = new List<RowView>();
        private Canvas m_canvas;

        private string[] m_words = Array.Empty<string>();
        private readonly List<Group> m_groups = new List<Group>();
        private readonly List<(Group Group, Entry Entry)> m_flat = new List<(Group, Entry)>();
        private readonly HashSet<string> m_expanded = new HashSet<string>();
        private int m_selected;
        private int m_scroll;
        private float m_nextScan;
        private bool m_focusNextFrame;

        public bool IsOpen => m_root && m_root.activeSelf;

        private void Awake() => Instance = this;

        public void Open()
        {
            if (!Settings.Finder.Value || !Player.m_localPlayer || IsOpen) return;
            if (!m_root && !Build()) return;
            m_root.transform.SetAsLastSibling();
            m_root.SetActive(true);
            InputGuard.WindowOpen = true;
            m_field.SetTextWithoutNotify("");
            m_words = Array.Empty<string>();
            m_expanded.Clear();
            m_selected = m_scroll = 0;
            Rebuild();
            m_focusNextFrame = true;
        }

        public void Close()
        {
            if (m_root) m_root.SetActive(false);
            InputGuard.WindowOpen = false;
            InputGuard.MarkActive();
            if (m_field) m_field.DeactivateInputField();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                InputGuard.WindowOpen = false;
                if (Settings.Finder.Value && Player.m_localPlayer && CanOpenNow() && Hotkeys.Pressed(Settings.FinderKey)) Open();
                return;
            }

            Player player = Player.m_localPlayer;
            if (!player || player.IsDead() || !Settings.Finder.Value || Menu.IsVisible())
            {
                Close();
                return;
            }
            if (ZInput.GetKeyDown(KeyCode.Escape, false))
            {
                Close();
                return;
            }
            if (m_focusNextFrame)
            {
                m_focusNextFrame = false;
                m_field.ActivateInputField();
            }

            if (ZInput.GetKeyDown(KeyCode.DownArrow, false)) MoveSelection(1);
            if (ZInput.GetKeyDown(KeyCode.UpArrow, false)) MoveSelection(-1);
            if (ZInput.GetKeyDown(KeyCode.PageDown, false)) MoveSelection(VisibleRows);
            if (ZInput.GetKeyDown(KeyCode.PageUp, false)) MoveSelection(-VisibleRows);
            if (ZInput.GetKeyDown(KeyCode.RightArrow, false)) SetExpanded(m_selected, true);
            if (ZInput.GetKeyDown(KeyCode.LeftArrow, false)) SetExpanded(m_selected, false);
            if (ZInput.GetKeyDown(KeyCode.Return, false) || ZInput.GetKeyDown(KeyCode.KeypadEnter, false))
            {
                // Enter also ends editing in the box: with nothing to pick, put the caret back
                if (m_flat.Count > 0)
                {
                    Pick(m_selected);
                    return;
                }
                m_focusNextFrame = true;
            }

            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel != 0f && IsPointerOver(m_panel)) Scroll(wheel > 0f ? -3 : 3);

            if (Time.time >= m_nextScan) Rebuild();
            Render();
        }

        private void OnDestroy()
        {
            InputGuard.WindowOpen = false;
            if (m_root) Destroy(m_root);
        }

        private static bool CanOpenNow() =>
            !Hotkeys.IsTyping() && !Minimap.IsOpen() && !StoreGui.IsVisible() && !Hud.InRadial() &&
            !Hud.IsPieceSelectionVisible() && !GameCamera.InFreeFly();

        // ---- data ----

        private void Rebuild()
        {
            m_nextScan = Time.time + RescanSeconds;
            (string Key, bool IsChest, Container Chest) selectedKey = SelectedKey();

            NearbyChests.Scan();
            var groups = new Dictionary<string, Group>();
            foreach (var (chest, distance) in NearbyChests.Found)
            {
                var perChest = new Dictionary<string, Entry>();
                foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
                {
                    if (!Matcher.Matches(item, m_words)) continue;
                    string key = item.m_shared.m_name;
                    if (!groups.TryGetValue(key, out Group group))
                    {
                        string name = Matcher.Localize(key);
                        group = new Group { Key = key, Name = name, Icon = item.GetIcon(), Rank = Matcher.NameRank(name, m_words) };
                        groups[key] = group;
                    }
                    group.Total += item.m_stack;
                    if (!perChest.TryGetValue(key, out Entry entry))
                    {
                        entry = new Entry
                        {
                            Chest = chest,
                            Name = Matcher.Localize(chest.GetInventory().GetName()),
                            Distance = distance,
                            Height = chest.transform.position.y - NearbyChests.Origin.y,
                        };
                        perChest[key] = entry;
                        group.Chests.Add(entry);
                    }
                    entry.Count += item.m_stack;
                }
            }

            m_groups.Clear();
            m_groups.AddRange(groups.Values
                .OrderBy(g => g.Rank)
                .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase));

            m_flat.Clear();
            foreach (Group group in m_groups)
            {
                m_flat.Add((group, null));
                if (m_expanded.Contains(group.Key))
                    foreach (Entry entry in group.Chests) m_flat.Add((group, entry));
            }

            // Keep the same row selected across rescans when it still exists
            int index = m_flat.FindIndex(row =>
                row.Group.Key == selectedKey.Key && (row.Entry != null) == selectedKey.IsChest &&
                (!selectedKey.IsChest || row.Entry.Chest == selectedKey.Chest));
            m_selected = index >= 0 ? index : Mathf.Clamp(m_selected, 0, Mathf.Max(0, m_flat.Count - 1));
            KeepSelectionVisible();
        }

        private (string, bool, Container) SelectedKey()
        {
            if (m_selected < 0 || m_selected >= m_flat.Count) return (null, false, null);
            var (group, entry) = m_flat[m_selected];
            return (group.Key, entry != null, entry?.Chest);
        }

        private void OnQueryChanged(string text)
        {
            m_words = Matcher.Parse(text);
            m_selected = m_scroll = 0;
            Rebuild();
            m_selected = m_scroll = 0;
        }

        // ---- actions ----

        private void MoveSelection(int delta)
        {
            if (m_flat.Count == 0) return;
            m_selected = Mathf.Clamp(m_selected + delta, 0, m_flat.Count - 1);
            KeepSelectionVisible();
        }

        private void Scroll(int delta)
        {
            m_scroll = Mathf.Clamp(m_scroll + delta, 0, Mathf.Max(0, m_flat.Count - VisibleRows));
            m_selected = Mathf.Clamp(m_selected, m_scroll, Mathf.Min(m_flat.Count - 1, m_scroll + VisibleRows - 1));
        }

        private void KeepSelectionVisible()
        {
            if (m_selected < m_scroll) m_scroll = m_selected;
            if (m_selected >= m_scroll + VisibleRows) m_scroll = m_selected - VisibleRows + 1;
            m_scroll = Mathf.Clamp(m_scroll, 0, Mathf.Max(0, m_flat.Count - VisibleRows));
        }

        private void SetExpanded(int index, bool expanded)
        {
            if (index < 0 || index >= m_flat.Count) return;
            Group group = m_flat[index].Group;
            if (expanded == m_expanded.Contains(group.Key)) return;
            if (expanded) m_expanded.Add(group.Key);
            else m_expanded.Remove(group.Key);
            // Collapsing from a chest row moves the selection to its item
            m_selected = m_flat.FindIndex(row => row.Group == group && row.Entry == null);
            Rebuild();
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= m_flat.Count) return;
            var (group, entry) = m_flat[index];
            List<Container> chests = entry != null ? new List<Container> { entry.Chest } : group.Chests.Select(e => e.Chest).ToList();
            Markers.Mark(chests, group.Icon);
            Close();

            string where = chests.Count == 1 ? "1 chest" : $"{chests.Count} chests";
            string dismiss = Settings.DismissKey.Value.MainKey != KeyCode.None ? $" ({Settings.DismissKey.Value} clears)" : "";
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"{group.Name}: marked {where}{dismiss}");
        }

        // ---- view ----

        private void Render()
        {
            for (int i = 0; i < m_rows.Count; i++)
            {
                RowView view = m_rows[i];
                int index = m_scroll + i;
                bool used = index < m_flat.Count;
                if (view.Rect.gameObject.activeSelf != used) view.Rect.gameObject.SetActive(used);
                if (!used) continue;

                var (group, entry) = m_flat[index];
                bool hovered = IsPointerOver(view.Rect);
                view.Background.color = index == m_selected
                    ? new Color(1f, 0.63f, 0.24f, 0.28f)
                    : hovered ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0f);

                if (entry == null)
                {
                    bool expandable = group.Chests.Count > 0;
                    view.Toggle.SetActive(expandable);
                    view.ToggleText.text = m_expanded.Contains(group.Key) ? "-" : "+";
                    view.Icon.enabled = group.Icon;
                    view.Icon.sprite = group.Icon;
                    view.Name.rectTransform.offsetMin = new Vector2(78f, 0f);
                    view.Name.text = group.Name;
                    view.Name.color = Ui.TextColor;
                    string chests = group.Chests.Count == 1 ? "1 chest" : $"{group.Chests.Count} chests";
                    view.Detail.text = $"×{group.Total}  ·  {chests}  ·  {group.Chests[0].Distance:0} m";
                }
                else
                {
                    view.Toggle.SetActive(false);
                    view.Icon.enabled = false;
                    view.Name.rectTransform.offsetMin = new Vector2(96f, 0f);
                    view.Name.text = entry.Name;
                    view.Name.color = Ui.DimTextColor;
                    view.Detail.text = $"×{entry.Count}  ·  {entry.Distance:0} m{Height(entry.Height)}";
                }
            }

            int chestCount = NearbyChests.Found.Count;
            string status = chestCount == 1 ? $"1 chest within {Settings.Range.Value} m" : $"{chestCount} chests within {Settings.Range.Value} m";
            if (NearbyChests.LoadedRadius > 0f && Settings.Range.Value > NearbyChests.LoadedRadius)
                status += $"  ·  chests more than ~{NearbyChests.LoadedRadius:0} m from you may not be loaded";
            m_status.text = status;

            m_empty.gameObject.SetActive(m_flat.Count == 0);
            if (m_flat.Count == 0)
                m_empty.text = chestCount == 0 ? "No chests you can open nearby"
                    : m_words.Length > 0 ? "Nothing in nearby chests matches" : "Nearby chests are empty";

            m_footer.text = m_flat.Count > VisibleRows
                ? $"{m_scroll + 1}-{Mathf.Min(m_flat.Count, m_scroll + VisibleRows)} of {m_flat.Count}     Up/Down choose   Right/Left show chests   Enter mark   Esc close"
                : "Up/Down choose   Right/Left show chests   Enter mark   Esc close";
        }

        private static string Height(float height)
        {
            if (height > 2.5f) return $"  ·  {height:0} m up";
            if (height < -2.5f) return $"  ·  {-height:0} m down";
            return "";
        }

        private bool IsPointerOver(RectTransform rect)
        {
            if (!rect || !m_canvas) return false;
            Camera uiCamera = m_canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : m_canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, ZInput.pointerPosition, uiCamera);
        }

        // ---- building the window ----

        private bool Build()
        {
            if (!Hud.instance || !InventoryGui.instance) return false;
            Canvas hudCanvas = Hud.instance.GetComponentInParent<Canvas>();
            Canvas rootCanvas = hudCanvas ? hudCanvas.rootCanvas : null;
            if (!rootCanvas) return false;

            m_root = new GameObject("SearchEverything_Finder", typeof(RectTransform));
            m_root.transform.SetParent(rootCanvas.transform, false);
            Ui.Stretch((RectTransform)m_root.transform, 0f);
            // Its own canvas, drawn above everything else in the game's UI (the inventory included)
            m_canvas = m_root.AddComponent<Canvas>();
            m_canvas.overrideSorting = true;
            int top = 0;
            foreach (Canvas canvas in rootCanvas.GetComponentsInChildren<Canvas>(true))
                if (canvas != m_canvas) top = Mathf.Max(top, canvas.sortingOrder);
            m_canvas.sortingOrder = top + 1;
            m_root.AddComponent<GraphicRaycaster>();

            // A click outside the window closes it
            Image backdrop = Ui.AddImage(m_root.transform, "Backdrop", null, new Color(0f, 0f, 0f, 0.35f));
            Ui.Stretch(backdrop.rectTransform, 0f);
            backdrop.raycastTarget = true;
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            float height = RowsTop + VisibleRows * RowHeight + 46f;
            Image panel = Ui.AddImage(m_root.transform, "Panel", null, Color.white);
            Ui.CopyPanelLook(panel);
            panel.raycastTarget = true;
            m_panel = panel.rectTransform;
            m_panel.anchorMin = m_panel.anchorMax = new Vector2(0.5f, 0.5f);
            m_panel.pivot = new Vector2(0.5f, 0.5f);
            m_panel.anchoredPosition = new Vector2(0f, 30f);
            m_panel.sizeDelta = new Vector2(PanelWidth, height);

            TMP_Text title = Ui.AddText(m_panel, "Title", 26f, TitleColor(), TextAlignmentOptions.Center);
            PlaceTop(title.rectTransform, 12f, 34f, 20f);
            title.text = "Find in nearby chests";

            m_field = SearchField.Create(m_panel, "SearchEverything_FinderSearch", "Item name or type (bow, food...)");
            if (!m_field)
            {
                Destroy(m_root);
                return false;
            }
            PlaceTop((RectTransform)m_field.transform, 52f, 38f, 28f);
            m_field.onValueChanged.AddListener(OnQueryChanged);

            m_status = Ui.AddText(m_panel, "Status", 16f, Ui.DimTextColor, TextAlignmentOptions.Left);
            PlaceTop(m_status.rectTransform, 98f, 24f, 28f);

            for (int i = 0; i < VisibleRows; i++) m_rows.Add(BuildRow(i));

            m_empty = Ui.AddText(m_panel, "Empty", 20f, Ui.DimTextColor, TextAlignmentOptions.Center);
            PlaceTop(m_empty.rectTransform, RowsTop + RowHeight * 2f, 30f, 20f);

            m_footer = Ui.AddText(m_panel, "Footer", 15f, Ui.DimTextColor, TextAlignmentOptions.Center);
            m_footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            m_footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            m_footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            m_footer.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            m_footer.rectTransform.sizeDelta = new Vector2(-40f, 22f);

            m_root.SetActive(false);
            SearchField.LogLayout("Nearby search box", (RectTransform)m_field.transform);
            return true;
        }

        private RowView BuildRow(int i)
        {
            var view = new RowView();
            Image background = Ui.AddImage(m_panel, $"Row{i}", null, new Color(0f, 0f, 0f, 0f));
            background.raycastTarget = true;
            view.Background = background;
            view.Rect = background.rectTransform;
            PlaceTop(view.Rect, RowsTop + i * RowHeight, RowHeight - 2f, 16f);
            int row = i;
            var button = background.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Pick(m_scroll + row));

            Image toggle = Ui.AddImage(view.Rect, "Toggle", Ui.SlotSprite(), new Color(1f, 1f, 1f, 0.5f));
            toggle.raycastTarget = true;
            toggle.rectTransform.anchorMin = toggle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            toggle.rectTransform.pivot = new Vector2(0f, 0.5f);
            toggle.rectTransform.anchoredPosition = new Vector2(6f, 0f);
            toggle.rectTransform.sizeDelta = new Vector2(24f, 24f);
            var toggleButton = toggle.gameObject.AddComponent<Button>();
            toggleButton.navigation = new Navigation { mode = Navigation.Mode.None };
            toggleButton.onClick.AddListener(() =>
            {
                int index = m_scroll + row;
                if (index < m_flat.Count) SetExpanded(index, !m_expanded.Contains(m_flat[index].Group.Key));
                m_focusNextFrame = true;
            });
            view.Toggle = toggle.gameObject;
            view.ToggleText = Ui.AddText(toggle.transform, "Text", 20f, Ui.TextColor, TextAlignmentOptions.Center);
            Ui.Stretch(view.ToggleText.rectTransform, 0f);

            view.Icon = Ui.AddImage(view.Rect, "Icon", null, Color.white);
            view.Icon.preserveAspect = true;
            view.Icon.rectTransform.anchorMin = view.Icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            view.Icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            view.Icon.rectTransform.anchoredPosition = new Vector2(38f, 0f);
            view.Icon.rectTransform.sizeDelta = new Vector2(32f, 32f);

            view.Name = Ui.AddText(view.Rect, "Name", 19f, Ui.TextColor, TextAlignmentOptions.Left);
            view.Name.rectTransform.anchorMin = new Vector2(0f, 0f);
            view.Name.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            view.Name.rectTransform.offsetMin = new Vector2(78f, 0f);
            view.Name.rectTransform.offsetMax = Vector2.zero;

            view.Detail = Ui.AddText(view.Rect, "Detail", 17f, Ui.DimTextColor, TextAlignmentOptions.Right);
            view.Detail.rectTransform.anchorMin = new Vector2(0.55f, 0f);
            view.Detail.rectTransform.anchorMax = new Vector2(1f, 1f);
            view.Detail.rectTransform.offsetMin = new Vector2(8f, 0f);
            view.Detail.rectTransform.offsetMax = new Vector2(-10f, 0f);
            return view;
        }

        /// <summary>A full-width strip, its top this far below the panel's top.</summary>
        private static void PlaceTop(RectTransform rect, float top, float height, float sideMargin)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-2f * sideMargin, height);
        }

        private static Color TitleColor()
        {
            InventoryGui gui = InventoryGui.instance;
            return gui && gui.m_containerName ? gui.m_containerName.color : new Color(1f, 0.76f, 0.4f);
        }
    }
}
