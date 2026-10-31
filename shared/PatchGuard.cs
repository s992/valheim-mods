using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Logging;

namespace ValheimMods.Shared
{
    // An exception thrown by a patch escapes into the game method around it and on into the game's own Update, which
    // then skips the rest of its frame, every frame: a bug in a radial-menu prefix can leave the player unable to move.
    // For patches on game methods that run every frame, add a finalizer:
    //
    //     private static Exception Finalizer(Exception __exception) => PatchGuard.Contain(__exception, "the tool menu");
    //
    // Exceptions that come from this mod's code are logged once, shown to the player, and stop there (the game method
    // ends early for that frame, returning its default). Exceptions from the game's own code pass through unchanged.
    // Don't use it where ending early would let something through, like paying for a craft: there the exception must
    // stop the action.
    internal static class PatchGuard
    {
        private static readonly HashSet<string> s_reported = new HashSet<string>();
        private static ManualLogSource s_log;

        /// <param name="feature">What the player would call it, e.g. "the tool menu".</param>
        /// <returns>Null to swallow the exception (ours), or the exception itself to rethrow it (the game's).</returns>
        public static Exception Contain(Exception exception, string feature)
        {
            if (exception == null || !FromThisMod(exception)) return exception;
            if (s_reported.Add(feature))
            {
                s_log ??= Logger.CreateLogSource(ModInfo.Name);
                s_log.LogError($"Error in {feature} (further errors there aren't logged): {exception}");
                PlayerNotice.Show($"{ModInfo.Name}: {feature} hit an error and may not work until the game restarts. " +
                                  "Details are in BepInEx/LogOutput.log.");
            }
            return null;
        }

        private static bool FromThisMod(Exception exception)
        {
            Assembly ours = typeof(PatchGuard).Assembly;
            for (Exception e = exception; e != null; e = e.InnerException)
            {
                foreach (StackFrame frame in new StackTrace(e, false).GetFrames() ?? Array.Empty<StackFrame>())
                    if (frame.GetMethod()?.DeclaringType?.Assembly == ours) return true;
            }
            return false;
        }
    }
}
