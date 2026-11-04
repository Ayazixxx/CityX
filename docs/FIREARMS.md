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

- **Glock** (`CityX/SourceArt/Glock/glock_model.py`): since 2026-11-03 the **Counter-Strike 2 Glock-18's side silhouette** (the owner's
  reference: "research how a glock looks and make it like that", "look at counter strike glock", "keep ours all black"), measured off the
  picture in millimetres about the web of the grip and built as outlines extruded across the gun's width, the slide's nose lofted. The
  picture looks a little down on the gun, which stood the grip up (traced, the backstrap leaned forward and the fingers could not get
  round the grip), so below the frame the grip is leaned back 8 degrees (`GRIP_LEAN_DEGREES`; the magazine's rake comes to 23). Times
  2.5: at 1.9 the grip was 16 cm tall for a 22 cm hand and the guard's opening hardly wider than a 5 cm finger (owner, 2026-11-03: "scale it up
  if you have to"). Parts: Frame (grip, trigger guard, levers, pins), Slide (ejection port, rear
  serrations, three-dot sights), Barrel, Magazine (with its baseplate), Trigger. The first model (2026-10-14, boxes from a Glock 19's
  figures) had the grip as a long slab hanging off the back and the trigger guard as a loose wire ("ours is all jacked up"). It
  **replaces the placeholder Beretta** (`Models/Beretta_M9.obj`, the owner's Meshy model, left on disk unused). The item is still
  `pistol` and the prefab still `Pistol.prefab`. The gun's points in `FirearmProfile` (muzzle, ejection port, magazine's way out, the
  support hand's palm) are the class defaults, baked into the prefab by the build: change them and rebuild. How the hands hold it: "The
  pistol in both hands", below.
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
hold it (`IHandGripStyle`). A rifle's shooting hand: three fingers round the grip, the trigger finger straight along the frame while
carried, on the trigger while aimed, pulled for a moment on each shot; its support hand curls under the handguard. During a reload the
motion's own finger poses win (`BakedPoseWeight`).

### The pistol in both hands (owner, 2026-11-03)

"he doesnt grip the glock properly", "put the finger that pulls the trigger through that hole", "look its fucking poking through", "the pov when
shooting the pistol doesnt show enough of the gun its covered by the body". What was wrong, all of it measured: the gun sat inside the hand
(`HoldPosition` 1 cm to the palm side, when the palm's surface is 4 cm from the bone), so folded fingers went through it; the grip was too small for
the hand; `ApplyStraightWrists` straightened the left hand after it was placed, so its fingers ran down the forearm's line through the gun; a straight
arm from the shoulder left the left arm out of reach and the gun level with the face, behind the head.

- **The hold is measured against the hand** (`FusedBodyShape`'s palm and fingers, `glock_model.py`'s outline). The right panel flat on the palm, the
  front strap a finger's radius short of the finger roots, the barrel rising 22 degrees above the knuckle line (the grip's rake), so the grip
  runs along the line of the finger roots. The rifles keep the old hold.
- **The fingers are authored in degrees** (`FingerAmounts.PistolStrong`, `PistolSupport`; `Authored`): an amount folds the tip with the root, which curls a
  finger through a grip. Three fingers square round the front strap and no further; **the index through the guard with its pad on the trigger, always**;
  a shot pulls it back; the thumbs lie forward along the frame, lifted clear of it. (Real shooters' strong thumb is on the far side of the frame; ours
  starts ahead of the backstrap, level with the slide, and can only get there through the gun, so it lies along the near side.)
- **The gun is placed and the arms follow** (`PlayerLocomotion.PistolHold.cs`, as the rifle): raised, the chest turns 15 degrees right, the gun goes out past
  the side of the head and over the shoulder (`PistolInChest`), barrel at the crosshair's point, the right arm solved onto it.
- **The left hand is laid on the grip** (`SetSupportHand`'s `handRotation`, `FirearmProfile.SupportHandRotation`): the right hand's rotation mirrored across the
  gun, palm flat on the left panel, a finger's thickness further forward so its fingers close over the right hand's.
- **Other players see it**: `MountCosmetic` sets the same hold, and a copy raises the pistol on its synced Aiming parameter.
- **The aim camera** is the pistol's own: `ThirdPersonCamera.PistolAim`, docs/CAMERA.md.

`PistolHoldPlayTests` checks all of this against the model's outline on the real hands, `FirearmPlayTests` that no corner of the gun is behind the head and
it is at least 6% of the screen wide. `PistolAimCapture` (Explicit, -Graphics) renders `pistol-grip.png`, `pistol-aim-game.png`, `pistol-aim-views.png`
and `pistol-hipfire.png`. The reload's `READY`, hold and finger numbers (`glock_reload.py`, `firearm_pose.py`) match these: change both and rebake.

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
- **Sounds**: the reload's magazine-out clack, the click of the seat and the rack are synthesised (like the hit tick); the AK-47 plays a
  recording instead since 2026-10-14 (see the update at the end). The AK's shot
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

## Update 2026-10-14 (later): the AK's rate, its tracers, windows

- **"make the ak47 fire rate the same as counter strike, right now it feels a bit too rapid."** Counter-Strike 2's AK-47 fires 600 rounds a
  minute, one every 0.1 s (csdb.gg, Profilerr); ours was already exactly that (`FirearmProfile.AK47`, 10 a second), and because a shot waits for
  a whole frame it comes out a touch SLOWER at 60 fps (about 8.6-10 a second). What made it look faster was drawing a tracer on every round, a
  continuous streak. Counter-Strike draws them on only some rounds for most rifles (the M4A4 every third, and since a patch always on the
  first): the AK now does the same (`FirearmProfile.TracerEvery` 3, `PistolWeapon.ShowsTracer`), on other players' copies too. The Glock keeps
  one on every shot. **Undone 2026-10-25** (owner: "not every bullet is leaving a trail try testing the ak rapid fire"): every round draws one
  again (`TracerEvery` 1). The same day the tracer's material was made truly additive (`SeeThroughMaterial`): set up with `_Surface`/`_Blend`
  alone it drew as a solid, and the cel look's black lines traced it into a black line.
- **"serious frame drops when rapid firing the ak at windows"**: fixed in the city streamer, see [PERFORMANCE.md](PERFORMANCE.md) (a refresh
  bookkeeping bug, tangents and colliders moved off the main thread; worst frame of a 3 s spray 18-46 ms before, ~4-5 ms after).
- Sources: [AK-47 vs SG 553 -- csdb.gg](https://csdb.gg/compare/ak-47-vs-sg-553/) and [How to use the AK in CS2 -- Profilerr](https://profilerr.net/how-to-use-the-ak-in-cs2-correctly-and-effectively/)
  (600 RPM); [CS:GO weapons -- biligame wiki](https://wiki.biligame.com/csgo/Weapon) (tracer frequency differs by weapon: every shot on the
  M249, every third on the M4A4); [CS:GO patch notes -- SteamDB](https://steamdb.info/patchnotes/2565539) (a tracer on the first bullet).

## Update 2026-10-14 (evening): the AK-47 reloads to a real recording

Owner: "replace the ak47 reload sound with this. speed it up so its 2.8 seconds when magazine is empty and 2.17 seconds with rounds left, but
try to maintain the pitch even when sped up." The recording (3.42 s, stereo, the magazine out, in, and the charging handle) is sped up to each
reload motion's exact length, 130 and 168 frames at 60 fps, WITHOUT raising its pitch:

- `CityX/SourceArt/AK47/stretch_reload.py` runs in Blender's own Python (its audio library reads the MP3 and writes Vorbis; numpy does the
  rest; no ffmpeg on this machine). WSOLA: short overlapping windows of the recording, each taken from further along than it is laid down and
  nudged to where it joins the last one in phase. It keeps the clicks sharp (a phase vocoder smears them) and every click in its place, only
  closer together. The ringing frequencies of the metal stay within a few percent (1626 Hz, 4479 Hz, 4565 Hz...); a plain speed-up (an
  AudioSource's pitch) would have raised them by the speed, 1.22 and 1.58 times. The source and the command are kept beside the script.
- `Weapons/Audio/ak47_reload_tactical.ogg` (2.167 s, 26 KB) and `ak47_reload_empty.ogg` (2.800 s, 33 KB) go on the AK's profile
  (`FirearmProfile.ReloadSound`, `EmptyReloadSound`, set by `AK47Builder`). `GunReloadDriver.Started` tells the gun a reload began, on the
  shooter's body and on other players' copies alike, and the gun plays the matching recording from its start; the motion's events then make no
  synthesised clicks. A shot once the fresh magazine is in, a body taken by a climb or a fall, or the gun put away stops it. The Glock keeps
  its synthesised clicks (no recording on its profile).
- The recording racks the charging handle at its end, and the tactical reload plays the same recording sped up, so it racks too, although the
  motion does not pull the handle. The clicks also fall where the recording has them, not on the motion's frames (the magazine leaves at 0.63 s).
- Tests: `FirearmTests` (both recordings as long as their motions, the Glock without), `FirearmPlayTests` (the shooter hears the recording for
  each kind of reload from its start, a shot cuts it off, another player's copy plays it when told the reload by name).

## 2026-10-14: the gun stats (docs/STATS.md)

- Firearm Damage scales every shot (and the flamethrower) at 2% a point; until now guns scaled with Strength, the melee stat.
- Fire Rate shortens the wait between shots (`PistolWeapon.FireCooldownRemaining`); Reload Speed runs the reload motion, its recording (pitched up
  with it) and the timer faster, and sends the speed to other players.
- Recoil Control takes its share off the camera climb, shake and hold and the gun kick, and the kick settles faster.
- SPREAD, CS:GO's way (`PistolWeapon.SpreadDegrees`), for the Accuracy stat: a standing or creeping first shot is still exact on the crosshair;
  running opens a cone up to 2.5 degrees, the air 4, and each shot of a held trigger after the first 0.4 more (up to 3). Aiming halves it;
  Accuracy takes its share off.

## Update 2026-10-26: the bullet trails are not see-through

Owner: "make the bullet trails less transparent." The trail was ADDITIVE (its light added to what is behind it), which all but vanishes against
a bright sky or pale glass, and it faded straight from the moment it was drawn (half gone by mid-life). Now `BulletTracer`'s material is
alpha-blended (`SeeThroughMaterial.Apply(.., additive: false)`): the gold streak is laid over what is behind it; its glow is stronger (0.75 of
the core, was 0.55) and evener along it; and it holds nearly whole for most of its 0.12 s (`BulletTracer.Opacity`, 1 - t squared) before
fading. Tests: `BulletTracerLookTests`; picture: `BulletHoleCapture` (`Logs/CityShots/bullettrail-1-was-added-light.png` and
`bullettrail-2-now-laid-over.png`).

## Update 2026-10-26: bullets go on through windows

Owner: "make it so the bullet travels through the window but does a little less damage. find out how much less damage bullets do when going
through objects in counter strike", then "lets make it 10% less damage for all guns for now". Counter-Strike: glass is its own case in the
penetration code, a 0.05 share of the damage lost plus a small flat amount by the gun's penetration power (about 10% off an AK round, 17% off a
Glock's), against 0.16 and much more with thickness for a wall; a bullet stops at its fifth solid. Sources: Valve's CS:GO surface types
(penetrationmodifier, damagemodifier, the fifth solid), the Counter-Strike wiki's Bullet Penetration, the CS:GO update of 2014-07-11 (glass
had been costing more than solid surfaces), and community reverse-engineering of the penetration code (the 0.05 glass share).

`Weapons/Firearms/GlassPenetration`: a bullet that meets glass breaks the pane as before (`IBreakableSurface.TakeBlow`) and its line is taken up
again 2 cm past it, `DamageKeptPerPane` 0.9; ONE window at most (owner, the same night: "make it so a bullet that has gone through 1 window cant go through another it just stops at whatever surface it hits unless its a player/mob/npc"): a second pane takes the bullet, its hole, and stops it (`MaxPanes` 2); anything not glass ends it (a body takes the
damage, the city's wall round a window stops it). `PistolWeapon.Fire` and the Shoota's shot both go through it, and the trail runs to where the
bullet really ended. Another player's copy of a shot draws its trail on through the same windows without breaking them
(`IBreakableSurface.IsGlassAt`, `GlassBreaker`): the glass broke on the shooter's machine and reaches everyone as broken glass does
(docs/MULTIPLAYER_STATE.md). Walls are not penetrated yet (no materials or thickness on them).

Tests: `GlassPenetrationPlayTests` (10% through a window, a second window takes the bullet and stops it, a wall stops it before or after a window, the trail goes through, a copy of the
shot only looks), `BulletHolesInRealWindowsPlayTests.ABulletGoesThroughARealWindowIntoTheRoomBehindIt` (a real city window, intact and then
cracked, and somebody in the room behind it hit for 90%).

**One window cut into pieces a frame.** A bullet through a window can now bring down the front window and the back one it had already cracked
in the same frame: two pieces-cuts at once, and `WindowSprayFramePlayTests` caught the window work at 8.7 ms against its 8 ms limit.
`GlassBreaker` now cuts at most one pane into pieces a frame (`ShatterOne`, a queue the next frames drain); the sound is not held back. The
spray's worst frame went to 4.9 ms (7.3 ms before bullets went through windows), the windows' own work to 2.8 ms.
