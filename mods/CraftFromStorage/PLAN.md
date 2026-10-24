# Valheim storage mod: plan (draft 3.1, built on MultiUserChest)

Working name: **Storage** (GUID `s992.Storage`, repo `~/projects/ValheimStorage`). Nothing is built yet.

**Draft 3.1** adds the decisions from 2026-10-21 (section 10), *borrowing* chest ownership (4.1), and
corrections about range, pulls and stale owners. The free test in 9.0 is done: removing CraftFromChests and
Digi's storage module removed the jank, so the diagnosis in section 1 holds.

**Draft 3 replaces draft 2, including draft 2's list of open decisions.** Draft 2 designed our own
multi-user chest layer. Draft 3 drops that layer and depends on MultiUserChest (MUC) for multiplayer.
Section 1 explains why. The draft-2 design is kept in Appendix A as plan B.

The mod replaces CraftFromChests and Digi's Epic Loot storage-materials module, works with Quick Stack Store
Sort Trash Restock (QSS) and Epic Loot, and requires MUC in multiplayer. Researched against the installed
builds: Valheim 1.0.17, MUC 0.6.2, QSS 1.4.16, Epic Loot 0.14.13, CraftFromChests 0.4.0, Digi Epic Loot
Add-ons 1.0.4.

---

## 1. Decision record: why we build on MUC

**The jank most likely comes from the craft-from-chests mods.**
- CraftFromChests removes materials from chests it doesn't own with `RemoveItem(name, amount)`.
  - MUC intercepts adding an item and removing a *specific* item, but not this remove-by-name overload.
  - A non-owner's edit is never saved, and the next resync overwrites it.
  - The results are free crafts and players who see different contents.
- Digi's module avoids the problem by only using chests the local client owns that nobody has open, so it
  barely works in multiplayer.
- First inferred from code, then confirmed in game by the 9.0 test.

**MUC's core model is the right one.**
- MUC lets only the chest's owner write. Everyone else sends requests, and the owner's ZDO syncs back.
- Vanilla handles every other shared object the same way: `RPC_AddOre`, `RPC_AddItem`, `RPC_AddAmmo`, fuel
  and item stands.
- Given how ZDO conflicts resolve (the higher revision wins, and the other write is silently lost), it is
  the only safe model.

**Valheim 1.0.17 has nothing newer.**
- Opening a chest still transfers ownership and is exclusive.
- There is no API for moving items between players and chests.

**MUC is battle-tested with QSS.**
- It has about 795k downloads and dates from 2021.
- QSS has a dedicated MUC code path, so QSS needs no patches from us.

**MUC's weaknesses:**
- **How it detects changes is brittle.** It intercepts specific methods and tracks which chest each item
  came from. Its changelog is a list of duplication fixes at those seams.
- **One failed patch disables everything.** Open issue #19 (Oct 2026): a patch that assumes a fixed IL
  layout throws when another mod has already transpiled `InventoryGui.UpdateContainer`. The exception
  aborts all of MUC's patches, and chests silently become single-user.
- **Maintenance is thin.** The last feature work was in 2024, and the Valheim 1.0 breakage was fixed by
  a community PR.
- **Edge cases we inherit:** MUC ignores chests that have no owner, and stale owners (a player who
  portalled away) are a weak spot.

**What this means for our mod:**
- **Craft-from-storage is the part worth owning.** The existing mods are churning: AzuCraftyBoxes, the
  dominant one, was recently deprecated.
- **Multi-user chests are the part not to rebuild.** Rebuilding is the riskiest code ("don't delete
  the warehouse") and buys little while MUC works.

**Rules that follow:**
- **Never write to a chest we don't own.**
  - Chests we own: edit them directly. That is safe and instant.
  - Chests we don't own: **pull** items into the player's inventory, using only the call shape that QSS
    restock uses, which MUC demonstrably syncs.
- **A pull is as safe as QSS restock with MUC.**
  - Normally the item either arrives or stays in the chest.
  - The remaining risk is inside MUC: the owner removes the item and its reply is lost (for example the
    owner disconnects). MUC's changelog shows past fixes in this path.
  - Our own code never deletes items.
- **Borrowing ownership** (4.1) lets most actions work on chests we own. It moves ownership with the same
  handshake vanilla runs every time someone opens a chest, and never while anyone has the chest open.
- **Keep the swap option open.** Every chest access goes through one narrow interface, so MUC could later
  be replaced by our own backend (Appendix A) without rewriting the features.
  - The trigger for doing that: a game update breaks MUC and no fix is coming.
  - Before building plan B, consider sending MUC a fix, as the community did for 1.0.

---

## 2. Relevant facts from research (still true in draft 3)

### Vanilla
- **Chest contents.** Contents live in the container's ZDO (`s_items`). Only the owner saves. Other
  clients re-read about once a second, and MUC keeps those copies current.
- **Loaded area** (default classic simulation distance):
  - Containers are guaranteed to be loaded within 128 m of the player, or 64 m on the lowest setting.
  - Ownership is only assigned within about 96 m of a peer's zone centre, so chests between 64 and 192 m
    away may have **no owner**.
  - MUC ignores ownerless chests, and so do we in multiplayer (decision 0).
  - Only chests within **64 m** of you are guaranteed to have an owner. Alone at a base, chests 64–128 m
    away can be ownerless and get skipped. This is shown in the status line.
  - In single player (or a listen host with no peers) nobody else can write, so we claim ownerless chests
    and take from them directly. That gives the full 128 m.
- **Requirement counting.**
  - Requirements are counted with `m_inventory.CountItems`/`HaveItem` in `Player.HaveRequirementItems`,
    `Player.HaveRequirements(Piece)`, `Player.GetFirstRequiredItem` and `InventoryGui.SetupRequirement`.
  - `SetupRequirement` sums all qualities, while `HaveRequirementItems` uses the best single quality.
  - The requirement display shows only the needed amount.
- **Crafting** has a timer (`OnCraftPressed` → `DoCrafting`), which re-checks requirements and then calls
  `ConsumeResources`. Single-ingredient recipes call `RemoveItem(name, n, quality)` instead, which Epic Loot
  hooks to move magic effects.
- **Building** is instant, and it calls `ConsumeResources` even under `nocost`.
- **Smelters.**
  - `Smelter` covers the smelter, blast furnace, kiln, windmill, spinning wheel and eitr refinery. The
    windmill and spinning wheel have no fuel.
  - `OnAddOre(item == null)` picks the first convertible inventory item, removes one, and sends
    `RPC_AddOre` to the owner.
  - `Switch.Interact` receives the alt (Shift) flag and ignores it.
  - Vanilla 1.0's radial picker (`TryGetItems`/`CanUseItems`) only looks at the player's inventory.

### MUC
- **Withdrawing from a chest you don't own.**
  - When something adds an item that belongs to a non-owned chest into the player's inventory (for
    example QSS restock's `playerInventory.MoveItemToThis(container, item, amount, x, y)`), MUC sends a
    remove request to the owner.
  - The item arrives in the player's inventory when the response comes back.
- **Chests you own** are unaffected; their edits save normally.
- **Exact behaviour to confirm (phase 0 spike):**
  - partial amounts;
  - what happens on failure;
  - where the item lands;
  - how long a request waits;
  - behaviour with stale owners.

### Epic Loot
- **Provider API.** `EpicLoot.API.RegisterInventoryProvider(id, getItems, count, remove, removeExact)`
  - The returns are synchronous: `remove` must remove immediately.
  - The enchanting table, merchant purchases and treasure maps (which spend coins) all count and remove
    through it.
  - Providers are summed together.
- **Digi's module** registers the provider `pieter.nearbychests`. Running ours alongside it double-counts.
- **Things our code must compose with:**
  - Lucky Craft, a `ref` prefix on `ConsumeResources` that rewrites `requirements`;
  - `Inventory.Load` postfixes that initialise magic data.
- **Direct inventory counts.** `RequirementElement` and `TemperPanel` count the player inventory directly.
- **Magic gear.** Items in chests carry magic data in `m_customData`, so taking "by name" could consume
  enchanted items.

---

## 3. Design overview

```
 ┌──────────────── Features ─────────────────┐
 │ craft/build │ smelt │ Epic Loot provider  │   show counts, consume
 ├──────────── Storage network ──────────────┤
 │ eligible chests, count table, planner     │
 ├──────────── Chest access (one interface) ─┤
 │ owned: edit directly                      │
 │ not owned: pull via MUC (async)           │   ← swappable backend (Appendix A)
 └───────────────────────────────────────────┘
```

## 4. Chest access layer

The only code that touches chest inventories. Four operations:
- `Count(chest)`: read the local copy.
- `TakeNow(chest, name, n)`: owned chests only, synchronous. Uses `RemoveItem(string…)`, the overload
  Epic Loot hooks.
- `Borrow(chests) → handle`: ask each chest's owner for ownership (4.1). It completes when the requests
  are granted or refused.
- `Pull(chest, name, n) → handle`: non-owned chests that couldn't be borrowed. Uses QSS restock's call
  shape and completes when the items have arrived in the player's inventory, or after a timeout (left in
  the chest, reported).
  - Pulls are batched per chest.
  - They only target an empty slot or a partial stack of the same item. A slot holding a different item
    would make MUC swap the two.
  - When the inventory has no room, the action fails with a clear message ("need 2 free slots").
    See 4.2.

**Findings from the phase 0 spike** (details in [docs/muc-spike.md](docs/muc-spike.md)):
- **Pulls identify items by chest slot only,** so a re-sorted chest can deliver a different item. Nothing is
  lost. The planner checks what actually arrived, and aborts with a message if it isn't what was needed.
- **Pull timeout releases MUC's blocked slot.** MUC has no timeout, and a slot blocked by a request that
  never gets a reply disables auto-pickup for the whole session. After 5 s we release the slot by
  reflection (behind its own availability flag).
- **`TakeNow` requires a current local copy** (`m_lastRevision == DataRevision`), reloading first if needed.
  After a borrow is granted, wait until the local ZDO shows us as owner, then reload, then edit.
- **Stale owners reply "failed" quickly.** MUC's RPCs reach the owner's peer even when it doesn't have the
  chest loaded. Only a disconnected owner gives no reply.

**Unresponsive chests.** A chest whose borrow or pull timed out (typically a stale owner, i.e. a player
who portalled away) is left out of counts for 30 s. Otherwise the UI would promise "105/10" and then
report "materials didn't arrive".

### 4.1 Borrowing ownership
- **The handshake.** Our own RPC to the chest's owner, handled like vanilla's open request minus the UI:
  - if the chest isn't in use (vanilla's and MUC's notion of in use, including a cart being pulled),
    the owner calls `ForceSendZDO`, then `SetOwner(requester)`, then replies "granted";
  - otherwise it replies "refused".
- **This is the same transfer vanilla performs on every chest open.** It doesn't add a new write path:
  after the transfer, edits are ordinary owner edits.
- **When we borrow.** Eligible nearby chests that we don't own are borrowed in one batch when the player:
  - opens a crafting station;
  - enters build mode with a piece selected;
  - opens the Epic Loot enchanting table;
  - hovers a smelter.

  After that they're refreshed every few seconds while the UI stays open. Ownership then simply stays
  with the player until the game moves it.
- **Chests someone has open** can't be borrowed. Each feature handles them its own way (sections 6–8).
  The status line counts them, e.g. `2 in use`.
- **To confirm in the phase 0 spike.** What MUC does with requests already in flight to the old owner
  when ownership moves. MUC's `FallbackResponse` suggests it bounces them back to the requester; verify
  that in its source and in game.

**MUC health check at startup.** Check that MUC is loaded at version ≥ 0.6.2 and that its Harmony patches
on `Inventory.AddItem` and `InventoryGui.UpdateContainer` are actually present. That second check catches
issue #19.
- If MUC is missing or unhealthy, non-owned chests are skipped entirely: owned-only mode.
- This is shown on screen once and in the status line.
- Single player is always owned-only, which is correct there.

**Chests we never touch:**
- ownerless chests, in multiplayer;
- chests whose ZDO has MUC's `MUC_Ignore` flag;
- tombstones;
- containers on players;
- Adventure Backpacks proxies (they have no ZDO);
- carts and ships, unless enabled;
- prefabs on the exclusion list.

### 4.2 Known limitation: pulls need free inventory space
Pulling is the only path that moves items through the player's inventory, so a full inventory blocks it.
Borrowed and owned chests use `TakeNow` and **need no space at all**. The limitation therefore only bites
when the materials sit in a chest someone else has open, or one that couldn't be borrowed.

**Where it applies:**

| Action | Affected? |
|---|---|
| Crafting | Only when the pulled share needs slots. The crafted result already needs a slot in vanilla. |
| Building | Only the click-again fallback. |
| Smelting | Only when inserting from a chest that couldn't be borrowed. The batch shrinks to the free space, minimum 1. |
| Epic Loot | Never: it uses owned chests only. |

**How we soften it:**
- Pulls stack onto existing partial stacks of the same item first, so they often need no new slot.
- Pull only the shortfall, never more.
- In crafting, the materials are consumed seconds later, so the slots are only held during the craft timer.

**How it's shown (prominently, never silently):**
- A centre-screen message names the problem and the fix: `Storage: need 2 free inventory slots to fetch
  materials from a chest someone has open`.
- The requirement panel marks those materials, e.g. `105/10 ⚠`, with a tooltip explaining the same thing.
- One log line per occurrence, for debugging.
- Nothing is ever pulled partially into a full inventory and left stranded. The plan checks space up front
  for the whole pull.

## 5. Storage network

- **Container registry.** Containers are tracked from `Awake` to `OnDestroy` and filtered every 0.5 s by
  distance from the **player**, since loading and ownership centre on the player.
- **Eligibility.** A container counts as storage if it:
  - was placed by a player;
  - passes `CheckAccess` and the ward check;
  - has an owner (in multiplayer);
  - is owned locally, or is not owned locally and MUC is healthy;
  - isn't currently marked unresponsive;
  - isn't a cart or ship (decision 3).

  Eligible chests are used nearest first.
- **Count table.** One combined (name, quality) → count table, updated when a chest's local copy changes.
  Lookups match vanilla's semantics per call site.
- **Items we never count or take:**
  - items with Epic Loot magic effects (Epic Loot crafting materials are allowed);
  - coins (configurable).
- **Range.** Configurable 5–250 m, default 30.
  - The effective range is capped at the guaranteed loaded radius.
  - A status line shows it, for example `Storage: 38 chests · 6 skipped (no owner) · 128 m cap`.
- **Hotkey.** One hotkey (default `Home`, configurable) toggles "use nearby storage", with a top-left
  message. Each feature can also be switched off in the config.
- **Cheats.** Storage is skipped under `nocost`, `NoCostCheat` and the no-cost global keys.

## 6. Craft and build from storage

- **Counts and display.**
  - Transpile the requirement counts in the four methods from section 2 to "inventory + table".
  - A `SetupRequirement` postfix shows `have/need` (105/10) and drives the red flash from the same count.
  - This covers the crafting panel, upgrades and the build menu.
- **Crafting** (it has a timer, so pulls fit naturally):
  1. On craft press, plan the shortfall after inventory. Owned chests come first (most nearby chests are
     owned by then, because opening the station borrowed them), then nearest non-owned chests. Start the
     pulls.
  2. At `DoCrafting`, vanilla consumes from the inventory. Our transpiled `RemoveItem` inside
     `ConsumeResources` takes whatever is still missing from **owned** chests with `TakeNow`. This runs
     after Lucky Craft and works for ×5 multi-craft.
  3. If pulled items haven't arrived by `DoCrafting`, extend the timer up to N seconds, then abort with
     "materials didn't arrive".
  4. Pulled items left over (cancelled craft, failed race) stay in the player's inventory, with a message.
     Nothing is ever lost.
- **Single-ingredient recipes:** `GetFirstRequiredItem` can return a storage item, and the same
  pull/take logic applies.
- **Building** (instant):
  - Nearby chests are borrowed when you select a piece, so placements normally take from owned chests
    synchronously.
  - If the materials are only in a chest someone has open, the first click starts a pull and says so
    ("fetching 4 wood…"). The next click places the piece.

## 7. Smelt from storage

- **Hover:** `Next: Flametal ore — 2 in inventory, 340 in storage · [Shift+E] change (2/4)`.
  The fuel hover shows the storage count.
- **Selection** (stored per player, locally; decision 5).
  - Patch `Switch.Interact` (non-hold, alt held) to cycle through accepted, available, non-blocklisted
    materials. Insertion is suppressed while Shift is held.
  - The selection sticks, and the hover says "none available" when the selected material runs out.
  - Vanilla's radial picker is extended to list storage materials.
- **Insertion.**
  - The selected material comes from the inventory first.
  - Otherwise use `TakeNow` from an owned chest and send vanilla's `RPC_AddOre`.
  - Otherwise pull a small batch into the inventory, then insert from the inventory. The batch is capped
    by the smelter's free capacity and by free inventory space (4.2). Holding E keeps working.
- **Fuel** is handled the same way, only where the smelter has a fuel switch.
- **Blocklist.**
  - Prefab names in the config. Default: `FineWood, RoundLog, YggdrasilWood`, so the kiln doesn't burn
    good wood.
  - A hotkey toggles the hovered smelter's selected material on the blocklist.
  - Blocklisted materials are never auto-inserted. Using an item from the hotbar stays vanilla.

## 8. Epic Loot

**Dropped on 2026-10-23.** Epic Loot 0.15.0 gave the enchanting table its own material storage, and
tempering now costs a bounty token instead of materials, so the provider, the Temper counts, the
Sacrifice-tab checkbox and the Digi handling were removed. The mod still asks Epic Loot's API which items
are enchanted, so crafting never uses up enchanted gear. That's the only remaining Epic Loot dependency,
and it's optional.

## 9. Safety, testing and phases

**Safety.**
- **We never write to a chest we don't own,** and a pull can't delete items (section 1).
- **Builds never deploy to the Default profile.** `PluginsDir` points at a separate r2modman profile
  (e.g. `StorageTest`), and deployment only happens if that folder exists.
- **Diagnostics:** an action log (`BepInEx/StorageOps.log`) and a `storage audit` command that prints
  totals across loaded eligible chests plus the inventory.
- **Kill switch:** `UseStorage.Enabled`.

**9.0 Free test before any code: done (2026-10-21).** You played with MUC and QSS, without CraftFromChests
or Digi's storage module, and the jank went away. CraftFromChests and Digi's module were the main cause,
and MUC is safe to build on.

**Testing.**
- Unit tests for the planner (which chests, how much, in what order, eligibility) and count semantics.
- In-game checklists on a test world with a dedicated server and two clients. The riskiest items are pull
  races and stale owners.

**Phases.**

| # | Phase | Ships | Main risk |
|---|---|---|---|
| 0 | Scaffold (no deploy to Default). Spike: verify MUC pull semantics and how MUC handles ownership moving under in-flight requests, in its source and in game. MUC health check | nothing playable | MUC behaves differently than assumed |
| 1 | Storage network, counts and `have/need` display, toggle, status line. **Read-only** | see storage counts | count edge cases (no risk to items) |
| 2 | Borrowing ownership, then craft/build consumption: owned take, pulls, timer extension, click-again | craft/build from storage | borrow/pull races |
| 3 | Smelting | smelt from storage | — |
| 4 | Epic Loot provider, Digi handling, direct counts | enchanting from storage | Epic Loot API drift |

Each phase ends with a commit, a checklist and the user's go-ahead. Phase 1 deliberately changes no items.

## 10. Decisions (2026-10-21)

| # | Decision | Choice |
|---|---|---|
| 0 | Ranges beyond 64 m in multiplayer | Accept: ownerless chests are skipped and shown in the status line. Single player claims them (section 2). A server-side take for ownerless chests is not planned. |
| 1 | Building from chests you don't own | Borrow ownership; when that fails, click again (4.1, 6) |
| 2 | Epic Loot from chests you don't own | Dropped: Epic Loot 0.15.0 has its own table storage (8) |
| 3 | Carts and ships as storage | Off |
| 4 | Toggle key | `Home`, configurable |
| 5 | Smelter selection | Per player, local |
| 6 | Coins from chests | Moot with the Epic Loot integration gone; coins are treated like any item |
| 7 | Chest trophies in the Sacrifice tab | Dropped with the Epic Loot integration (8) |
| 8 | Default range | 30 m |
| 9 | Name | Storage (working title) |

---

## Appendix A: plan B, our own shared-container backend (from draft 2)

Build this only if MUC stops working with no fix coming. The detailed design is in git history once the repo
exists, and summarised here with the lessons from draft 2's hostile review.

**Model.** One writer per container: the current owner, or the server when the container is unowned or
stale-owned.

**Capturing edits.** Non-owners diff their local copy (base from the ZDO + pending ops) and send
**per-player, per-container sequenced ops** through the server.
- The server forwards an op to the real owner if that owner has the chest loaded.
- Otherwise the server applies it itself, at the byte level, keeping unparseable items, and calls `Save`
  explicitly.

**Confirmation and undo.**
- An op is confirmed only when a received ZDO shows its sequence number as applied. Never use revision
  numbers for this.
- Ops are accepted or rejected slot by slot.
- A rejected withdrawal is taken back from the player using one stack-type budget per frame.
- When an op can't be resolved, deposits are given back and withdrawals are kept: duplicate, never delete.
- Pending ops are persisted in the character save, and logout drains them.

**Rebase.** Flush capture first, update objects in place, and cancel any drag or split whose object was
replaced.

**Viewer state** lives in the owner's memory, not the ZDO. Item bytes are parsed only when their hash
changes.

**QSS.** Transpile its `HasPlugin(MUC)` call sites (find them by method name: 4 behavioural sites plus 1
config site), and guard `ShouldAffectNonOwnerContainer` so QSS never edits containers we don't manage.

**Testing.** A Unity-free core with a simulation that models the real behaviour:
- higher revision wins, ties ignored;
- RPCs dropped when the target has no instance or has left;
- stale owners;
- ownership changes while a write is in flight;
- crashes;
- container destruction.

**Known traps, all verified against 1.0.17:**
- an owner-0 `InvokeRPC` broadcasts to everyone;
- RPCs to an object with no instance are silently dropped;
- `Inventory.AddItem` doesn't fire `Changed` on the server;
- `Inventory.Load` drops unknown prefabs;
- every field write (position, InUse, …) bumps the ZDO revision;
- `HasPlugin` is small enough for Mono to inline it, so a patch on it might never run.
