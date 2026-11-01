using System.Reflection;

namespace ValheimMods.Shared
{
    internal static class ModInfo
    {
        // The assembly is named after the mod's folder, like the plugin (Directory.Build.props)
        public static readonly string Name = typeof(ModInfo).Assembly.GetName().Name;

        /// <summary>
        /// The csproj version; local builds add "-dev+" and the build time (Directory.Build.targets), so a log shows
        /// whether a dev build or the released one loaded.
        /// </summary>
        public static readonly string Version =
            typeof(ModInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            typeof(ModInfo).Assembly.GetName().Version.ToString(3);
    }
}
