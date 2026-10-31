# Valheim game API notes

Verified by decompiling **Valheim 1.0.17** (network version 40, Unity 6000.0.75) in October 2026.
Treat these as a map of where to look, not as truth: the game updates, so decompile the installed
version (`tools/decompile.py`) before relying on a signature. Private members are usable directly
because the shared `Directory.Build.props` publicizes `assembly_valheim`.

## Contents
- Which assembly a type lives in
- Console commands and chat
- Map pins
- Player
- Input and "is the player typing?"
- Camera and on-screen drawing
- Building pieces and structural support (WearNTear)
- Item drops, auto-pickup and tooltips
- Inventory UI hover
- World progression (boss keys) and biomes
- World layout
- Radial menu (1.0) and tools
- Inventory panels, search boxes and text input

## Which assembly a type lives in

`CS0012: The type 'X' is defined in an assembly that is not referenced` tells you which DLL to add.
Known ones outside `assembly_valheim`:

| Type | Assembly |
|---|---|
| `ZInput` | `assembly_utils` |
| `Localization` | `assembly_guiutils` |
| `GuiInputField` (e.g. `Terminal.m_input`) | `gui_framework` |
| `PlatformUserID` (e.g. `Minimap.AddPin` signature) | `Splatform` |
| `InventoryElement` | `assembly_valheim`, top-level (not nested in `InventoryGrid`) |

To find an unknown type: `ilspycmd -l c <dll>` on each candidate in `valheim_Data/Managed`.

## Console commands and chat

- Commands live in the static `Terminal.commands` dictionary; constructing a `Terminal.ConsoleCommand`
  registers it. Register in a **Harmony postfix on `Terminal.InitTerminal`** (private static), which runs once.
- Constructor (two overloads: `ConsoleEvent` returning void, `ConsoleEventFailable` returning object):
  `ConsoleCommand(string command, string description, ConsoleEvent action, bool isCheat = false, bool isNetwork = false,
  bool onlyServer = false, bool isSecret = false, bool allowInDevBuild = false, bool hideBehindDevCommands = false,
  ConsoleOptionsFetcher optionsFetcher = null, bool alwaysRefreshTabOptions = false, bool remoteCommand = false, bool onlyAdmin = false)`
- `ConsoleEventArgs`: `Args` (split on spaces, so quotes are NOT respected), `ArgsAll` (everything after the
  command), `FullLine`, `Context` (the Terminal; reply with `args.Context?.AddString(...)`).
  Parse `ArgsAll` yourself if you need quoted strings.
- `optionsFetcher` gives vanilla tab completion for the **first** parameter only.
- **Chat**: `Chat.InputText` strips a leading `/` and runs the rest as a command, refusing `isCheat` commands.
  So a non-cheat command works as `/name ...` in chat with no extra code. Replies go to the local chat window only.
- Rich parameter hints (like `spawn`'s) come from the Server Devcommands mod, not vanilla; see integrations.md.

## Map pins

- `Minimap.instance.AddPin(Vector3 pos, Minimap.PinType type, string name, bool save, bool isChecked,
  long ownerID = 0, PlatformUserID author = default)` (needs the `Splatform` reference). `save: true` persists it.
- World coordinates: **x and z** are the map plane, y is height. The `pos` console command prints X,Y,Z.
- `PinType`: Icon0..Icon4, Death, Bed, Shout, None, Boss, Player, RandomEvent, Ping, EventArea, Hildir1..3, Memorial.
  The five placeable icons, confirmed in game: Icon0 fire, Icon1 house, Icon2 hammer, Icon3 dot (default), Icon4 rune.
  Hildir1..3 render as question marks (Hildir quest pins). `None` has no sprite.
- `Minimap.instance.m_icons` (`List<SpriteData>` with `m_name` PinType and `m_icon` Sprite) shows which types have a sprite.

## Player

- `Player.m_localPlayer`; `GetCurrentBiome()`; `GetHoverObject()` (what the crosshair targets; use
  `GetComponentInParent<T>()` on it); `InPlaceMode()` (hammer/hoe/cultivator equipped);
  `Message(MessageHud.MessageType.TopLeft, text)` for short feedback.
- Building hooks: `PlacePiece(Piece, Vector3, Quaternion, bool doAttack, bool cheated)` (public),
  `RemovePiece()` (private, raycasts itself). Prefixes on both are a clean "just before the player builds/removes".
- `AutoPickup(float dt)` (private): loops over nearby `ItemDrop`s and skips one when `!drop.m_autoPickup`,
  plus other checks. The single `ldfld ItemDrop::m_autoPickup` is a good transpiler hook (see patterns.md).

## Input and "is the player typing?"

- The game uses **Unity's new Input System**. Read keys with `ZInput.GetKeyDown(KeyCode, false)` /
  `ZInput.GetKey(KeyCode, false)`. BepInEx's `KeyboardShortcut.IsDown()` uses the legacy API and isn't
  reliable here, so store `KeyboardShortcut` in config (Configuration Manager can edit it) but evaluate it with ZInput.
- Ignore hotkeys while typing or in menus: `Console.IsVisible()`, `TextInput.IsVisible()`, `Menu.IsVisible()`,
  `Minimap.InTextInput()`, `Chat.instance && Chat.instance.HasFocus()`. Add `InventoryGui.IsVisible()` unless the
  hotkey is meant to work in the inventory.
- `Console` here is the game's global `Console` class; inside a namespace with `using System;` it still resolves
  to the game's (namespace members beat using-imports), but `global::Console` is clearer if in doubt.
- Avoid defaults that collide with Windows (Ctrl+Alt+Delete) or vanilla binds. F7/F8/F9, Delete and modifiers
  of them were free as of 1.0.17.

## Camera and on-screen drawing

- Camera: `GameCamera.instance.m_camera` (private). Measure "nearby" from the **camera**, not the player:
  build-camera mods move the camera far from the character, and players use them.
- World-space labels: `OnGUI` + `camera.WorldToScreenPoint(pos)` (skip `z <= 0`), y flipped with `Screen.height`.
  Draw a dark copy offset by 1px first as a shadow. Only draw on `EventType.Repaint`. Worked in game.
- Lines: `OnRenderObject` + `GL.LINES` with a material from `Shader.Find("Hidden/Internal-Colored")`
  (`_ZTest` Always to draw through walls); check `Camera.current == camera`. Worked in game.
- Tinting a game object: `MaterialMan.instance.SetValue(go, Shader.PropertyToID("_EmissionColor"), color)` and
  `ResetValue(go, id)` to undo. Vanilla's own build-mode highlight resets it after 0.2s, so re-apply periodically.
- The IMGUI modules need `UnityEngine.IMGUIModule` and `UnityEngine.TextRenderingModule` references.

## Building pieces and structural support (WearNTear)

- Every piece: `WearNTear`. `WearNTear.GetAllInstances()` lists all loaded pieces.
- `GetSupport()` (private): owner reads `m_support`, others read the synced ZDO value (`ZDOVars.s_support`).
  `m_noSupportWear` false = piece doesn't use support. `m_materialType` (Wood, Stone, Iron, HardWood, Marble,
  Ashstone, Ancient, Ice, Timberwood). `GetMaterialProperties(out max, out min, out horizontalLoss, out verticalLoss)`.
- Material table (1.0.17): Wood 100/10, HardWood 140/10, Timberwood 200/10, Stone 1000/100, Ice 1000/100,
  Iron 1500/20, Marble 1500/100, Ashstone 2000/100, Ancient 5000/100 (max/min).
- `UpdateSupport()` (private): touching terrain or a non-piece collider → max. Otherwise the best offer over
  overlapping neighbours of `neighbourSupport × (1 − loss × distance)` (distance between centres of mass + 0.1,
  loss interpolated horizontal→vertical by angle), or the average of two supports below the COM that are ≥100°
  apart. Capped at the piece's own max. No load or "budget": supporting more pieces costs nothing.
- Vanilla highlight colour: `(support − min) / (max × 0.5 − min)`, so anything above ~half max looks fully green;
  light blue = at max (grounded).
- Each piece recalculates on its own timer, so changes ripple out over a few seconds.

## Item drops, auto-pickup and tooltips

- `ItemDrop.s_instances` (private static list) holds every drop in the world. `Awake` loads `m_itemData` from the
  ZDO immediately, so a drop's data (including other mods' custom data) is ready after spawn.
- `ItemDrop.m_autoPickup`: vanilla sets it false in `OnPlayerDrop()` for items the player drops, so it doubles as
  "the player dropped this". Don't overload it for your own logic; wrap the check instead.
- `ItemDrop.ItemData`: `m_shared.m_name` (localization token, e.g. `$item_resin`), `m_shared.m_itemType`,
  `m_dropPrefab.name` (internal prefab name like `RoundLog`, language independent; best key for config lists),
  `m_customData` (string dictionary other mods store data in).
- `ItemType` (1.0.17): None, Material, Consumable, OneHandedWeapon, Bow, Shield, Helmet, Chest, Ammo, Customization,
  Legs, Hands, Trophy, TwoHandedWeapon, Torch, Misc, Shoulder, Utility, Tool, Attach_Atgeir, Fish,
  TwoHandedWeaponLeft, AmmoNonEquipable, Trinket. Group these into friendlier categories for players.
- Tooltip text: static `ItemDrop.ItemData.GetTooltip(ItemData item, int qualityLevel, bool crafting, float worldLevel,
  int stackOverride = -1, bool appending = false)`. A postfix that appends to `__result` also runs when another mod's
  prefix replaces the tooltip (Epic Loot does).
- Display name: `Localization.instance.Localize(item.m_shared.m_name)`.
- Drops have particle systems and lights (sparkles/glow). Turning them off is the way to "hide" effects while keeping the model.

## Inventory UI hover

- `InventoryGui.IsVisible()`, `InventoryGui.instance.m_playerGrid`, `.m_containerGrid` (private; check
  `grid.isActiveAndEnabled`, the container grid is inactive when no chest is open).
- `InventoryGrid.GetHoveredElement()` (private, uses `ZInput.pointerPosition`) → `InventoryElement`;
  `GetElementPos(element)` (private) → `Vector2i`; `grid.GetInventory().GetItemAt(x, y)`.

## World progression (boss keys) and biomes

- Boss defeated keys are world global keys: `ZoneSystem.instance.GetGlobalKey("defeated_eikthyr")`;
  others: defeated_gdking (Elder), defeated_bonemass, defeated_dragon (Moder), defeated_goblinking (Yagluth),
  defeated_queen, defeated_fader, defeated_frozenking_p3. Synced to clients; shared by everyone on a server.
- `Heightmap.Biome` is a flags enum (Meadows=1, Swamp=2, Mountain=4, BlackForest=8, Plains=16, AshLands=32,
  DeepNorth=64, Ocean=256, Mistlands=512, plus combined All and Land values to skip when iterating).
  Progression order isn't the enum order; Epic Loot keeps one (integrations.md).
- Biome **names** for players: `Player.GetCurrentBiomeData()` returns a `BiomeSector`; `.GetName()` is the localized
  name the game shows on entering a biome, including 1.0's alt-biome modifiers. `BiomeSector.GetBiomeName(biome)`
  gives the `$biome_<name>` token (pass through `Localization.instance.Localize`).
  Right after spawn, `GetCurrentBiomeData()` is `BiomeSector.Empty` (Biome None) until the first update (~1s), and
  `WorldGenerator.GetBiomeSector` returns placeholder sectors (`EmptyMeadows`, `EmptyBlackForest`, `EmptyEdge`)
  before biome data is ready. Fall back to `WorldGenerator.instance.GetBiome(pos)` rather than printing a wrong name.

## World layout

- The world is centred on (0, 0): `WorldGenerator.worldSize = 10000` (radius), `waterEdge = 10500` (edge of the world).
  Vanilla `pos` (hidden behind devcommands) prints position and `Utils.DistanceXZ(Vector3.zero, pos)`.
- `WorldGenerator` is a plain C# class, not a `UnityEngine.Object`: null-check it with `== null`, not `!instance`.
  The `if (!x)` shorthand only compiles (and only means "destroyed or null") for UnityEngine objects.

## Radial menu (1.0) and tools (learned building EverythingsANail, Oct 2026)

- `Hud.instance.m_radialMenu` is a `Valheim.UI.RadialBase`; menus are `IRadialConfig`s whose
  `InitRadialConfig(radial)` builds elements and calls `radial.ConstructRadial(list)`. Elements are
  instantiated from `RadialData.SO` (`GroupElement`, `ItemElement`, `BackElement`, `MainGroupConfig`).
  A `GroupElement` with `Name`, `SubTitle`, `Icon.sprite`, `Interact`, `CloseOnInteract`, `Activated`
  (the "equipped" highlight) set by hand works as a generic entry; `ItemElement`'s highlight is reset
  every frame from `m_data.m_equipped`.
- `RadialBase.Open` only loads input settings, resets state and hooks death-close for configs that
  are `OpenRadialConfig`: derive from it and re-implement `IRadialConfig` (`public new void InitRadialConfig`).
  Input is a set of `Func<bool>` (`GetBack`, `GetOpenThrowMenu`, `GetThrow`, ...) on the radial, reinstalled
  by vanilla on each normal open; wrap them to change keys for your own menus. Element `TryOpenSubRadial`
  runs on `GetOpenThrowMenu` (only while `CanThrow`, set when the secondary button goes down).
- Opened from `Player.HandleRadialInput` (keyboard `OpenRadial` = G, `RadialSecondaryInteract` = right mouse).
  `RadialBase.InRadialBlockingPlaceMode` blocks opening in build mode (except repair).
- A tool's behaviour lives entirely in `ItemData.m_shared`: `m_buildPieces` (hammer/hoe/cultivator),
  `m_tamedOnly` (butcher knife), `m_attack.m_harvest` (scythe), `m_animationState` FishingRod/Feaster(serving tray)/Scythe,
  `m_toolTier`, `m_itemType` (decides the hand slot in `Humanoid.EquipItem`), `m_skillType`.
  Saves store only the prefab hash + per-item data, never shared data.
- `Player.IsKnownMaterial(m_shared.m_name)` = item has ever been in the character's inventory.
- Held model: `Humanoid.SetupVisEquipment` uses `m_dropPrefab.name`; the fishing line needs the rod model's `_RodTop`.
- Mods that identify tools by `GetRightItem().m_shared.m_name` (`$item_cultivator`, `$item_hoe`, `$item_hammer`,
  `$item_blueprintrune`): PlantEasily, PlantEverything, OCDheim, PlanBuild (plus held-model hash for its plan hammer).

## Inventory panels, search boxes and text input (learned building SearchEverything, Oct 2026)

- **UI layout without the game running**: the UI prefabs are in Unity bundles under
  `valheim_Data/StreamingAssets/SoftRef/Bundles/`; `SoftRef/manifest_extended` (text) maps asset paths to bundle
  names (`_GameMain.prefab` was bundle `d59cfac`, the main scene `17245031`). UnityPy (pip, in a scratch venv) reads
  GameObject trees with RectTransform anchors/positions/sizes; script class names don't resolve, but names and
  typetrees (`m_text`, `m_Sprite`) do. Took minutes and replaced guessing where to put a widget.
- Panels (canvas units, 1.0.17): `m_player` 570x287 at (40,-40) from the top-left; `m_container` 570x340, a child
  of the player panel, 30 below it; its `container_name` centred at the top (y -19) between Take All (top-left) and
  Stack All (top-right); grid from y -46. `m_crafting` 570x650 at (-40,-200) from the top-right; Craft/Upgrade tabs
  at y -69..-101 on the left, a free strip right of them (x ~250-560), station level box top-right (y -3..-67),
  `RecipeList` 196x545 at (10,-101), description panel on the right.
- The game has a search box: `Hud.instance.m_buildUi.m_searchField` (`GUIFramework.GuiInputField`, a
  `TMP_InputField`; build menu). Instantiate it for a vanilla-looking box; replace its UnityEvents, hide its
  `SearchBarHint` child (shows the build menu's focus key) and destroy `Localize` components. Placeholder default
  `$chat_entertext`, 20 pt.
- Recipe list: `InventoryGui.UpdateCraftingPanel()` (private) refetches recipes and calls
  `UpdateRecipeList(List<Recipe> recipes)`; removing entries from that list in a prefix filters both tabs.
- `InventoryGrid.UpdateGui` (private) redraws every slot every frame (icon colour reset to white); change slot looks
  in a postfix, e.g. a `CanvasGroup` alpha on the `InventoryElement`. `GetElement(x, y, width)` returns null when out
  of range.
- While text is typed, the game's checks are `TextInput.IsVisible()` (Player/PlayerController `TakeInput`, camera
  cursor capture, the pause menu's Escape, chat's Enter, map keys) and `Hud.instance.m_buildUi.SearchFieldFocused`.
  `InventoryGui.Update` checks neither: it closes on `Inventory`/`Use`/`JoyButtonB`/`JoyButtonY` buttons
  (`ZInput.ResetButtonStatus(name)` swallows them) and on raw `KeyCode.Escape` (skip `Hide()` that frame).
  Escape both unfocuses a TMP field and is read by those Updates in unknown order, so treat "focused this or last
  frame" as typing. `TextInput.IsVisible` is tiny and could be inlined, so SearchEverything also guards the big
  callers directly (`InputGuard.cs`).
- Vanilla keyboard defaults (ZInput): W A S D, Space, LeftCtrl crouch, LeftShift run, E use, Q autorun, R hide,
  F guardian power, G radial, T emote, X sit, C walk, V autopickup, M map, Tab inventory, 1-8 hotbar, F5 console.
  (from the `AddButton("...", KeyToPath(Key.X))` defaults in `ZInput`; K has no default binding).
- Debug mode (`debugmode` with cheats on) reads raw `GetKeyDown` for Z (fly), B (no build cost), K (kill nearby
  enemies) and L (remove drops) in `Player.Update`, inside `if (TakeInput())`, ignoring modifiers: Ctrl+K is still K
  there. A mod hotkey on one of those letters should make `Player.TakeInput` false on the frame it fires.
- Camera zoom (`GameCamera.UpdateCamera`) reads `ZInput.GetMouseScrollWheel()` unless the inventory, map, chat, menu
  or build menu is open; it doesn't check `TextInput`. SearchEverything transpiles those calls while its window is open.

