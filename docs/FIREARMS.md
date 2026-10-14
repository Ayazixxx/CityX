# Firearms: the Glock, the AK-47, real reloads (2026-10-14)

The owner's asks, in order:

- "improve upon the pistol/gun mechanics, research third person shooters and implement what you learn. the pistol model is a
  temporary one that I made in meshy, I want the guns to have realistic reload animations and for the fingers to be in place as
  they would a real gun. improve upon the pov too i cant even see this pistol when im aiming in with it ... remember there will be
  all sorts of firearms"
- "maybe use blender mcp to make the basic glock pistol with accurate reload animations"
- "add an ak47 with reload animation too. use this file attached as a guide but remake it yourself so we dont have to credit them.
  research ak47 mechanics in 3rd person games and use that when building the ak47"

The earlier pistol work (the three-trace hitscan shot, camera kick, hit ticks, auto-reload, shoulder swap) is in
[THIRD_PERSON_PISTOL.md](THIRD_PERSON_PISTOL.md) and still holds. This page is what changed on top of it.

## What the research said (sources at the bottom)

**Reloading a pistol** (shooting-training sources): bring the gun into the *workspace* in front of the chest, where you can see it;
the strong thumb presses the release and the magazine drops free; the support hand fetches a fresh one from the belt, indexes it
(index finger along its front) and seats it with the heel of the palm -- the click is the moment the ammo is in; only an *empty*
gun needs its slide racked (over the top, the whole hand on the rear serrations). A tactical reload (a round still chambered) has no
rack, so it is quicker.

**Reloading an AK** (Shooting Illustrated, "Tips: Best Way to Reload an AK", "How to Run Your AK"): an AK magazine is not pushed
straight in. Its front lug hooks into the front of the well and the magazine is **rocked back** until the latch clicks; it comes out
the same way in reverse, the thumb pressing the paddle release behind the well. On an empty rifle the support hand then comes
**under the gun to the charging handle on the right side** and, with an open palm, pulls it to the rear and lets it go ("resist the
urge to ride the bolt forward").

**AK-47 in games**: about 600 rounds a minute (PUBG's AKM 0.1 s between shots; CS:GO 600 RPM; CoD 705 RPM), 30 rounds, the hardest
hitting of the common rifles (PUBG's AKM 47 a hit, the most of its rifles), heavy recoil. Reload: PUBG's AKM 2.25 s tactical,
2.9 s empty; CoD's AK-47 1.75 s to the point where the reload can be cancelled. **The recoil is the AK's identity**: in CS:GO it is a
fixed, learnable pattern -- the first shots climb almost straight up, then the spray drifts right, then wanders left and right with
a smaller climb. Random spread is a *separate* system (movement, jumping); the pattern itself is the same every time. Short bursts
are the recommended way to use it.

**Third-person aiming** (Giant Bomb "Over the Shoulder", The Division 2 guides, Gears community camera notes, Unreal/Unity animation
guides): aiming moves the camera *in over the shoulder* (RE4 popularised it, Gears made it the standard); the character fills the left
of the frame and the gun points into the middle; shoulder swapping is a tactical tool. Two-handed weapons are rigged with the
dominant hand driving the gun and the off hand IK'd to a point on it, the off-hand IK released during a reload.

## What was built

### The models (made from scratch in Blender, headless)

- **Glock** (`CityX/SourceArt/Glock/glock_model.py`): a Glock 19's real dimensions times 1.9 (the body is 2.6 m with wide hands; at
  2.6x the grip was deeper than the palm and the fingers could not wrap it). Parts: Frame, Slide (with the ejection port, rear
  serrations and three-dot sights), Barrel, Magazine, Trigger. It **replaces the placeholder Beretta** (`Models/Beretta_M9.obj`, the
  owner's Meshy model, left on disk unused). The item is still `pistol` and the prefab still `Pistol.prefab`.
- **AK-47** (`CityX/SourceArt/AK47/ak47_model.py`): real proportions (880 mm overall, 415 mm barrel) times 1.45 -- the size at
  which the support hand reaches the handguard with the stock in the shoulder. Parts: Body (receiver, dust cover, wooden stock, grip
  and handguards, gas tube, barrel, sights, trigger guard, magazine paddle, selector), a curved Magazine whose origin is its front lug
  (it rocks about it), the Bolt's charging handle on the right, the Trigger. The reference file the owner sent was only looked at
  (probe renders of its shape and the order of its reload, `CityX/SourceArt/AK47/ak47_reference_probe.py`); nothing of it is used.
- Both are authored in the gun's own frame (x right, y up, z the muzzle) with the **origin at the web of the pistol grip**, so one
  hold fits every gun with a pistol grip (`FirearmProfile.HoldPosition`/`HoldDipDegrees`). Each writes a `.glb` and a `.points.json`
  (muzzle, ejection port, magazine, butt, handguard...) that the profiles' numbers come from.
- `FirearmBuilder` turns a `.glb` into a prefab: each part its own child with its mesh baked to a small asset, flat-coloured URP
  materials by the Blender material's name, no textures.

### The reloads (keyframed in Blender, played on the body)

`CityX/SourceArt/Firearms/` is a small pipeline shared by every gun:

- `firearm_rig.py`: the game's skeleton (BodyRig's bones, arms-down rest) **with the twenty finger bones**, plus the real body
  mesh skinned the game's way (or, with `--no-body`, capsule stand-ins for quick check renders).
- `firearm_pose.py`: a reload is written as **key specs** -- where the gun is (in the chest's frame), where the free hand is and which
  way its fingers and palm face (in the chest's, the gun's or the other hand's frame), how far each finger is folded, where each gun
  part is. Every frame the specs are *blended and then solved* (two-bone IK for both arms, the shooting hand placed by the gun's hold),
  so a hand on a moving magazine stays on it. A part "in the hand" (the fresh magazine) rides the free hand.
- `firearm_export.py`: every bone's local rotation in the game's live skeleton (fingers included), and each part's position,
  rotation and visibility **in the gun's own frame**, with the segments (`tactical`, `empty`) and events (`drop`, `grab`, `seat`,
  `rack`) from the timeline markers. Read by `BakedMotion` (which now carries props and events).
- `glock_reload.py`: tactical 1.4 s, empty 1.73 s. Workspace, thumb on the release, the magazine drops (the game throws a copy to
  the floor), the hand to the left hip, the fresh one indexed and seated with the heel of the palm, then (empty) the overhand rack.
- `ak47_reload.py`: tactical 2.17 s, empty 2.8 s. The torso stays bladed, the rifle rolled so the well faces the hand; the thumb on
  the paddle, the magazine **rocked forward off its lug** and pulled clear, dropped; the fresh one from the chest rig at the belly,
  **hooked by its front lug and rocked back** until it clicks; then (empty) the hand comes under the receiver to the charging handle,
  pulls it to the rear and lets it slam home.

Rebuild everything for a gun (Blender 4.2, no window):

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --python CityX/SourceArt/Glock/glock_build_all.py -- --no-body
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --python CityX/SourceArt/AK47/ak47_build_all.py -- --no-body
```

Add `--render <folder> [frames...]` for check images (drop `--no-body` to see the real body), then in Unity
**CityX > Rebuild Firearms** (or the scene build).

In the game (`GunReloadDriver`): the reload is a **name** (`gun_reload`, `gun_reload_empty`) played through
`PlayerAnimationDriver.PlayLocalState`, so another player's copy plays the same motion on its copy of the gun. It is **upper-body
only**: the hips and legs stay with the Animator and the foot IK, so you can reload while walking. It eases in over 0.1 s from the
aim or carry pose and out over 0.16 s. The events drive the gun: `drop` throws the old magazine on the floor (`FirearmEffects`),
`seat` puts the ammo in (with a click), `rack` cocks it. **A shot after the click cancels the rest of the motion** (CoD's reload
cancel; after the rack for an empty reload). A body without the motion (a bare test rig) falls back to the old timed dip.

### The fingers on the gun

`HandGrip` now takes per-digit amounts (`FingerAmounts`) instead of one "closed" number, and a held item can say how its hands
hold it (`IHandGripStyle`). A gun's shooting hand: three fingers round the front strap, the **trigger finger straight along the
frame** while carried, **on the trigger** while aimed, **pulled** for a moment on each shot; the thumb lies forward along the frame.
The pistol's support hand wraps over the shooting hand's fingers, thumb forward; the rifle's support hand curls under the handguard.
During a reload the motion's own finger poses win (`BakedPoseWeight`).

### The rifle hold (`PlayerLocomotion.RifleAim.cs`)

A pistol is aimed by bending the arm until the barrel points at the target. A rifle is the other way round: the **gun is placed** --
butt in the shoulder pocket, barrel at the crosshair's point (the same depth-smoothed point the pistol uses) -- and the shooting hand
is solved onto its grip; the support hand goes under the handguard. The torso turns **30 degrees to the right** (the bladed rifle
stance: it brings the support shoulder forward so that hand can reach). Carried, it sits at **low ready** (butt lower, muzzle 28
degrees down and in); aiming or firing brings it up in about a seventh of a second. Measured: the barrel 0.0 degrees off the aim point
at -20/0/+20 pitch, the butt 15 cm from the shoulder joint, the support palm within 1.3 cm of the handguard.

### The camera: you can see the gun now

The aim camera was 4 m back and 1.4 m to the side (Godot's numbers): the gun was a speck at the edge of the frame or out of it.
Bringing it in over the shoulder at 0.6 m to the side put the character's **0.7 m head** squarely between the lens and a pistol held
in a two-handed stance under its middle (seen in the first renders). Now: **1.0 m to the side, 2.3 m back, 0.2 m higher**, eased in
over a quarter of a second (`ThirdPersonCamera.AimShoulderOffset/AimDistance/AimPivotRaise`). The gun sits left of and just below the
crosshair, pointing at it; a test checks it is on screen and that the line from the lens to it clears the head.

### The feel

- **Recoil pattern** (`FirearmProfile.RecoilPattern`): each shot of a burst kicks the view by its own entry (up and sideways),
  learnable, never random. The Glock: 1.2 degrees up a shot, settling at once between shots. The AK: a climb straight up for five
  shots, building, then a drift right, then a wander left and right with less climb -- about 6.5 degrees over ten aimed shots.
  An automatic **holds** the kick through the burst (`RecoilHoldSeconds`), so a spray climbs and must be pulled down; the view
  settles back once you stop, sideways faster than up (`ThirdPersonCamera.RecoilYawRecoverScale`). Every bullet still lands exactly
  on the crosshair (the owner's rule): the recoil moves the view, never the bullet off it.
- **Full auto**: the AK fires while the button is held, at 10 shots a second (`IEquippableGun.IsAutomatic`).
- **The gun moves**: the slide or bolt runs back on every shot and home again (75 ms), the trigger is pulled, the gun is shoved back
  and tips up (as before, after the arms are solved), a **spent case** flies out of the port -- short and fat from the Glock, long
  from the AK -- tumbles, bounces and lies on the floor for four seconds. Simulated arcs, no physics bodies: nothing they do can
  block a shot.
- **Sounds**: the reload's magazine-out clack, the click of the seat and the rack are synthesised (like the hit tick). The AK's shot
  is the pistol clip pitched down to 0.78 -- a real rifle recording would be better.

### Other players see all of it

- The held gun: `PistolWeapon` is an `ICosmeticMountable`; the AK is in `NetworkedPlayerWiring.ItemPrefabPaths`. On a copy the
  rifle is held as a rifle (its support hand on the handguard), raised to the shoulder by the copy's synced Aiming parameter.
- The reload: by name, like a sword swing; the copy's magazine drops and lies on its floor too.
- **The shots** (new, also for the pistol): each shot is announced by name (`gun_fire`, `PlayerAnimationDriver.AnnounceState`), and
  the copy's gun flashes, cracks (a 3D sound at the gun), draws a tracer down its barrel to what it meets, throws brass and cycles its
  slide or bolt. A shot also cuts short a reload the copy is still showing. The damage itself still goes through `NetworkedDamage`.

### Ways in

- `/equip ak47` (also `ak`, `rifle`), `/equip pistol` (or `glock`). R reloads, left mouse fires (held for the AK), right mouse aims,
  Q swaps the shoulder.
- `/gun recoil <n>` scales the whole pattern of the gun in hand; `/gun adsrecoil`, `shake`, `kick`, `kickangle` tune the profile.

## Adding the next gun

1. A model script in `CityX/SourceArt/<Gun>/` using `Firearms/firearm_parts.py`: the gun's frame, the origin at the grip's web,
   parts named for what moves, a `POINTS` dict.
2. A reload script of key specs for `Firearms/firearm_pose.py` (copy `ak47_reload.py`), a `<gun>_build_all.py`, run it.
3. A `FirearmProfile.<Gun>()` with the numbers and points, a builder like `AK47Builder`, an `ItemRegistry` id, the prefab in
   `NetworkedPlayerWiring` and the scene builder's item list. A shotgun or a sniper rifle may want a new `FirearmKind` (pump,
   bolt action) -- the reload by name and the profile take that without touching the shot.

## Not done / limits

- **The reload while aiming** plays the same motion: the gun comes down out of the aim (as in Fortnite, where a Heavy AR cannot be
  reloaded while aiming). Reloading on the move is fine: the legs keep walking.
- **The AK's sound** is the pistol's clip pitched down; a rifle recording should replace it.
- **Network copies**: the copy's rifle aims level along the body (no aim pitch is synced yet), and brass on a copy comes from its
  own gun's port, not from the shooter's exact pose.
- **The Glock's slide does not lock back** on an empty magazine (a real one does); the empty reload's rack stands in for the
  slide release.

## Sources

- [Tips: Best Way to Reload an AK -- Shooting Illustrated](https://www.shootingillustrated.com/content/tips-best-way-to-reload-an-ak/)
- [How to Run Your AK -- Shooting Illustrated](https://www.shootingillustrated.com/content/how-to-run-your-ak/)
- [AK-47 -- Wikipedia](https://en.wikipedia.org/wiki/AK-47) (dimensions)
- [AKM -- PUBG Wiki](https://pubg.wiki.gg/wiki/AKM) (reload, damage, fire rate patches)
- [AKM vs M416 -- Sportskeeda](https://www.sportskeeda.com/esports/news-pubg-mobile-akm-vs-m416-which-assault-rifle-is-better-and-why-ultimate-guide) (reload times)
- [AK-47 -- Call of Duty Wiki (BreezeWiki mirror)](https://breezewiki.discard.no/callofduty/wiki/AK-47) (705 RPM, reload cancel)
- [CS2 Recoil Patterns & Spray Control -- csdb.gg](https://csdb.gg/recoil-patterns/) and [Spray control theory -- Guildorder](https://guildorder.com/games/csgo/guides/spray-control-theory)
- [Full stats of Fortnite weapons -- Dot Esports](https://dotesports.com/news/full-stats-of-all-fortnite-battle-royale-weapons) and [Fortnite Wiki](https://fortnite.fandom.com/wiki/Fury_Assault_Rifle) (the Heavy Assault Rifle: 25 rounds, 2.5-3.1 s reloads, no reloading while aiming)
- [Over the Shoulder -- Giant Bomb](https://giantbomb.com/wiki/Concepts/Over_the_Shoulder), [Shoulder Swapping Aim -- Giant Bomb](https://giantbomb.com/wiki/Concepts/Shoulder_Swapping_Aim)
- [Division 2: aim down sights -- Twinfinite](https://twinfinite.net/guides/division-2-aim-down-sights-how/)
- [Weapon Animation Systems -- MoCap Online](https://mocaponline.com/blogs/mocap-news/weapon-animation-systems-guide) (support-hand IK, released in a reload)
