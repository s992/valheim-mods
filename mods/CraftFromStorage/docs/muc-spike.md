# Phase 0 spike: how MultiUserChest pulls and ownership changes behave

Read from the decompiled MUC 0.6.2 (`ContainerHandler`, `ContainerRPCHandler`, `InventoryHandler`,
`InventoryBlock`, `PickupPatch`, `GamePatches`) and vanilla 1.0.17 `Inventory.MoveItemToThis`.
The in-game checks at the bottom are still to do.

## The pull call

`playerInventory.MoveItemToThis(chestInventory, chestItem, amount, x, y)`, the call shape QSS restock uses.
- **Vanilla:** this calls the private `AddItem(item, amount, x, y)`. It only removes from the source when the
  item's stack hits 0.
- **MUC:** its prefix on that `AddItem` sees an item from a chest this client doesn't own, landing in an
  inventory it does own. It sends `MUC_RequestItemRemove` and returns "added", but the player's inventory
  doesn't change yet.
- **Nothing changes locally until the reply arrives.** The local chest copy is untouched until the owner's ZDO
  syncs back. MUC marks the target slot `(x, y)` as **blocked** until the reply.

## Answers to the plan's questions

| Question | Answer |
|---|---|
| How is the item identified? | **By chest slot only.** The owner removes whatever is at `fromPos` when the request arrives; nothing checks it's the same item. If someone re-sorted the chest meanwhile, you get a different item. Nothing is lost (you get exactly what was removed), but a pull can deliver the wrong material. |
| Partial amounts | The owner removes `min(stack at fromPos, amount)` and the reply says how many. |
| Where it lands | At `(x, y)`. If that fails (slot filled meanwhile): it tops up a matching stack there, and **drops the rest at the player's feet**. Not lost. |
| On failure | Empty slot at `fromPos` or not owner: reply "failed", nothing moves. If the request had a swap item, it's given back (or dropped). |
| Timeout | **None.** The target slot stays blocked until a reply arrives. |
| Stale owner (player portalled away) | **Replies quickly with "failed".** MUC's RPCs are *global* routed RPCs to the owner's peer, so the handler runs even without the chest loaded, then answers with its fallback response. This is better than I assumed: not a silent drop. |
| Owner disconnected | The server drops the request. **No reply ever comes.** |
| Owner 0 (nobody) | MUC refuses to send (`HasOwner` check). |
| Ownership moves while a request is in flight | The old owner checks `IsOwner()` when handling, so it replies "failed" with nothing moved. If it handled the request *before* the transfer, the change is saved and carried over with the ZDO. Either way nothing is duplicated or lost. This makes borrowing safe alongside MUC traffic. |

## Two findings that change the plan

**1. A pull that never gets a reply disables auto-pickup for the rest of the session.**
- MUC's `PickupPatch` refuses *every* auto-pickup while any slot in the player's inventory is blocked.
- `InventoryBlock.ReleaseBlockedSlots` is never called anywhere, so only a reply unblocks a slot.
- So if a chest's owner disconnects mid-pull, the player can't pick anything up until they relog. It's a
  latent MUC bug; QSS restock can trigger it too, but our pulls would make it more frequent.
- **Plan change:** on pull timeout (5 s), release that slot in MUC's `InventoryBlock` by reflection (MUC
  internals, behind their own availability flag). A late reply after that still lands or drops at the
  player's feet.
- Also keep pulls few and batched. Even successful pulls pause auto-pickup for one round trip.

**2. Borrowing: "granted" can arrive before the chest's latest contents.**
- Vanilla's open handshake calls `ForceSendZDO` and then replies, but the ZDO and the routed RPC travel
  separately. The reply can overtake the data.
- Editing a stale local copy as the new owner would then save it over the real contents. That loses items.
- **Plan change:** after "granted", wait until the local ZDO shows us as owner (owner and data arrive in the
  same ZDO update), then call `Container.Load()`. `TakeNow` additionally requires the local copy to be
  current (`m_lastRevision == DataRevision`), and reloads first if it isn't. That precondition protects
  every owned-chest edit, borrowed or not.

## Health check (implemented)
`MucHealth.Check` runs in our plugin's `Start` (after every plugin's `Awake`). It requires:
- MUC loaded at ≥ 0.6.2;
- MUC's patches present on `Inventory.AddItem` (both overloads), `Inventory.RemoveItem(item)`,
  `Container.RPC_RequestOpen` and `InventoryGui.UpdateContainer`.

When any of these fails, non-owned chests are skipped and a warning is logged. The on-screen status line
comes in phase 1.

## In-game checks (to do, needs two clients)
Riskiest first:
1. **Disconnect mid-pull.** Player A has a chest open (so A owns it). Player B starts a QSS restock from it,
   and A quits the game at that moment. If B's auto-pickup stops working afterwards, finding 1 is confirmed.
2. **Stale owner.** A opens a chest, closes it, and portals away. B restocks from that chest from about 70 m
   away. Expect "nothing restocked" right away, not a hang.
3. **Re-sort race.** A sorts a chest (QSS) while B restocks from it. B may receive a different item. Check
   that nothing disappears (`storage audit` once it exists; until then, count by hand).
