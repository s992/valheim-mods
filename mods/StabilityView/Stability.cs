using UnityEngine;

namespace StabilityView
{
    internal struct MaterialInfo
    {
        public float Max;
        public float Min;
        public float HorizontalLoss;
        public float VerticalLoss;
    }

    // Thin wrappers over WearNTear's private support members (assembly is publicized)
    internal static class Stability
    {
        // Pieces that never compute support (m_noSupportWear off) just sit at their maximum
        public static bool UsesSupport(WearNTear wnt) =>
            wnt.m_noSupportWear && wnt.m_nview != null && wnt.m_nview.IsValid();

        public static float Support(WearNTear wnt) => wnt.GetSupport();

        public static MaterialInfo Material(WearNTear wnt)
        {
            wnt.GetMaterialProperties(out float max, out float min, out float horizontal, out float vertical);
            return new MaterialInfo { Max = max, Min = min, HorizontalLoss = horizontal, VerticalLoss = vertical };
        }

        public static Vector3 CenterOfMass(WearNTear wnt) => wnt.GetCOM();

        // 0 at the collapse threshold, 1 at the material's maximum
        public static float Ratio(float support, MaterialInfo material) =>
            material.Max <= material.Min ? 1f : Mathf.Clamp01((support - material.Min) / (material.Max - material.Min));

        public static bool IsAtMax(float support, MaterialInfo material) => support >= material.Max;

        public static readonly Color MaxColor = new Color(0.6f, 0.8f, 1f);

        // Red at collapse, yellow halfway, green at max; light blue (like vanilla) when at max
        public static Color ColorFor(float support, MaterialInfo material)
        {
            if (IsAtMax(support, material)) return MaxColor;
            float ratio = Ratio(support, material);
            return ratio < 0.5f
                ? Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(1f, 0.9f, 0.2f), ratio * 2f)
                : Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(0.3f, 1f, 0.35f), (ratio - 0.5f) * 2f);
        }
    }
}
