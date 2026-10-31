using System.Collections.Generic;
using HarmonyLib;

namespace MarkerCommand
{
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    internal static class RegisterCommand
    {
        private static void Postfix()
        {
            new Terminal.ConsoleCommand(Marker.Command,
                "<x> <z> [name] [icon] | me [name] [icon] - adds a map marker at world coordinates or your position",
                Marker.Run,
                // Vanilla tab completion only covers the first parameter
                optionsFetcher: () => new List<string> { "me" });
        }
    }
}
