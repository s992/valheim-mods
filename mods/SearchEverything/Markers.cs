using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimMods.Shared;

namespace SearchEverything
{
    /// <summary>
    /// On-screen markers over chests picked in the nearby search: the item's icon and the distance, drawn on the HUD
    /// so they show through walls. A marker goes when its chest is opened, after the marker duration, or on the
    /// dismiss key.
    /// </summary>
    internal class Markers : MonoBehaviour
    {
        private sealed class Marker
        {
            public Container Chest;
            public float Until;
            public RectTransform Rect;
            public TMP_Text Text;
        }

        private static readonly List<Marker> s_markers = new List<Marker>();
        private static RectTransform s_layer;
        private static Canvas s_canvas;

        public static int Count => s_markers.Count;

        public static void Mark(IEnumerable<Container> chests, Sprite icon)
        {
            Clear();
            if (!EnsureLayer()) return;
            float until = Settings.MarkerSeconds.Value > 0 ? Time.time + Settings.MarkerSeconds.Value : float.MaxValue;
            foreach (Container chest in chests)
                if (chest) s_markers.Add(Create(chest, icon, until));
        }

        public static void Clear()
        {
            foreach (Marker marker in s_markers)
                if (marker.Rect) Destroy(marker.Rect.gameObject);
            s_markers.Clear();
        }

        private FrameGuard m_guard;

        private void Awake() => m_guard = new FrameGuard(this, "the chest markers");

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                m_guard.Report(e);
            }
        }

        private void LateUpdate()
        {
            try
            {
                Position();
            }
            catch (Exception e)
            {
                m_guard.Report(e);
            }
        }

        // The guard disables this component after repeated errors: markers it no longer moves would be wrong
        private void OnDisable() => Clear();

        private static void Tick()
        {
            if (s_markers.Count == 0) return;
            if (!Player.m_localPlayer || !s_layer)
            {
                Clear();
                return;
            }
            if (Hotkeys.Pressed(Settings.DismissKey) && !SearchKeys.IsTyping())
            {
                Clear();
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Chest markers cleared");
                return;
            }
            Container open = InventoryGui.instance ? InventoryGui.instance.m_currentContainer : null;
            for (int i = s_markers.Count - 1; i >= 0; i--)
            {
                Marker marker = s_markers[i];
                if (marker.Chest && marker.Chest != open && Time.time < marker.Until && marker.Rect) continue;
                if (marker.Rect) Destroy(marker.Rect.gameObject);
                s_markers.RemoveAt(i);
            }
        }

        private static void Position()
        {
            if (s_markers.Count == 0) return;
            Camera camera = Utils.GetMainCamera();
            if (!camera || !s_canvas) return;
            Camera uiCamera = s_canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : s_canvas.worldCamera;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
            foreach (Marker marker in s_markers)
            {
                Vector3 world = marker.Chest.transform.position + Vector3.up * 1.4f;
                Vector3 screen = camera.WorldToScreenPoint(world);
                Vector2 local = Vector2.zero;
                // Behind the camera: no edge arrows (not asked for), just hidden until it's in front again
                bool visible = screen.z > 0f &&
                               RectTransformUtility.ScreenPointToLocalPointInRectangle(s_layer, screen, uiCamera, out local);
                if (marker.Rect.gameObject.activeSelf != visible) marker.Rect.gameObject.SetActive(visible);
                if (!visible) continue;
                marker.Rect.anchoredPosition = local;
                marker.Rect.localScale = Vector3.one * pulse;
                marker.Text.text = $"{Vector3.Distance(camera.transform.position, marker.Chest.transform.position):0} m";
            }
        }

        private void OnDestroy() => Clear();

        // On the HUD, so markers hide with it
        private static bool EnsureLayer()
        {
            if (s_layer) return true;
            if (!Hud.instance || !Hud.instance.m_rootObject) return false;
            var go = new GameObject("SearchEverything_Markers", typeof(RectTransform));
            go.transform.SetParent(Hud.instance.m_rootObject.transform, false);
            s_layer = (RectTransform)go.transform;
            s_layer.anchorMin = Vector2.zero;
            s_layer.anchorMax = Vector2.one;
            s_layer.offsetMin = s_layer.offsetMax = Vector2.zero;
            Canvas canvas = go.GetComponentInParent<Canvas>();
            s_canvas = canvas ? canvas.rootCanvas : null;
            return true;
        }

        private static Marker Create(Container chest, Sprite icon, float until)
        {
            var root = new GameObject("Marker", typeof(RectTransform));
            root.transform.SetParent(s_layer, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(48f, 48f);

            Image frame = Ui.AddImage(root.transform, "Frame", Ui.SlotSprite(), new Color(0f, 0f, 0f, 0.6f));
            Ui.Stretch(frame.rectTransform, 0f);
            if (icon)
            {
                Image image = Ui.AddImage(root.transform, "Icon", icon, Color.white);
                Ui.Stretch(image.rectTransform, 6f);
                image.preserveAspect = true;
            }
            Image plate = Ui.AddImage(root.transform, "DistancePlate", null, new Color(0f, 0f, 0f, 0.6f));
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            plate.rectTransform.pivot = new Vector2(0.5f, 1f);
            plate.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            plate.rectTransform.sizeDelta = new Vector2(64f, 22f);
            TMP_Text text = Ui.AddText(plate.transform, "Distance", 17f, Color.white, TextAlignmentOptions.Center);
            Ui.Stretch(text.rectTransform, 0f);

            return new Marker { Chest = chest, Until = until, Rect = rect, Text = text };
        }
    }
}
