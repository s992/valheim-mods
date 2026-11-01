using System.Globalization;
using System.Text;
using UnityEngine;

namespace StabilityView
{
    // The overlay's text: one label per piece and the status line. Plain values in, strings out, so it's testable.
    internal static class Labels
    {
        public static readonly Color IncreaseColor = new Color(0.35f, 1f, 0.4f);
        public static readonly Color DecreaseColor = new Color(1f, 0.3f, 0.25f);
        public static readonly Color NewColor = new Color(0.55f, 0.8f, 1f);

        /// <summary>Colour for a piece that changed since the snapshot, or is new since it.</summary>
        public static Color ChangeColor(float delta, bool isNew) => isNew ? NewColor : delta > 0f ? IncreaseColor : DecreaseColor;

        /// <summary>
        /// A piece's label as rich text, and the same text without tags (drawn underneath as its shadow).
        /// <paramref name="delta"/> is the change since the snapshot, null when it's below the threshold.
        /// </summary>
        public static (string Text, string Plain) Piece(string materialType, float support, MaterialInfo material, float? delta, bool isNew)
        {
            var line = new StringBuilder();
            if (Settings.ShowMaterial.Value)
                line.Append(materialType).Append(' ');
            line.Append(support.ToString("0.#", CultureInfo.CurrentCulture));
            if (Settings.ShowMaxSupport.Value)
                line.Append('/').Append(material.Max.ToString("0", CultureInfo.CurrentCulture));
            if (Settings.ShowBreakThreshold.Value)
                line.Append(" (min ").Append(material.Min.ToString("0", CultureInfo.CurrentCulture)).Append(')');

            string change = isNew ? "new"
                : delta.HasValue ? (delta.Value > 0f ? "+" : "") + delta.Value.ToString("0.#", CultureInfo.CurrentCulture)
                : null;

            string first = line.ToString();
            Color color = Stability.ColorFor(support, material);
            string text = Colored(first, color) + (change == null ? "" : "\n" + Colored(change, ChangeColor(delta ?? 0f, isNew)));
            return (text, change == null ? first : first + "\n" + change);
        }

        // Says when pieces were left out, so missing labels are never a mystery
        public static string Status(int shown, int hiddenByLimit, int hiddenByRadius, bool snapshotActive, bool manualSnapshot)
        {
            var status = new StringBuilder("StabilityView: ").Append(shown).Append(" shown");
            if (hiddenByLimit > 0)
                status.Append(" · ").Append(hiddenByLimit).Append(" hidden by MaxPieces (").Append(Settings.MaxPieces.Value).Append(')');
            if (hiddenByRadius > 0)
                status.Append(" · ").Append(hiddenByRadius).Append(" beyond ")
                    .Append(Settings.Radius.Value.ToString("0", CultureInfo.CurrentCulture)).Append(" m");
            if (snapshotActive)
                status.Append(manualSnapshot
                    ? $" · changes since manual snapshot ({Settings.ClearSnapshot.Value} clears)"
                    : " · showing changes");
            return status.ToString();
        }

        private static string Colored(string text, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }
}
