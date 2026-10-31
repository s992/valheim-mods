# Code patterns that worked

Patterns from the mods in this repo, all shipped or tested in game.
Adapt them rather than pasting blindly; the folders under `mods/` have full versions.

## Contents
- Project shape
- Config (Configuration Manager friendly)
- Hotkeys
- Optional integration with another mod
- Harmony: prefix, postfix, transpiler
- Failing safely (shared/)
- Changing game objects reversibly
- Keeping per-frame work cheap
- Feedback to the player

## Project shape

One class per concern keeps files small and reviewable, e.g. `Plugin.cs` (wiring only), `Settings.cs`
(all ConfigEntries), `Rules.cs` / domain logic, `Patches.cs` (Harmony), one MonoBehaviour per runtime job
(`Overlay`, `DropEffects`, `Marker`), `Input.cs`, `<OtherMod>Compat.cs`.

```csharp
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(EpicLootCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)] // only if integrating
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        Settings.Bind(Config);
        EpicLootCompat.Init(Logger);
        // All or nothing (see "Failing safely"); nothing else starts when it fails
        if (!SafePatching.PatchAll(new Harmony(MyPluginInfo.PLUGIN_GUID), Logger)) return;
        gameObject.AddComponent<Overlay>();   // MonoBehaviours live on the plugin's GameObject
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
    }
}
```

`MyPluginInfo` is generated from `<BepInExPluginGuid>`, `<BepInExPluginName>` and `<Version>` in the csproj;
the config file is `BepInEx/config/<GUID>.cfg`, so the GUID `s992.<Name>` gives `s992.<Name>.cfg`.

## Config (Configuration Manager friendly)

s992 uses shudnal's ConfigurationManager, which was checked to support:
- `[Flags]` enums → a row of **checkboxes**. Great for sets (rarities, item groups, biomes). Define your own
  flags enum with friendly members rather than exposing raw game enums.
- `Color` → a colour picker with alpha. Good for "colour + opacity" options (alpha 0 = invisible).
- `KeyboardShortcut` → a key-capture field.
- `AcceptableValueRange<T>` → a slider. `AcceptableValueList` → a dropdown.
- Lists of arbitrary strings have no widget; use a comma-separated string and offer an in-game way to edit it
  (e.g. a hotkey that adds the hovered item's internal name).

```csharp
Radius = config.Bind("Display", "Radius", 30f,
    new ConfigDescription("Show on-screen pieces within this many metres of the camera.",
        new AcceptableValueRange<float>(2f, 150f)));
```

- Write descriptions for players: what it does and what the values mean.
- React to changes with `entry.SettingChanged += ...`; bump a `Version` counter that cached results compare against.
- BepInEx keeps saved values when a default changes, so a new default doesn't reach existing users or the
  developer's own config. Say so when changing a default, and suggest deleting the line.
- Renaming a setting leaves the old one orphaned in existing config files (harmless). Mention it.

## Hotkeys

```csharp
private static readonly KeyCode[] ModifierKeys =
    { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };

// Exact match through ZInput (the new input system): extra modifiers must not be held,
// so F8 and Shift+F8 can be bound to different actions
public static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
{
    KeyboardShortcut shortcut = entry.Value;
    if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false)) return false;
    var modifiers = shortcut.Modifiers.ToList();
    if (modifiers.Any(key => !ZInput.GetKey(key, false))) return false;
    return !ModifierKeys.Any(key => !modifiers.Contains(key) && ZInput.GetKey(key, false));
}
```

Guard with an `IsTyping()` check (game-api.md lists the calls). For text the player sees (status lines,
messages), print the configured shortcut (`Settings.X.Value.ToString()`) rather than hard-coding "Shift+F8".

## Optional integration with another mod

Reference nothing at compile time; find the other mod at runtime. Then the mod builds in CI without that
mod's DLL, and it works (with the feature off) when the other mod isn't installed.

```csharp
public const string Guid = "randyknapp.mods.epicloot";
private delegate bool TryGetRarityFn(ItemDrop.ItemData item, ref int rarity);
private static TryGetRarityFn s_tryGetRarity;
public static bool Available { get; private set; }

public static void Init(ManualLogSource log)
{
    if (!Chainloader.PluginInfos.ContainsKey(Guid)) return;
    try
    {
        Type api = AccessTools.TypeByName("EpicLoot.API");
        MethodInfo method = AccessTools.Method(api, "TryGetRarity", new[] { typeof(ItemDrop.ItemData), typeof(int).MakeByRefType() });
        if (method == null) { log.LogWarning("Epic Loot API not found; rarity rules disabled"); return; }
        s_tryGetRarity = (TryGetRarityFn)Delegate.CreateDelegate(typeof(TryGetRarityFn), method);
        Available = true;
    }
    catch (Exception e) { log.LogWarning($"Epic Loot hookup failed; rarity rules disabled: {e.Message}"); }
}
```

- Prefer the other mod's official API (`[PublicAPI]`, a class named `API`). If you must use internals, put
  them behind their own `...Available` flag so only that feature switches off if they change, and say so in a comment.
- Delegates (`Delegate.CreateDelegate`) for hot paths; `MethodInfo.Invoke` is fine for occasional calls.
- `AccessTools.TypeByName` searches all loaded assemblies by full name.
- Add `[BepInDependency(guid, SoftDependency)]` so the other mod loads first when present.
- Decompile the other mod from the r2modman profile first (`decompile.py --plugin <name> --project`).

## Harmony: prefix, postfix, transpiler

- **Postfix** to add behaviour after a method (register commands after `Terminal.InitTerminal`, append tooltip text).
- **Prefix** to act just before (snapshot state before `Player.PlacePiece`). Return `void` unless you mean to skip the original.
- **Transpiler** to change one condition inside a long method without copying it. Count replacements and fail
  the patch if the count isn't what the game code had, so a game update turns the mod off with a clear message
  instead of applying half a change:

```csharp
[HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
internal static class AutoPickupPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo autoPickup = AccessTools.Field(typeof(ItemDrop), nameof(ItemDrop.m_autoPickup));
        MethodInfo replacement = AccessTools.Method(typeof(AutoPickupPatch), nameof(CanAutoPickup));
        int replaced = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.LoadsField(autoPickup))
            {
                replaced++;
                // the ItemDrop is already on the stack; the call consumes it and leaves a bool
                yield return new CodeInstruction(OpCodes.Call, replacement) { labels = instruction.labels, blocks = instruction.blocks };
            }
            else yield return instruction;
        }
        SafePatching.ExpectReplaced(replaced, 1, "Player.AutoPickup: m_autoPickup checks");
    }

    private static bool CanAutoPickup(ItemDrop drop) => drop.m_autoPickup && !Rules.IsFiltered(drop.m_itemData);
}
```

With the publicized assembly, `nameof(Player.AutoPickup)` works on private members.

## Failing safely (shared/)

`shared/*.cs` is compiled into every mod (namespace `ValheimMods.Shared`, internal types), so it needs no
dependency and mods released at different times can't conflict.

- **`SafePatching.PatchAll(harmony, Logger)`** instead of `harmony.PatchAll()`. Harmony's version stops at the
  first patch class that throws (a game method that moved in an update) and keeps the ones before it, so a mod
  could count materials in chests but not take them. This tries every class; if any fails, it removes all of the
  mod's patches, logs why and tells the player the mod is off. `Plugin.Awake` returns early when it returns false.
- **Patches for optional mods** (`harmony.Patch(...)` from `Awake`, only when the other mod is loaded) go after
  `SafePatching.PatchAll`, in their own try/catch with a warning: a changed optional mod switches off only that
  feature (LootFilter's `BuildCameraPickupPatch`).
- **`SafePatching.ExpectReplaced(replaced, expected, what)`** at the end of every transpiler.
- **`FrameGuard`** for `Update`/`OnGUI` on MonoBehaviours. Unity logs an exception and calls the method again next
  frame, so one bug can fill the log. Move the body into a `Tick()` and catch around it; after repeated errors
  the guard disables the component and tells the player. Undo the component's changes in `OnDisable` (it runs
  then), not only in `OnDestroy`.

```csharp
private FrameGuard _guard;
private void Awake() => _guard = new FrameGuard(this, "the stability overlay"); // the player's name for it
private void Update()
{
    try { Tick(); }
    catch (Exception e) { _guard.Report(e); }
}
private void OnDisable() => Hide();
```

- **`PlayerNotice.Show(message)`** shows a message once the player is in game (top left and the console), for
  problems that would otherwise be only in the log.

## Changing game objects reversibly

When hiding or recolouring things (particles, lights, other mods' components), record exactly what you
changed and restore exactly that. Toggling everything back "on" would turn on things that were off to begin with.

```csharp
private sealed class State
{
    public readonly List<Behaviour> DisabledBehaviours = new List<Behaviour>();
    public readonly List<ParticleSystem> StoppedParticles = new List<ParticleSystem>();
    public readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> RecoloredParticles = new ...;
}
```

- Re-apply on every scan: other mods may add effects after spawn (Epic Loot's beam appears in a later Update).
  Only touch things that are currently enabled/playing so re-applying is idempotent.
- Restore everything when a relevant setting changes, then re-apply with the new settings.
- Restore in `OnDestroy`.
- Recolour particles via `ParticleSystem.MainModule.startColor` and `Clear()` (already-emitted particles keep the old colour).

## Keeping per-frame work cheap

- Scan world lists (`WearNTear.GetAllInstances()`, `ItemDrop.s_instances`) on an interval (~0.25s), not every frame,
  unless something changed (a `Version` counter, a held key toggling).
- Spread expensive work (physics overlaps, analysis) over frames: a cursor and a per-frame budget, caching
  results with a timestamp.
- Prefer on-screen and nearby items, measured from the camera, and cap the count; tell the player when a cap
  hides things (a status line like "200 shown · 40 hidden by MaxPieces").

## Feedback to the player

- `Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, text)` after a hotkey action, saying what changed:
  `"Resin: filtered"`, `"All trophies: not filtered"`.
- When an action did nothing useful, say why instead of looking broken ("magic items use rarity rules",
  "Rarity rules need Epic Loot").
- Tooltip lines explaining *why* something happened (`Loot filter: filtered (rarity: Magic)`).
- Log one warning (not one per frame) when an optional feature can't work.
