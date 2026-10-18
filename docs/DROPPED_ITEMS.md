# Dropped items: what a body lets go of when it dies

Asked for (2026-10-17): "make it so enemies and players drop their weapon they are holding when they die it just becomes a separate object
with gravity and can be picked up and added to inventory. so the swordsman would drop the combat sword he is holding."

(docs/GAME_DESIGN.md's Death section recommends dropping every carried item in normal worlds. That was Claude's suggestion, not a decision.
Only the weapon in hand drops for now.)

## What happens

- **A player who dies drops the weapon in their hand** (`DropsWeaponOnDeath`):
  - it leaves their inventory, without going back into storage (`Inventory.TakeWeaponInHand`);
  - a holstered second weapon stays theirs;
  - after a respawn their hands are empty until they pick it up again.
- **The enemies drop theirs**: the swordsman his combat sword (`SwordsmanBrain.OnDied`), the Shoota his AK-47 (`ShootaBrain.OnDied`).
- **It falls from the hand** (`DroppedItem`):
  - it is the item's own model with everything of the item switched off (`Items/InertModel`, the same stripping another player's copy uses);
  - it sits on a rigidbody (3 kg) with a box round its visible meshes, never thinner than 5 cm, so a blade does not slip through the street;
  - it leaves the hand with the body's own speed, half of a killing blow's fling, 1.5 m/s up and 1.2 m/s out from the body, turning over at
    4 rad/s; it tumbles, comes to rest on the street, and lies there;
  - it never collides with the body it fell from (it starts inside its hand).
- **Picking it up** (`DroppedItemPicker`, on the local player's body):
  - E picks up the dropped item the player looks at most directly, within 2.5 m of their feet;
  - the look cone is 45 degrees, wider than other pickups' 35, since the item lies on the ground at the feet;
  - it goes into the inventory (`Inventory.HasRoomFor` first: a full inventory leaves it lying);
  - a dead player picks up nothing.
- **No ground under it yet** (another player's drop far away, where this machine has not built the street): it waits in the air where it was
  dropped and falls once there is ground within 60 m below. If it falls 60 m past where it was dropped (its street unloaded), it goes back
  there to wait.

## Multiplayer

`DroppedItems` is this machine's record of what lies around (`DroppedItems.Shared`); `DroppedItemsNetwork` (on the city streamer, added by the
scene builder) keeps the records the same, with FishNet broadcasts like the glass and the doors.

- **A player's drop is shared.** It is shown on the dropper's machine at once and sent to the server, which checks it and tells everyone:
  - a known item, finite and sane numbers, at most 40 m/s;
  - an id of the sender's own: the high half is the client's id + 1, so ids never meet;
  - the sender's body within 8 m of the drop;
  - at most 4 drops a second per player, and 300 lying at once (the oldest is cleared away).
- **Picking up a shared item asks the server**:
  - the first player to ask gets it, if their body is within 12 m of where it was dropped;
  - the server tells every machine it is gone, and only then does it go into that player's inventory;
  - so two players can never both take the same sword.
- **A player who joins** is sent everything lying around, where it now lies on the server's machine.
- **An NPC's drop stays on its machine**, as the NPC does (an NPC exists only on the machine that spawned it). So does everything in
  singleplayer, and it is taken at once.

## Tests

- `DroppedItemsRulesTests` (EditMode): what the server lets through, whose id is whose, a bottle's cracks surviving the trip, and whether an
  item fits.
- `DroppedItemPlayTests`:
  - a player dies holding a sword: it leaves the hand and the inventory, lands on the ground, and another player looking at it picks it up;
  - one behind the player's back is not in reach;
  - the swordsman drops his combat sword, the Shoota his AK-47;
  - a drop with no ground under it waits, then lands when the street is built.
- `NetworkedDroppedItemsPlayTests`, over real sockets:
  - a sword dropped by player A lies on player B's machine, B picks it up and it is gone from A's;
  - two players asking at once: one gets it;
  - a drop 300 m from the dropper is refused;
  - a late joiner sees what still lies there.
- `DroppedItemCapture` (Explicit, `-Graphics`) films the swordsman's sword falling to the street (`dropped-sword-*.png`).

## Not done

- Only the weapon in hand drops: not the holstered one, the clothes or the storage.
- Dropped items stay until picked up (no despawn timer).
- There is no "press E" prompt on screen, as for the other pickups.
- An item's state rides along (a bottle's cracks), but guns keep no ammo count (theirs is not saved anywhere yet).
