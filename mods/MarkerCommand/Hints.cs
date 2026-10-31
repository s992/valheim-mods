using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MarkerCommand
{
    // Parameter hints via Server Devcommands, looked up by reflection so it isn't a build dependency
    internal static class Hints
    {
        public const string ServerDevcommandsGuid = "server_devcommands";

        public static bool Register()
        {
            Type autoComplete = AccessTools.TypeByName("ServerDevcommands.AutoComplete");
            if (autoComplete == null) return false;

            // Newer versions pass the typed parameters, which lets the hints follow "me" mode
            MethodInfo withArgs = AccessTools.Method(autoComplete, "Register",
                new[] { typeof(string), typeof(Func<int, int, string[], List<string>>) });
            if (withArgs != null)
            {
                Func<int, int, string[], List<string>> fetcher = (index, subIndex, args) => GetHint(index, args);
                withArgs.Invoke(null, new object[] { Marker.Command, fetcher });
                return true;
            }

            MethodInfo withoutArgs = AccessTools.Method(autoComplete, "Register",
                new[] { typeof(string), typeof(Func<int, int, List<string>>) });
            if (withoutArgs != null)
            {
                Func<int, int, List<string>> fetcher = (index, subIndex) => GetHint(index, Array.Empty<string>());
                withoutArgs.Invoke(null, new object[] { Marker.Command, fetcher });
                return true;
            }

            return false;
        }

        // Entries starting with "?" are shown as a hint; a list without them is offered as completions
        private static List<string> Hint(string text) => new List<string> { "?" + text };

        // args are the parameters typed after the command name
        private static List<string> GetHint(int index, string[] args)
        {
            bool meMode = args.Length >= 1 && args[0].Equals("me", StringComparison.OrdinalIgnoreCase);
            int nameIndex = meMode ? 1 : 2;
            if (index == 0)
                return Hint("X | world east-west coordinate, or <color=yellow>me</color> for your current position");
            if (index == 1 && !meMode)
                return Hint("Z | world north-south coordinate");
            if (index == nameIndex)
                return Hint("Name | optional label, \"quote\" it if it has spaces (or skip straight to an icon)");
            return Marker.AvailableIcons.Select(Marker.IconName).ToList();
        }
    }
}
