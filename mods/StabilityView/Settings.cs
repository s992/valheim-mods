using BepInEx.Configuration;
using UnityEngine;

namespace StabilityView
{
    internal static class Settings
    {
        public static ConfigEntry<KeyboardShortcut> ToggleOverlay;
        public static ConfigEntry<KeyboardShortcut> ToggleSourceLines;
        public static ConfigEntry<KeyboardShortcut> TakeSnapshot;
        public static ConfigEntry<KeyboardShortcut> ClearSnapshot;

        public static ConfigEntry<bool> OnlyInBuildMode;
        public static ConfigEntry<float> Radius;
        public static ConfigEntry<int> MaxPieces;
        public static ConfigEntry<bool> ShowStatus;
        public static ConfigEntry<int> FontSize;
        public static ConfigEntry<bool> ShowMaterial;
        public static ConfigEntry<bool> ShowMaxSupport;
        public static ConfigEntry<bool> ShowBreakThreshold;
        public static ConfigEntry<bool> HideFullySupported;

        public static ConfigEntry<bool> ShowSourceLines;
        public static ConfigEntry<bool> LinesThroughWalls;

        public static ConfigEntry<bool> AutoSnapshot;
        public static ConfigEntry<bool> HighlightChanges;
        public static ConfigEntry<float> ChangeThreshold;
        public static ConfigEntry<float> ChangeDuration;

        public static void Bind(ConfigFile config)
        {
            ToggleOverlay = config.Bind("Keys", "ToggleOverlay", new KeyboardShortcut(KeyCode.F7),
                "Show or hide the stability overlay.");
            ToggleSourceLines = config.Bind("Keys", "ToggleSourceLines", new KeyboardShortcut(KeyCode.F9),
                "Show or hide the lines to where each piece gets its support.");
            TakeSnapshot = config.Bind("Keys", "TakeSnapshot", new KeyboardShortcut(KeyCode.F8),
                "Record current support values; changes are then shown relative to them, and automatic snapshots pause until it's cleared.");
            ClearSnapshot = config.Bind("Keys", "ClearSnapshot", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftShift),
                "Forget the snapshot and stop showing changes (resumes automatic snapshots).");

            OnlyInBuildMode = config.Bind("Display", "OnlyInBuildMode", true,
                "Only show the overlay while a building tool (hammer, hoe, ...) is equipped.");
            Radius = config.Bind("Display", "Radius", 30f,
                new ConfigDescription("Show on-screen pieces within this many metres of the camera.", new AcceptableValueRange<float>(2f, 150f)));
            MaxPieces = config.Bind("Display", "MaxPieces", 200,
                new ConfigDescription("Show at most this many on-screen pieces (closest to the camera first).", new AcceptableValueRange<int>(1, 1000)));
            ShowStatus = config.Bind("Display", "ShowStatus", true,
                "Show a status line with how many pieces are labelled and how many were left out.");
            FontSize = config.Bind("Display", "FontSize", 13,
                new ConfigDescription("Label font size.", new AcceptableValueRange<int>(8, 32)));
            ShowMaterial = config.Bind("Display", "ShowMaterial", false,
                "Prefix labels with the piece's material (Wood, Stone, ...).");
            ShowMaxSupport = config.Bind("Display", "ShowMaxSupport", true,
                "Show support as current/max instead of just current.");
            ShowBreakThreshold = config.Bind("Display", "ShowBreakThreshold", false,
                "Show the support value below which the piece collapses.");
            HideFullySupported = config.Bind("Display", "HideFullySupported", false,
                "Skip labels for pieces at their material's maximum (grounded pieces).");

            ShowSourceLines = config.Bind("Support sources", "ShowSourceLines", true,
                "Draw a line from each piece to the piece(s) it gets its support from.");
            LinesThroughWalls = config.Bind("Support sources", "LinesThroughWalls", true,
                "Draw source lines on top of everything instead of hiding them behind geometry.");

            AutoSnapshot = config.Bind("Changes", "AutoSnapshot", true,
                "Take a snapshot automatically just before you place or remove a piece, so each change shows its effect.");
            HighlightChanges = config.Bind("Changes", "HighlightChanges", true,
                "Tint pieces whose support changed since the snapshot (green = up, red = down).");
            ChangeThreshold = config.Bind("Changes", "ChangeThreshold", 0.5f,
                new ConfigDescription("Ignore changes smaller than this.", new AcceptableValueRange<float>(0f, 100f)));
            ChangeDuration = config.Bind("Changes", "ChangeDuration", 0f,
                new ConfigDescription("Stop showing changes this many seconds after the snapshot. 0 = until the next snapshot.", new AcceptableValueRange<float>(0f, 600f)));
        }
    }
}
