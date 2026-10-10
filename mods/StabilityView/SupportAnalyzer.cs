using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StabilityView
{
    internal enum SourceKind
    {
        // Touching terrain: full support
        Terrain,
        // Touching a non-building object (rock, tree, ...): full support
        StaticObject,
        // Best offer from a single neighbouring piece
        Piece,
        // Average of two supports below on opposite sides
        Braced,
        // Nothing found (the piece is about to collapse)
        None,
    }

    internal sealed class SourceInfo
    {
        public SourceKind Kind;
        public WearNTear A;
        public WearNTear B;
        // What the mirrored calculation arrives at, to compare against the game's value
        public float Value;
        public float AnalyzedAt;
    }

    // Read-only mirror of WearNTear.UpdateSupport (game 1.0.17) that also records which
    // neighbour produced the winning value. The game only keeps the value itself.
    internal static class SupportAnalyzer
    {
        private struct SupportPoint
        {
            public Vector3 Point;
            public float Value;
            public WearNTear Piece;
        }

        private static readonly Collider[] Overlaps = new Collider[128];
        private static readonly List<SupportPoint> Points = new List<SupportPoint>();

        public static SourceInfo Analyze(WearNTear wnt)
        {
            var info = new SourceInfo { Kind = SourceKind.None, AnalyzedAt = Time.time };

            // Only the owner sets these up; doing it here is the same caching the game would do
            if (wnt.m_colliders == null) wnt.SetupColliders();

            MaterialInfo material = Stability.Material(wnt);
            Vector3 com = wnt.GetCOM();
            bool touchesTerrain = false;
            float best = 0f;
            WearNTear bestPiece = null;
            Points.Clear();

            foreach (var bound in wnt.m_bounds)
            {
                int count = Physics.OverlapBoxNonAlloc(bound.m_pos, bound.m_size, Overlaps, bound.m_rot, WearNTear.s_rayMask);
                for (int i = 0; i < count; i++)
                {
                    Collider collider = Overlaps[i];
                    if (collider.attachedRigidbody != null || collider.isTrigger || wnt.m_colliders.Contains(collider))
                        continue;

                    if (collider.gameObject.layer == WearNTear.s_terrainLayer)
                    {
                        touchesTerrain = true;
                        continue;
                    }

                    WearNTear other = collider.GetComponentInParent<WearNTear>();
                    if (other == null)
                    {
                        info.Kind = SourceKind.StaticObject;
                        info.Value = material.Max;
                        return info;
                    }
                    if (!other.m_supports)
                        continue;

                    float distance = Vector3.Distance(com, other.GetCOM()) + 0.1f;
                    float pivotDistance = Vector3.Distance(com, other.transform.position) + 0.1f;
                    if (pivotDistance < distance && !wnt.m_forceCorrectCOMCalculation)
                        distance = pivotDistance;

                    float support = other.GetSupport();
                    Consider(support - material.HorizontalLoss * distance * support, other, ref best, ref bestPiece);

                    Vector3 point = WearNTear.FindSupportPoint(com, other, collider);
                    if (point.y < com.y + 0.05f)
                    {
                        Vector3 direction = (point - com).normalized;
                        if (direction.y < 0f)
                        {
                            // Straight down uses the vertical loss, sideways the horizontal loss
                            float t = Mathf.Acos(1f - Mathf.Abs(direction.y)) / (Mathf.PI / 2f);
                            float loss = Mathf.Lerp(material.HorizontalLoss, material.VerticalLoss, t);
                            Consider(support - loss * distance * support, other, ref best, ref bestPiece);
                        }
                        Points.Add(new SupportPoint
                        {
                            Point = point,
                            Value = support - material.VerticalLoss * distance * support,
                            Piece = other,
                        });
                    }
                }
            }

            if (touchesTerrain)
            {
                info.Kind = SourceKind.Terrain;
                info.Value = material.Max;
                return info;
            }

            if (bestPiece != null)
            {
                info.Kind = SourceKind.Piece;
                info.A = bestPiece;
            }

            // Bracing: two supports below, at least 100 degrees apart around the piece
            for (int i = 0; i < Points.Count - 1; i++)
            {
                Vector3 from = Points[i].Point - com;
                from.y = 0f;
                for (int j = i + 1; j < Points.Count; j++)
                {
                    float average = (Points[i].Value + Points[j].Value) * 0.5f;
                    if (average <= best) continue;
                    Vector3 to = Points[j].Point - com;
                    to.y = 0f;
                    if (Vector3.Angle(from, to) >= 100f)
                    {
                        best = average;
                        info.Kind = SourceKind.Braced;
                        info.A = Points[i].Piece;
                        info.B = Points[j].Piece;
                    }
                }
            }

            info.Value = Mathf.Min(best, material.Max);
            return info;
        }

        private static void Consider(float value, WearNTear piece, ref float best, ref WearNTear bestPiece)
        {
            if (value > best)
            {
                best = value;
                bestPiece = piece;
            }
        }
    }
}
