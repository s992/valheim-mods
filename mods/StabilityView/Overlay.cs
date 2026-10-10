using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace StabilityView
{
    internal sealed class Overlay : MonoBehaviour
    {
        public static Overlay Instance { get; private set; }

        private const float RefreshInterval = 0.25f;
        private const float SourceMaxAge = 1f;
        private const int SourcesPerFrame = 25;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color IncreaseColor = new Color(0.35f, 1f, 0.4f);
        private static readonly Color DecreaseColor = new Color(1f, 0.3f, 0.25f);
        private static readonly Color NewColor = new Color(0.55f, 0.8f, 1f);

        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl,
            KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private sealed class Entry
        {
            public WearNTear Piece;
            public Color Color;
            // Rich text for the label, and the same text without tags for its shadow
            public string Text;
            public string Plain;
        }

        private bool _enabled;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<WearNTear, SourceInfo> _sources = new Dictionary<WearNTear, SourceInfo>();
        private HashSet<GameObject> _highlighted = new HashSet<GameObject>();
        private HashSet<GameObject> _nextHighlighted = new HashSet<GameObject>();
        private float _nextRefresh;
        private int _sourceCursor;
        private int _hiddenByLimit;
        private int _hiddenByRadius;
        private GUIStyle _style;
        private GUIStyle _shadowStyle;
        private Material _lineMaterial;
        private bool _lineMaterialFailed;
        private bool _analysisErrorLogged;

        public bool IsShowing { get; private set; }

        private void Awake() => Instance = this;

        private void OnDestroy() => Hide();

        private void Update()
        {
            HandleKeys();

            if (!_enabled || !CanShow())
            {
                if (IsShowing) Hide();
                return;
            }

            IsShowing = true;
            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + RefreshInterval;
                Refresh();
            }
            if (Settings.ShowSourceLines.Value)
                AnalyzeSources();
        }

        private static bool CanShow()
        {
            Player player = Player.m_localPlayer;
            if (!player || !GameCamera.instance) return false;
            return !Settings.OnlyInBuildMode.Value || player.InPlaceMode();
        }

        private void Hide()
        {
            IsShowing = false;
            _entries.Clear();
            _sources.Clear();
            _nextHighlighted.Clear();
            ApplyHighlightChanges();
        }

        // --- input ---

        private void HandleKeys()
        {
            if (IsTyping()) return;

            if (Pressed(Settings.ToggleOverlay))
            {
                _enabled = !_enabled;
                Message(!_enabled ? "Stability overlay off"
                    : Settings.OnlyInBuildMode.Value ? "Stability overlay on (shown while building)"
                    : "Stability overlay on");
            }
            if (Pressed(Settings.ToggleSourceLines))
            {
                Settings.ShowSourceLines.Value = !Settings.ShowSourceLines.Value;
                Message(Settings.ShowSourceLines.Value ? "Support source lines on" : "Support source lines off");
            }
            if (Pressed(Settings.ClearSnapshot))
            {
                Snapshot.Clear();
                Message("Stability snapshot cleared");
            }
            if (Pressed(Settings.TakeSnapshot))
            {
                Snapshot.Capture(manual: true);
                Message(Settings.AutoSnapshot.Value
                    ? "Stability snapshot taken (automatic snapshots paused until cleared)"
                    : "Stability snapshot taken");
            }
        }

        private static bool IsTyping() =>
            Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || InventoryGui.IsVisible()
            || Minimap.InTextInput() || (Chat.instance && Chat.instance.HasFocus());

        // Read through ZInput: the game uses Unity's new input system, which BepInEx's
        // KeyboardShortcut.IsDown() doesn't see. Extra modifiers must not be held, so
        // F8 and Shift+F8 can be bound to different things.
        private static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false))
                return false;
            List<KeyCode> modifiers = shortcut.Modifiers.ToList();
            if (modifiers.Any(key => !ZInput.GetKey(key, false)))
                return false;
            return !ModifierKeys.Any(key => !modifiers.Contains(key) && ZInput.GetKey(key, false));
        }

        private static void Message(string text) =>
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, text);

        // --- data ---

        private void Refresh()
        {
            Camera camera = GameCamera.instance.m_camera;
            if (!camera) return;

            // Measured from the camera rather than the player, so free/build cameras work,
            // and only on-screen pieces count towards the limits
            Vector3 origin = camera.transform.position;
            float radiusSquared = Settings.Radius.Value * Settings.Radius.Value;
            int beyondRadius = 0;

            var nearby = new List<KeyValuePair<float, WearNTear>>();
            foreach (WearNTear wnt in WearNTear.GetAllInstances())
            {
                if (!wnt || !Stability.UsesSupport(wnt)) continue;
                Vector3 com = Stability.CenterOfMass(wnt);
                if (!IsOnScreen(camera, com)) continue;
                float distanceSquared = (com - origin).sqrMagnitude;
                if (distanceSquared <= radiusSquared)
                    nearby.Add(new KeyValuePair<float, WearNTear>(distanceSquared, wnt));
                else
                    beyondRadius++;
            }
            nearby.Sort((a, b) => a.Key.CompareTo(b.Key));

            int limit = Settings.MaxPieces.Value;
            _hiddenByLimit = Math.Max(0, nearby.Count - limit);
            _hiddenByRadius = beyondRadius;

            bool showChanges = Snapshot.Active;
            _entries.Clear();
            _nextHighlighted.Clear();

            foreach (var pair in nearby.Take(limit))
            {
                WearNTear wnt = pair.Value;
                float support = Stability.Support(wnt);
                MaterialInfo material = Stability.Material(wnt);

                float delta = 0f;
                bool changed = false;
                bool isNew = false;
                if (showChanges)
                {
                    if (Snapshot.TryGet(wnt, out float before))
                    {
                        delta = support - before;
                        changed = Mathf.Abs(delta) >= Settings.ChangeThreshold.Value;
                    }
                    else
                    {
                        isNew = true;
                    }
                }

                if (Settings.HideFullySupported.Value && Stability.IsAtMax(support, material) && !changed && !isNew)
                    continue;

                var entry = new Entry { Piece = wnt, Color = Stability.ColorFor(support, material) };
                BuildText(entry, wnt, support, material, changed ? delta : (float?)null, isNew);
                _entries.Add(entry);

                if (Settings.HighlightChanges.Value && (changed || isNew))
                {
                    Color tint = isNew ? NewColor : delta > 0f ? IncreaseColor : DecreaseColor;
                    MaterialMan.instance.SetValue(wnt.gameObject, EmissionColorId, tint * 0.4f);
                    _nextHighlighted.Add(wnt.gameObject);
                }
            }

            ApplyHighlightChanges();

            // Forget sources for pieces that are gone or out of range
            var visible = new HashSet<WearNTear>(_entries.Select(e => e.Piece));
            foreach (WearNTear stale in _sources.Keys.Where(k => !k || !visible.Contains(k)).ToList())
                _sources.Remove(stale);
        }

        private static bool IsOnScreen(Camera camera, Vector3 position)
        {
            Vector3 viewport = camera.WorldToViewportPoint(position);
            return viewport.z > 0f
                && viewport.x >= -0.05f && viewport.x <= 1.05f
                && viewport.y >= -0.05f && viewport.y <= 1.05f;
        }

        private static void BuildText(Entry entry, WearNTear wnt, float support, MaterialInfo material, float? delta, bool isNew)
        {
            var line = new StringBuilder();
            if (Settings.ShowMaterial.Value)
                line.Append(wnt.m_materialType).Append(' ');
            line.Append(support.ToString("0.#"));
            if (Settings.ShowMaxSupport.Value)
                line.Append('/').Append(material.Max.ToString("0"));
            if (Settings.ShowBreakThreshold.Value)
                line.Append(" (min ").Append(material.Min.ToString("0")).Append(')');

            string change = null;
            Color changeColor = Color.white;
            if (isNew)
            {
                change = "new";
                changeColor = NewColor;
            }
            else if (delta.HasValue)
            {
                change = (delta.Value > 0f ? "+" : "") + delta.Value.ToString("0.#");
                changeColor = delta.Value > 0f ? IncreaseColor : DecreaseColor;
            }

            string first = line.ToString();
            entry.Plain = change == null ? first : first + "\n" + change;
            entry.Text = Colored(first, entry.Color) + (change == null ? "" : "\n" + Colored(change, changeColor));
        }

        private static string Colored(string text, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        // Resets tints that are no longer wanted and swaps in the new set
        private void ApplyHighlightChanges()
        {
            foreach (GameObject go in _highlighted)
            {
                if (go && !_nextHighlighted.Contains(go))
                    MaterialMan.instance.ResetValue(go, EmissionColorId);
            }
            (_highlighted, _nextHighlighted) = (_nextHighlighted, _highlighted);
            _nextHighlighted.Clear();
        }

        // Spread the physics queries over frames: a few pieces per frame, each refreshed about once a second
        private void AnalyzeSources()
        {
            if (_entries.Count == 0) return;
            int budget = Math.Min(SourcesPerFrame, _entries.Count);
            for (int n = 0; n < budget; n++)
            {
                _sourceCursor = (_sourceCursor + 1) % _entries.Count;
                WearNTear wnt = _entries[_sourceCursor].Piece;
                if (!wnt) continue;
                if (_sources.TryGetValue(wnt, out SourceInfo cached) && Time.time - cached.AnalyzedAt < SourceMaxAge)
                    continue;
                try
                {
                    _sources[wnt] = SupportAnalyzer.Analyze(wnt);
                }
                catch (Exception e)
                {
                    if (!_analysisErrorLogged)
                    {
                        Debug.LogWarning($"[StabilityView] Support source analysis failed: {e}");
                        _analysisErrorLogged = true;
                    }
                    _sources[wnt] = new SourceInfo { Kind = SourceKind.None, AnalyzedAt = Time.time };
                }
            }
        }

        // --- drawing ---

        private void OnGUI()
        {
            if (!IsShowing || Event.current.type != EventType.Repaint) return;
            Camera camera = GameCamera.instance ? GameCamera.instance.m_camera : null;
            if (!camera) return;

            EnsureStyles();
            if (Settings.ShowStatus.Value)
                DrawStatus();
            foreach (Entry entry in _entries)
            {
                if (!entry.Piece) continue;
                Vector3 screen = camera.WorldToScreenPoint(Stability.CenterOfMass(entry.Piece));
                if (screen.z <= 0f) continue;

                var rect = new Rect(screen.x - 100f, Screen.height - screen.y - 20f, 200f, 40f);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), entry.Plain, _shadowStyle);
                GUI.Label(rect, entry.Text, _style);
            }
        }

        // Says when pieces were left out, so missing labels are never a mystery
        private void DrawStatus()
        {
            var status = new StringBuilder("StabilityView: ").Append(_entries.Count).Append(" shown");
            if (_hiddenByLimit > 0)
                status.Append(" · ").Append(_hiddenByLimit).Append(" hidden by MaxPieces (").Append(Settings.MaxPieces.Value).Append(')');
            if (_hiddenByRadius > 0)
                status.Append(" · ").Append(_hiddenByRadius).Append(" beyond ").Append(Settings.Radius.Value.ToString("0")).Append(" m");
            if (Snapshot.Active)
                status.Append(Snapshot.IsManual
                    ? $" · changes since manual snapshot ({Settings.ClearSnapshot.Value} clears)"
                    : " · showing changes");

            string text = status.ToString();
            // Top centre, under the compass (the bottom is taken by the build hints)
            var rect = new Rect(0f, 44f, Screen.width, 24f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _shadowStyle);
            GUI.Label(rect, text, _style);
        }

        private void EnsureStyles()
        {
            int size = Settings.FontSize.Value;
            if (_style != null && _style.fontSize == size) return;

            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                fontSize = size,
                fontStyle = FontStyle.Bold,
            };
            _style.normal.textColor = Color.white;
            _shadowStyle = new GUIStyle(_style) { richText = false };
            _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        // Lines run from each piece to its support source(s), fading towards the source
        private void OnRenderObject()
        {
            if (!IsShowing || !Settings.ShowSourceLines.Value || _sources.Count == 0) return;
            Camera camera = GameCamera.instance ? GameCamera.instance.m_camera : null;
            if (!camera || Camera.current != camera || !EnsureLineMaterial()) return;

            _lineMaterial.SetInt("_ZTest", (int)(Settings.LinesThroughWalls.Value ? CompareFunction.Always : CompareFunction.LessEqual));
            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            foreach (Entry entry in _entries)
            {
                if (!entry.Piece || !_sources.TryGetValue(entry.Piece, out SourceInfo source)) continue;
                Vector3 from = Stability.CenterOfMass(entry.Piece);
                if (source.A) Line(from, Stability.CenterOfMass(source.A), entry.Color);
                if (source.B) Line(from, Stability.CenterOfMass(source.B), entry.Color);
            }
            GL.End();
            GL.PopMatrix();
        }

        private static void Line(Vector3 from, Vector3 to, Color color)
        {
            GL.Color(color);
            GL.Vertex(from);
            GL.Color(new Color(color.r, color.g, color.b, 0.25f));
            GL.Vertex(to);
        }

        private bool EnsureLineMaterial()
        {
            if (_lineMaterial) return true;
            if (_lineMaterialFailed) return false;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (!shader)
            {
                Debug.LogWarning("[StabilityView] Line shader not found; support source lines are disabled");
                _lineMaterialFailed = true;
                return false;
            }
            _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
            return true;
        }
    }
}
