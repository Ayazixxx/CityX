# Abilities in multiplayer

**Standing rule (from the project owner):** every ability is visible to every player on a server, and a new ability is built that way *the first
time*. "Done" for an ability includes other players seeing it. It is not a follow-up task.

Asked for: "make it so shards and fireball and all the spells are visible to all players on multiplayer, and when making new abilities moving
forward just make them like that the first time."

## How it works

An ability is a plain component on the player body. Players are **owner-authoritative** (see `MULTIPLAYER_ARCHITECTURE.md`): the owner's copy does
the real thing (movement, the real projectile, damage). Everyone else's copy of that body only *shows* it.

```
owner's copy                  server                    every other player who can see the body
ability does its thing
ability.Fired(event) ───────► SendAbilityToServer ────► BroadcastAbility (ExcludeOwner) ──► ability.PlayRemote(event)
```

- **`IReplicatedAbility`** (`Character/Abilities/IReplicatedAbility.cs`): `AbilityId`, `event Fired(AbilityEvent)`, `PlayRemote(AbilityEvent)`.
- **`AbilityEvent`**: `Kind`, `Origin`, `Direction`, `A`, `B`, `N` -- plain numbers, the ability decides what they mean.
- **`NetworkedAbilitySync`** (on the body next to `NetworkedAnimatorSync`) finds every `IReplicatedAbility` on the body, sends the owner's `Fired`
  events as one reliable RPC pair keyed by a hash of the ability id, and calls `PlayRemote` on the other copies. **A new ability costs no
  networking code.**
- **Interest management applies**: only players within the body's view distance (100 m) are sent it.
- **The server does not trust the relay**: numbers must be finite and sane and the origin within 60 m of where the server has the caster
  (`NetworkedAbilitySync.IsPlausible`, checked again on the receiving client).
- **`PlayRemote` shows, it never does**: no damage, no cooldown, no input, and it must never raise `Fired` (that would bounce it across the
  network for ever). Projectiles have a `Cosmetic` flag for this: a cosmetic projectile flies, bursts, sticks and sounds like the real one but
  hurts nothing. Be tolerant of anything in the event: it came from a stranger's machine.
- **`RemoteAbilityPose`**: holds the raised-hand cast pose on a remote copy (a remote copy runs no input reader, so nothing else would),
  its palm toward the way the spell went (the event's direction).

### The cast pose (2026-11-15)

Owner: "when you are holding a 1 handed weapon and use a projectile spell like fireball or ice shards, it comes out of your hand that isnt
holding a weapon so typically your left ... he just lifs his left arm up and faces the palm of his hand toward the target. make the right hand
unarmed projectile magic cast also face the palm of the hand toward the target if it doesnt already" (it did not: the old pose swung the right
upper arm up 130 degrees about a fixed axis, no elbow or wrist, the palm wherever it ended). Now (`PlayerLocomotion.Casting.cs`): the free hand
reaches out toward where the spell goes (two-bone IK, from its shoulder 72 cm along the aim, 30 cm out to its own side and 20 cm down, so the
game's camera over the right shoulder sees the left one past the head), the palm to the target, the fingers up and open (in a fist fight
too). The right hand, or the LEFT when the right holds anything (`CastsWithLeftHand`: a sword, a knife, the grapple gun, a pistol...); a
staff or a wand still casts through itself. A spell from the bar (fireball, ice shards, arcane orb) leaves from that palm
(`CastPalmPoint`, worked out from the shoulder since the hand has not come up yet on the frame it is cast) toward the crosshair; the event
carries that origin, so a copy's spell leaves its copy's palm too, and the copy picks its own free hand by what it holds. Applied after the
straight wrists, which it bends, as the barrier's hands are (they share `HoldPalmOut`). Tests: `PlayerLocomotionCastPosePlayTests`;
`CastPoseCapture` renders `Logs/CityShots/cast-*.png`.

## What each ability sends

| Ability | Id | Fired | Shown on other players' copy |
|---|---|---|---|
| Fireball (1) | `fireball` | once per cast: origin, direction, speed | the fireball (bursts where it lands), cast sound at the hand, raised hand |
| Ice shards (3) | `ice-shards` | once **per shard** (N = which of the four; B = how long the hand stays up) | each shard flying, sticking in the wall / character on that machine and ageing 30 s / 10 s, "shing", raised hand with the first |
| Sword wave (7) | `sword-wave` | once, when the crescent leaves the blade | the crescent (stopped by the world, hurts nothing). The sword swing itself rides the sword-animation sync |
| Bloodrage (8) | `bloodrage` | once at the start (A = duration) | the red aura and the Bloodrage walk/run/jump for the same time |
| Octaslash (6, sword) | `octaslash` | wind-up begun, dash begun (direction), ended | the coiled pose, the lunge and the eight slashes on the same timeline; hurts nothing -- see docs/OCTASLASH.md |
| Acid rain (5) | `acid-rain` | once per cast: the storm's centre | the same storm, cosmetic (clouds, rain, glow, sound; burns nothing) -- see docs/ACID_RAIN.md |
| Self Smite (9) | `self-smite` | once per cast | the same animation, cloud, bolt, burst, light, sky flash and thunder on the same timeline; hurts nothing, never rooted -- see docs/SELF_SMITE.md |
| Grow, Shrink (0, and the Wizard tree) | `body-size` | once per cast or early end | the swirl of motes, the chime and the raised hand; the SIZE itself comes with the body's NetworkTransform (it syncs scale), so a late joiner sees it too -- see docs/SIZE_SPELLS.md |
| Sword spin (2) | `sword-spin` | charging begun, turn begun (A = 1 for air, B = charge), ended | the Blender animation (the turn is inside it) and the light trailing from the blade, on the same timeline -- see docs/SWORD_SPIN.md |
| Stab dash (Sword tree, sword) | `stab-dash` | begun (direction), ended | the Blender animation (the squat, the thrust, the lunge) on the same timeline; the slide and facing come with the body's transform; hurts nothing -- see docs/STAB_DASH.md |
| Glacier Spikes (Cryomancer) | `glacier-spikes` | once per cast: the line's start, its way, A its length, B its seed | the same field of crystals from the same seed, on the copy's own ground, and the raised hand; hurts nothing. Arming and its arrow are the caster's own -- see docs/GLACIER_SPIKES.md |

The spin's rise is ordinary movement and arrives with the body's own transform; its turn is inside the animation (since 2026-10-17), so a
copy whirls smoothly whatever the transform sync's rate.

## Both bodies get the abilities

There are two kinds of body: the scene's baked player (the host's, and the only body in singleplayer) and `NetworkedPlayer.prefab` (every
player who joins is spawned as it). The casters used to be added only to the baked body, so **a player who joined had no spells at all**.
`PlayerAbilityWiring.AddAbilities` (`Character/Editor`) now puts the innate abilities on both from one list; `CityMassingSceneBuilder` and
`NetworkedPlayerBuilder` both call it.

## Checklist for a new ability

1. Write the component. Implement `IReplicatedAbility`: raise `Fired` when the **owner's** copy does something others should see; in
   `PlayRemote` show it (cosmetic projectile, aura, pose, sound) without doing it.
2. Give any projectile a `Cosmetic` mode (skip damage, keep everything visible).
3. Add it to `PlayerAbilityWiring.AddAbilities` (and load its prefabs there by path). Rebuild the scene (`CityMassingSceneBuilder.Build`), which
   rebuilds `NetworkedPlayer.prefab`.
4. Tests: show-it-without-hurting (`AbilityPlaybackPlayTests`), and over real sockets (`NetworkedAbilitySyncPlayTests`).

`AbilityReplicationGuardTests` fails if you skip steps 1 or 3: a component named `…Caster` / `…Ability` that does not implement the interface;
an `IReplicatedAbility` that is not on `NetworkedPlayer.prefab`; duplicate ids.

## Not covered (and why)

- **Held items**: replicated since 2026-10-05, see docs/MULTIPLAYER_STATE.md (the sword spin now shows the blade; the old note below is out of date).
  sword spin shows the arm and swoosh without the blade, and the wave appears without a sword swinging it. Equipping is its own piece of work.
  The flamethrower, an equippable item, is in the same gap.
- A joining player has no inventory, so the sword abilities (2, 7) are only usable on the host's body until equipment exists on spawned bodies.
- Damage is still the caster's machine's decision (owner-authoritative, like everything else about players); what a remote machine shows is
  cosmetic. Hits on other players are not yet synced as health.
