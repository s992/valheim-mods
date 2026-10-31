# Chest ownership in Valheim, and how CraftFromStorage works with it

This is the technical background for CraftFromStorage's design: how Valheim decides who may change a chest, what goes
wrong when that's ignored, what MultiUserChest (MUC) adds, and the rules this mod follows on top. It was verified by
decompiling Valheim 1.0.17 (`assembly_valheim`) and MultiUserChest 0.6.2. Class and method names below are the game's
own, so they can be looked up after a game update.

## Contents
1. ZDOs and owners
2. How conflicting writes are resolved
3. How ownership is assigned and moves
4. How much of the world is loaded
5. Vanilla chests
6. What goes wrong when a non-owner edits a chest
7. MultiUserChest
8. CraftFromStorage's rules
9. Smelters
10. Summary of limits

---

## 1. ZDOs and owners

Every networked object (chests, smelters, pieces, creatures, dropped items) is backed by a **ZDO**, a bag of
key/value data with an ID, a position and an **owner**. A `ZNetView` component connects a game object to its ZDO.

- **The owner is one peer (player or server) by session ID**, or 0 for nobody (`ZDO.GetOwner()`,
  `ZNetView.IsOwner()`).
- **The owner runs the object.** Its machine simulates creatures it owns and processes smelters it owns
  (`Smelter.UpdateSmelter` returns early on non-owners). It also writes the object's state.
- **Everyone else holds a copy** that the server forwards whenever the ZDO changes.
- **A chest's contents** are one byte array in its ZDO (`ZDOVars.s_items`), the serialized `Inventory`.
- **A smelter's state lives in its ZDO too:** queue size (`s_queued`), queued items (`item0`, `item1`, ...) and
  fuel (`s_fuel`).

## 2. How conflicting writes are resolved

Each ZDO carries two counters: `OwnerRevision` (bumped when the owner changes) and `DataRevision` (bumped by every
field write that changes a value: position, contents, flags, anything).

When a peer receives a ZDO update (`ZDOMan.RPC_ZDOData`), it keeps whichever copy has the **higher `DataRevision`**,
and **ignores ties**. Writes by any peer propagate, owner or not (`ZDOMan.ShouldSend` doesn't look at ownership).

This is a "last writer with the higher number wins" scheme, with no merging. Two machines changing the same ZDO at
the same time produce two candidate versions, and one of them silently disappears. For a chest, that means items
vanish or duplicate. The only thing preventing this in practice is the convention that **only the owner writes**.

## 3. How ownership is assigned and moves

- **The server reassigns ownership** every couple of seconds in `ZDOMan.ReleaseNearbyZDOS`. For each peer, it
  looks at persistent ZDOs near that peer's position:
  - a ZDO the peer owns that's now **outside** its active area gets owner 0;
  - a ZDO with **no owner**, or whose owner isn't near it, goes to this peer if it's **inside** this peer's active
    area.
- **The active area for ownership** (`ZNetScene.InActiveArea`) is 1.5 zones (96 m) either way from the centre of the
  peer's 64 m zone, a square.
  - Since a player can stand anywhere in their zone, **only objects within 64 m of a player are guaranteed to have
    an owner** (96 − 32).
  - Further out, a loaded chest can have **owner 0**.
- **Opening a chest also moves ownership** (section 5).
- **Stale owners.** Release only checks around each peer's *current* position. A player who teleports or portals
  away can stay the owner of chests they no longer have loaded, until another player's area covers those chests.
- **The dedicated server only instantiates objects near world position 0,0,0.** So the server is a normal owner
  only around spawn. Elsewhere it just relays.

## 4. How much of the world is loaded

Objects are instantiated (`ZNetScene.CreateObjects`) for the zones `ZDOMan.FindSectorObjects` returns around the
player.

- **Simulation distance** controls this: the graphics setting, using the **lower of the client's and the server's**
  (`ZNet.GetSyncedSimulationDistance`).
- **Its "near" value N means zones up to N rings out are loaded.**
- **"Classic" levels load the full square.** The others skip zones whose centre is (N + 0.5) zones or more away,
  i.e. the corners.

The radius at which a chest is **guaranteed** loaded, wherever the player stands in their zone, is therefore the
distance to the nearest unloaded zone:

| Setting level | Near / classic | Guaranteed loaded |
|---|---|---|
| 0 (lowest) | 1 / classic | 64 m |
| 1 | 2 / no | 91 m |
| 2 (default) | 2 / classic | 128 m |
| 3 | 3 / no | 143 m |
| 4 | 4 / no | 231 m |
| 5 (highest) | 5 / no | 272 m |

CraftFromStorage caps its range at this radius (`StorageNetwork.GuaranteedLoadedRadius`). Ownership (section 3)
doesn't scale with this setting. In multiplayer, only chests within ~64 m are guaranteed to have an owner, whatever
the setting.

## 5. Vanilla chests

Relevant parts of `Container`:

- **Opening.** `Interact` sends `RPC_RequestOpen` to the owner. If the chest isn't in use, the owner calls
  `ForceSendZDO` to the requester, then `SetOwner(requester)`, then replies "granted", and the requester opens the
  UI. If it is in use, the reply is "in use". **Opening = becoming the owner, exclusively.**
- **Saving.** `Inventory.m_onChanged` → `OnContainerChanged` → `Save()`, but only if `IsOwner()`. A non-owner's
  edits to its local copy are never written to the ZDO.
- **Loading.** `CheckForChanges` runs every second and calls `Load()` when the ZDO's `DataRevision` differs from
  the last one seen (`m_lastRevision`). It skips loading while `m_inUse`, which only the owner sets when it opens the
  chest. `Inventory.Load` throws away every item object and creates new ones.
- **The UI.** `InventoryGui.UpdateContainer` only shows the container grid while the local client owns the chest.
- **In use.** The owner mirrors `m_inUse` into the ZDO (`s_inUse`), which drives the lid animation and lets other
  code see that someone has the chest open.

Two quirks matter for mods:

- **The "granted" reply can overtake the data.** It's a routed RPC, while the force-sent ZDO travels separately.
  The new owner must not edit until its local ZDO shows it as owner (owner and contents arrive in the same update)
  and it has reloaded.
- **`Inventory.AddItem` only fires `Changed` when a local player exists.** On a dedicated server, edits don't save
  unless `Save` is called explicitly.

## 6. What goes wrong when a non-owner edits a chest

Changing `container.GetInventory()` on a chest you don't own changes only your local copy.

- **It's never saved** (section 5).
- **It's wiped by the next reload**, when the owner's version arrives.
- **Any side effects are real:** if your inventory gained items, you keep them.

Depending on order, the result is free items, items that vanish, or players seeing different contents. This is how
CraftFromChests behaves. It removes materials with `RemoveItem(name, amount)` on whatever chests are nearby, and
that's the multiplayer "jank" it causes.

Writing the ZDO directly as a non-owner is worse. It races the owner's own saves under the revision rule in
section 2.

## 7. MultiUserChest

MUC keeps vanilla's rule (only the owner writes) and turns everyone else's edits into **requests to the owner**.

- **Opening never blocks.** If someone has the chest open, others are let in without moving ownership. They see
  the owner's copy, which reloads as usual.
- **Edits become requests.** MUC intercepts specific `Inventory` methods (`AddItem`, `RemoveItem(item)`, moves) when
  the item or target belongs to a chest the local client doesn't own. It sends a request (add, remove, move,
  consume, drop) to the owner as a *global* routed RPC. The owner applies it to the real inventory, saves, and
  replies. Your side updates when the reply arrives.
- **QSS has a dedicated MUC path.** Quick Stack Store Sort Trash Restock checks whether MUC is loaded:
  - with MUC, it edits non-owned chests' local inventories directly, and MUC turns those edits into requests;
  - without MUC, it grabs ownership of each chest instead.

Details that matter for this mod (from MUC's source):

- **A remove request names a chest slot, not an item.** If the chest was re-sorted meanwhile, you get whatever is in
  that slot now. Nothing is lost, but it may not be what you asked for.
- **Replies land in the target slot.** If they can't, the item tops up a matching stack and the rest drops at your
  feet.
- **Stale owners still answer.** Because the RPC is global, a stale owner's handler still runs and replies
  "failed". Only an owner that has **disconnected** never replies.
- **There's no timeout.** While a request is pending, MUC blocks its target slot in your inventory, and **refuses
  all auto-pickup** while any slot is blocked (`PickupPatch`). A reply that never comes keeps that slot blocked for
  the session; nothing in MUC releases it.
- **Requests are never applied twice by mistake.** The owner checks `IsOwner()` when handling. A request that
  arrives after ownership moved is answered "failed", with nothing moved.
- **One failed patch disables everything.** MUC applies all its patches in one `PatchAll`. If one throws (issue #19:
  another mod transpiled `InventoryGui.UpdateContainer` first), the rest never apply and chests are silently
  single-user again.

## 8. CraftFromStorage's rules

**This mod never writes to a chest it doesn't own.** Everything follows from that.

**Which chests count** (`StorageNetwork`):
- placed by a player;
- you pass the chest's access check and any ward;
- within range (capped as in section 4);
- not a tombstone, a container on a player, or an excluded prefab;
- not a cart or ship unless that setting is on;
- not flagged with MUC's `MUC_Ignore`;
- in multiplayer, **has an owner**: owner-0 chests have nobody to apply changes, so they're skipped and counted in
  the status line. In single player nobody else can write, so ownerless chests are claimed and used.

**Is MUC healthy** (`MucHealth`)? At startup, after every plugin has loaded, the mod checks that MUC is installed, at
least 0.6.2, and that its patches on `Inventory.AddItem` (both overloads), `Inventory.RemoveItem(item)`,
`Container.RPC_RequestOpen` and `InventoryGui.UpdateContainer` are actually applied. If any check fails, chests other
players own are left out entirely.

**Three ways to take items** (`ChestAccess`):

1. **Owned chests: edit directly.** Before every edit, if the ZDO's `DataRevision` differs from what the chest last
   loaded, reload first. That way a stale copy is never saved over the real contents. Items are removed nearest chest
   first, after the player's inventory. Removal uses the same `RemoveItem(name, ...)` call vanilla uses, so other
   mods' hooks on it still run.
2. **Borrowing: become the owner first.**
   - The mod sends its own RPC to the chest's owner. The owner hands the chest over exactly like vanilla's open
     handshake (`ForceSendZDO`, then `SetOwner`, then reply), minus the UI.
   - It refuses if the chest is open, is a cart being pulled, or was borrowed by that owner in the last 30 s. That
     last rule stops two players at one base passing chests back and forth.
   - The requester waits until its own ZDO copy shows it as owner, reloads, and only then uses the chest.
   - Borrowing happens ahead of need: when a recipe or build piece is selected and the inventory plus owned chests
     fall short, or when a smelter needs material.
   - It's rate-limited (3 in flight, one every 0.3 s), because each hand-over resends the whole chest to everyone
     nearby.
3. **Pulling: MUC fetches items into the inventory.**
   - The mod calls `playerInventory.MoveItemToThis(chestInventory, item, amount, x, y)`, the same call QSS's restock
     uses, so MUC turns it into a remove request.
   - The target slot is always empty, or a partial stack of the same item with room, never a slot MUC would treat
     as a swap. Space for the whole plan is reserved up front, so nothing half-arrives.
   - If a pull gets no reply in 5 s, the mod releases MUC's blocked slot (via reflection) so auto-pickup works again.
     A late reply still lands, or drops at the player's feet.
   - Either way, a pull that brought nothing (no reply, or a stale owner's "failed") leaves that chest out of counts
     for 30 s, so the UI stops promising its items. The status line counts these as "not answering". A borrow request
     that gets no reply does the same.

**Crafting** (`CraftPatches`):
- The requirement checks (`Player.HaveRequirementItems`, `HaveRequirements(Piece)`, `GetFirstRequiredItem`) count
  inventory + storage, and the requirement display shows `have/need`.
- When craft is pressed, anything only available in chests other players own is pulled, during the craft timer.
- When the timer ends, the craft waits briefly for those pulls. It then goes ahead only if everything can be paid
  from the inventory plus owned chests. Otherwise it stops with a message rather than being free.
- Consumption replaces the single `RemoveItem` call inside `Player.ConsumeResources`: the inventory first (unchanged
  vanilla call), then owned chests.

**Building:** placing is instant, so if materials are only in chests other players own, the first click starts a
pull and doesn't place. The next click does.

## 9. Smelters

A smelter's queue and fuel live in **its** ZDO, written by **its** owner, which in multiplayer is often another
player.

- **Inserting:** `Smelter.OnAddOre`/`OnAddFuel` remove one item locally, then send `RPC_AddOre`/`RPC_AddFuel` to the
  owner. The owner queues it **without re-checking the limit**.
- **Your copy of the count lags:** it only updates when the owner's next ZDO update arrives.
- **Holding E repeats faster than that round trip,** so each repeat saw room that had already been used, and the
  smelter overfilled.

`SmelterInFlight` counts this client's inserts that the smelter's count doesn't show yet:
- each one is cleared when the count rises, or after 3 s as a backstop;
- the full checks and the hover counts use observed + in flight;
- this only applies to smelters someone else owns, because an owner's own RPC is handled on the spot.

Two players filling one smelter at the same moment can still go slightly over, as in vanilla.

## 10. Summary of limits

| Limit | Why |
|---|---|
| Multiplayer needs MUC (and its patches working) to use chests other players own | Only the owner may write; MUC is what turns other players' edits into requests |
| Chests beyond ~64 m may be skipped in multiplayer ("no owner") | Ownership is only guaranteed within 64 m; ownerless chests have nobody to apply changes |
| Range capped at the guaranteed loaded radius (128 m on default settings) | Chests outside the loaded area don't exist on the client |
| Fetching needs free inventory space | Pulls go through the player's inventory |
| Brief waits / a second click when building | Requests and hand-overs take a round trip to another player |
| Chests someone has open aren't borrowed | Vanilla's rule: an open chest stays with whoever opened it |
| A chest whose owner can't deliver (portalled away, disconnected) is "not answering" for 30 s | The owner's game answers "failed" or not at all, and nobody else may change the chest until the server reassigns it |
