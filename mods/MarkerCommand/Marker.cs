using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MarkerCommand
{
    internal struct Token
    {
        public string Text;
        public bool Quoted;
    }

    internal static class Marker
    {
        public const string Command = "marker";
        public const Minimap.PinType DefaultIcon = Minimap.PinType.Icon3;

        // Splits on spaces, keeping "quoted strings" together (an unclosed quote runs to the end)
        public static List<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < input.Length)
            {
                if (input[i] == ' ')
                {
                    i++;
                    continue;
                }
                if (input[i] == '"')
                {
                    int end = input.IndexOf('"', i + 1);
                    if (end < 0) end = input.Length;
                    tokens.Add(new Token { Text = input.Substring(i + 1, end - i - 1), Quoted = true });
                    i = end + 1;
                }
                else
                {
                    int end = input.IndexOf(' ', i);
                    if (end < 0) end = input.Length;
                    tokens.Add(new Token { Text = input.Substring(i, end - i), Quoted = false });
                    i = end;
                }
            }
            return tokens;
        }

        public static bool IsMe(Token token) =>
            !token.Quoted && token.Text.Equals("me", StringComparison.OrdinalIgnoreCase);

        // Every pin type except None, which has no sprite
        public static IEnumerable<Minimap.PinType> AllIcons =>
            ((Minimap.PinType[])Enum.GetValues(typeof(Minimap.PinType))).Where(t => t != Minimap.PinType.None);

        // Icons with a sprite loaded; falls back to all of them before the map exists
        public static IEnumerable<Minimap.PinType> AvailableIcons =>
            Minimap.instance ? AllIcons.Where(HasSprite) : AllIcons;

        public static bool HasSprite(Minimap.PinType type) =>
            Minimap.instance.m_icons.Any(s => s.m_name == type && s.m_icon != null);

        // Friendly names for the map's five placeable icons; the enum names still work too
        private static readonly Dictionary<Minimap.PinType, string> Aliases = new Dictionary<Minimap.PinType, string>
        {
            { Minimap.PinType.Icon0, "fire" },
            { Minimap.PinType.Icon1, "house" },
            { Minimap.PinType.Icon2, "hammer" },
            { Minimap.PinType.Icon3, "dot" },
            { Minimap.PinType.Icon4, "rune" },
        };

        private static string EnumName(Minimap.PinType type) => type.ToString().ToLowerInvariant();

        public static string IconName(Minimap.PinType type) =>
            Aliases.TryGetValue(type, out string alias) ? alias : EnumName(type);

        public static bool TryParseIcon(Token token, out Minimap.PinType icon)
        {
            icon = DefaultIcon;
            if (token.Quoted) return false;
            string text = token.Text.ToLowerInvariant();
            foreach (var type in AllIcons)
            {
                if (IconName(type) == text || EnumName(type) == text)
                {
                    icon = type;
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseFloat(Token token, out float value) =>
            float.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        public static void Run(Terminal.ConsoleEventArgs args)
        {
            if (!Minimap.instance)
            {
                args.Context?.AddString("Map is not available (join a world first).");
                return;
            }

            List<Token> tokens = Tokenize(args.ArgsAll ?? "");
            Vector3 pos;
            int nameStart;

            if (tokens.Count >= 1 && IsMe(tokens[0]))
            {
                if (!Player.m_localPlayer)
                {
                    args.Context?.AddString("No local player.");
                    return;
                }
                pos = Player.m_localPlayer.transform.position;
                nameStart = 1;
            }
            else if (tokens.Count >= 2 && TryParseFloat(tokens[0], out float x) && TryParseFloat(tokens[1], out float z))
            {
                pos = new Vector3(x, 0f, z);
                nameStart = 2;
            }
            else
            {
                args.Context?.AddString("Usage: marker <x> <z> [name] [icon]  or  marker me [name] [icon]");
                return;
            }

            // An unquoted final word that matches an icon name is the icon
            List<Token> rest = tokens.Skip(nameStart).ToList();
            Minimap.PinType icon = DefaultIcon;
            if (rest.Count > 0 && TryParseIcon(rest[rest.Count - 1], out icon))
            {
                rest.RemoveAt(rest.Count - 1);
                if (!HasSprite(icon))
                {
                    args.Context?.AddString($"Icon '{IconName(icon)}' has no sprite. Available: {string.Join(", ", AvailableIcons.Select(IconName))}");
                    return;
                }
            }

            string name = string.Join(" ", rest.Select(t => t.Text));
            Minimap.instance.AddPin(pos, icon, name, save: true, isChecked: false);
            args.Context?.AddString($"Added {IconName(icon)} marker '{name}' at ({pos.x:F0}, {pos.z:F0})");
        }
    }
}
