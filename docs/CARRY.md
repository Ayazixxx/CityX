# Tap E to take, hold E to carry (Skyrim)

Owner, 2026-10-21: "make it so holding E on objects carrys them while tapping E key equips it. make the holding system like skyrims".

## The key (`Character/Interaction/InteractKey.cs`)

- **Tap** (E let go within `HoldSeconds`, 0.3 s): every item's pickup acts on the tap. This covers weapons, the jetpack, the parachute,
  hats, clothes and dropped items. Pickups used to act the moment E went down, which would have taken every item the player meant to lift.
- **Hold** (0.3 s): `GrabCarry` lifts what the player looks at.
- The press is timed once per frame in game time (`InteractPress`, plain and tested).
- **Not while typing** (2026-11-06): while a text box has the keyboard (the chat, the console), E is no press at all (`InteractKey.IsTyping`,
  told by `UI/Hud/TypingFocus`), so an E typed into the chat picks nothing up. `TypingEPlayTests`.
- Doors, benches, books, cars and switches still act on the press; nothing about them is carried.

## Tap = equip (`Inventory.PickUp`)

- A weapon goes straight into an empty hand, in the primary slot, else the secondary. With a weapon already in hand, it is stored.
- Anything worn (clothes, hats, back gear) is put on, and what was worn there goes into storage.
- What cannot be equipped is stored. `CanPickUp` makes sure a dropped item is only taken when it will fit somewhere.

## Hold = carry (`Character/Interaction/GrabCarry.cs`, `Carryable.cs`)

- **What:** anything within 3 m that the player looks at (within the look cone, `LookDot` 0.55): an item lying in the world, a dropped
  item, or a limp body's bone (`Grabbable`). The player's own body is never a target, and neither is anything worn or held by a body.
- **Becoming physical:** most items lie as still props. On first lift, an item gets a 2 kg rigidbody and a solid box around its model
  (`Carryable.Physical`). It stays physical afterwards, so it falls and tumbles where it is dropped.
- **Held:** a bounded spring pulls the thing toward a point in front of the chest (1.6 m up), along the look, at the distance it was
  grabbed from (1.3 to 2.5 m). Because it is pushed rather than placed, it bumps into walls, and a heavy body part lags behind.
  - A loose item floats free of gravity and turns back to the angle it was picked up at, turning with the player as in Skyrim.
  - A bone keeps its gravity and joints, so a body hangs from the grabbed part.
  - It never collides with the carrier.
- **Let go of E:** the thing drops.
- **Left click while holding:** the thing is thrown along the look at 10 m/s. Combat ignores that click (`HandsFull`).
- **Ends early:** dying, or the thing getting wedged more than 4 m from where it should be.

Tests: `GrabCarryTests`, `GrabCarryMathTests`, `InventoryTests.PickingUp*`, `CarryPlayTests` (the real key: hold, carry, drop, throw;
tap to equip). `CarryCapture` (Explicit, `-Graphics`) films it: `Logs/CityShots/carry-NN.png`.

## Not done

- **Multiplayer:** what is carried, and where it lands, is local to this machine. Items lying in the world are not shared anyway
  (dropped items are, but by id: a carried dropped item lands elsewhere for others). Another player does not see the carry.
- There is no carry pose: the arms stay as they were.
