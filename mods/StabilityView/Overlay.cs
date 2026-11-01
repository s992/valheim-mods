using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimMods.Shared;

namespace StabilityView
{
    internal sealed class Overlay : MonoBehaviour
    {
        public static Overlay Instance { get; private set; }

        private const float RefreshInterval = 0.25f;
        private const float SourceMaxAge = 1f;
        private const int SourcesPerFrame = 25;

        private static readonly int s_emissionColorId = Shader.PropertyToID("_EmissionColor");

        private sealed class Entry
        {
            public WearNTear Piece;
            public Color Color;
            // Rich text for the label, and the same text without tags for its shadow
            public string Text;
            public string Plain;
        }

        private bool m_enabled;
        private readonly List<Entry> m_entries = new List<Entry>();
        private readonly Dictionary<WearNTear, SourceInfo> m_sources = new Dictionary<WearNTear, SourceInfo>();
        private HashSet<GameObject> m_highlighted = new HashSet<GameObject>();
        private HashSet<GameObject> m_nextHighlighted = new HashSet<GameObject>();
        private float m_nextRefresh;
        private int m_sourceCursor;
        private int m_hiddenByLimit;
        private int m_hiddenByRadius;
        private GUIStyle m_style;
        private GUIStyle m_shadowStyle;
        private Material m_lineMaterial;
        private bool m_lineMaterialFailed;
        private bool m_analysisErrorLogged;
        private FrameGuard m_guard;

        public bool IsShowing { get; private set; }

        private void Awake()
        {
            Instance = this;
            m_guard = new FrameGuard(this, "the stability overlay");
        }

        // Also runs when the guard switches this off, so the overlay doesn't stay on screen
        private void OnDisable() => Hide();

        private void OnDestroy() => Hide();

        private void Update()
        {
            try { Tick(); }
            catch (Exception e) { m_guard.Report(e); }
        }

        private void Tick()
        {
            HandleKeys();

            if (!m_enabled || !CanShow())
            {
                if (IsShowing) Hide();
                return;
            }

            IsShowing = true;
            if (Time.time >= m_nextRefresh)
            {
                m_nextRefresh = Time.time + RefreshInterval;
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
            m_entries.Clear();
            m_sources.Clear();
            m_nextHighlighted.Clear();
            ApplyHighlightChanges();
        }

        // --- input ---

        private void HandleKeys()
        {
            if (IsTyping()) return;

            if (Hotkeys.Pressed(Settings.ToggleOverlay))
            {
                m_enabled = !m_enabled;
                Message(!m_enabled ? "Stability overlay off"
                    : Settings.OnlyInBuildMode.Value ? "Stability overlay on (shown while building)"
                    : "Stability overlay on");
            }
            if (Hotkeys.Pressed(Settings.ToggleSourceLines))
            {
                Settings.ShowSourceLines.Value = !Settings.ShowSourceLines.Value;
                Message(Settings.ShowSourceLines.Value ? "Support source lines on" : "Support source lines off");
            }
            if (Hotkeys.Pressed(Settings.ClearSnapshot))
            {
                Snapshot.Clear();
                Message("Stability snapshot cleared");
            }
            if (Hotkeys.Pressed(Settings.TakeSnapshot))
            {
                Snapshot.Capture(manual: true);
                Message(Settings.AutoSnapshot.Value
                    ? "Stability snapshot taken (automatic snapshots paused until cleared)"
                    : "Stability snapshot taken");
            }
        }

        // The overlay's keys are off in the inventory too
        private static bool IsTyping() => Hotkeys.IsTyping() || InventoryGui.IsVisible();

        private static void Message(string text)
        {
            if (Player.m_localPlayer) Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, text);
        }

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
            m_hiddenByLimit = Math.Max(0, nearby.Count - limit);
            m_hiddenByRadius = beyondRadius;

            bool showChanges = Snapshot.Active;
            m_entries.Clear();
            m_nextHighlighted.Clear();

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

                var (text, plain) = Labels.Piece(wnt.m_materialType.ToString(), support, material, changed ? delta : (float?)null, isNew);
                m_entries.Add(new Entry { Piece = wnt, Color = Stability.ColorFor(support, material), Text = text, Plain = plain });

                if (Settings.HighlightChanges.Value && (changed || isNew))
                {
                    Color tint = Labels.ChangeColor(delta, isNew);
                    MaterialMan.instance.SetValue(wnt.gameObject, s_emissionColorId, tint * 0.4f);
                    m_nextHighlighted.Add(wnt.gameObject);
                }
            }

            ApplyHighlightChanges();

            // Forget sources for pieces that are gone or out of range
            var visible = new HashSet<WearNTear>(m_entries.Select(e => e.Piece));
            foreach (WearNTear stale in m_sources.Keys.Where(k => !k || !visible.Contains(k)).ToList())
                m_sources.Remove(stale);
        }

        private static bool IsOnScreen(Camera camera, Vector3 position)
        {
            Vector3 viewport = camera.WorldToViewportPoint(position);
            return viewport.z > 0f
                && viewport.x >= -0.05f && viewport.x <= 1.05f
                && viewport.y >= -0.05f && viewport.y <= 1.05f;
        }

        // Resets tints that are no longer wanted and swaps in the new set
        private void ApplyHighlightChanges()
        {
            foreach (GameObject go in m_highlighted)
            {
                if (go && !m_nextHighlighted.Contains(go))
                    MaterialMan.instance.ResetValue(go, s_emissionColorId);
            }
            (m_highlighted, m_nextHighlighted) = (m_nextHighlighted, m_highlighted);
            m_nextHighlighted.Clear();
        }

        // Spread the physics queries over frames: a few pieces per frame, each refreshed about once a second
        private void AnalyzeSources()
        {
            if (m_entries.Count == 0) return;
            int budget = Math.Min(SourcesPerFrame, m_entries.Count);
            for (int n = 0; n < budget; n++)
            {
                m_sourceCursor = (m_sourceCursor + 1) % m_entries.Count;
                WearNTear wnt = m_entries[m_sourceCursor].Piece;
                if (!wnt) continue;
                if (m_sources.TryGetValue(wnt, out SourceInfo cached) && Time.time - cached.AnalyzedAt < SourceMaxAge)
                    continue;
                try
                {
                    m_sources[wnt] = SupportAnalyzer.Analyze(wnt);
                }
                catch (Exception e)
                {
                    if (!m_analysisErrorLogged)
                    {
                        ModInfo.Log.LogWarning($"Support source analysis failed: {e}");
                        m_analysisErrorLogged = true;
                    }
                    m_sources[wnt] = new SourceInfo { Kind = SourceKind.None, AnalyzedAt = Time.time };
                }
            }
        }

        // --- drawing ---

        private void OnGUI()
        {
            try { Draw(); }
            catch (Exception e) { m_guard.Report(e); }
        }

        private void Draw()
        {
            if (!IsShowing || Event.current.type != EventType.Repaint) return;
            Camera camera = GameCamera.instance ? GameCamera.instance.m_camera : null;
            if (!camera) return;

            EnsureStyles();
            if (Settings.ShowStatus.Value)
                DrawStatus();
            foreach (Entry entry in m_entries)
            {
                if (!entry.Piece) continue;
                Vector3 screen = camera.WorldToScreenPoint(Stability.CenterOfMass(entry.Piece));
                if (screen.z <= 0f) continue;

                var rect = new Rect(screen.x - 100f, Screen.height - screen.y - 20f, 200f, 40f);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), entry.Plain, m_shadowStyle);
                GUI.Label(rect, entry.Text, m_style);
            }
        }

        private void DrawStatus()
        {
            string text = Labels.Status(m_entries.Count, m_hiddenByLimit, m_hiddenByRadius, Snapshot.Active, Snapshot.IsManual);
            // Top centre, under the compass (the bottom is taken by the build hints)
            var rect = new Rect(0f, 44f, Screen.width, 24f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, m_shadowStyle);
            GUI.Label(rect, text, m_style);
        }

        private void EnsureStyles()
        {
            int size = Settings.FontSize.Value;
            if (m_style != null && m_style.fontSize == size) return;

            m_style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                fontSize = size,
                fontStyle = FontStyle.Bold,
            };
            m_style.normal.textColor = Color.white;
            m_shadowStyle = new GUIStyle(m_style) { richText = false };
            m_shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        // Lines run from each piece to its support source(s), fading towards the source
        private void OnRenderObject()
        {
            if (!IsShowing || !Settings.ShowSourceLines.Value || m_sources.Count == 0) return;
            Camera camera = GameCamera.instance ? GameCamera.instance.m_camera : null;
            if (!camera || Camera.current != camera || !EnsureLineMaterial()) return;

            m_lineMaterial.SetInt("_ZTest", (int)(Settings.LinesThroughWalls.Value ? CompareFunction.Always : CompareFunction.LessEqual));
            m_lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            foreach (Entry entry in m_entries)
            {
                if (!entry.Piece || !m_sources.TryGetValue(entry.Piece, out SourceInfo source)) continue;
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
            if (m_lineMaterial) return true;
            if (m_lineMaterialFailed) return false;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (!shader)
            {
                ModInfo.Log.LogWarning("Line shader not found; support source lines are disabled");
                m_lineMaterialFailed = true;
                return false;
            }
            m_lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            m_lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m_lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m_lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            m_lineMaterial.SetInt("_ZWrite", 0);
            return true;
        }
    }
}
