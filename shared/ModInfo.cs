namespace ValheimMods.Shared
{
    internal static class ModInfo
    {
        // The assembly is named after the mod's folder, like the plugin (Directory.Build.props)
        public static readonly string Name = typeof(ModInfo).Assembly.GetName().Name;
    }
}
