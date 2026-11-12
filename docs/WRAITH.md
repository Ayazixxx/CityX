# The Wraith

The first real enemy (the swordsman, the Shoota and the Crimson Claw are testers): a low-level ghost that floats about the streets, sneaks up behind a player and bites their neck. Owner,
2026-10-26: "make a new enemy called "Wraith" its a low level enemies that dies very quickly but sneaks up on you and tries to bite you and does a lot of damage if successful".

Owner, 2026-11-05, with their Meshy model and a Mixamo clip: "make the head replaced with this model's head, I also like the body of the model attached better but want it to whisp away into nothing towards the bottom of it. how can we get the bottom to whisp away almost in a gas like state like a ghost? also I want him to try to sneak up behind you and sink his teeth into your neck using the wraith neck bite.fbx animation. make blood effects where he bites". Then: "the model's arms are all fucked up because they are meshy generated" so use "the arms you have now that are the same as the playermodel".

`/spawn wraith` puts one in front of you (`/spawn wraith 5` a row of them).

## What it is

| Piece | Where | What |
|---|---|---|
| Body | `Character/Editor/WraithBuilder.cs` (menu CityX > Rebuild Wraith, and with the scene), from `Character/WraithModel/PhantomMawBody.glb`, made by `SourceArt/WraithBody/wraith_body.py` | the owner's model cut from 228k triangles to 19k: its arms taken off at the sides of the torso, stood in the body's frame with its head the size and place of the player's. The head rides the head bone, the torso the spine, the tail the hips |
| Arms | the same mesh | the player's own arms and hands, every triangle of the fused body wholly on them, with their own skinning. They are most of its 72k triangles |
| Teeth | `wraith_body.py` cuts them off the model; hung under `Jaw` | solid white, opaque, on a see-through body. They and the eyes shrink away when it dies |
| Eyes | `WraithBuilder`, in the model's sockets | glowing red (owner, 2026-11-05: "make the eyes on the wraith glowing red"; solid red before, as asked on 2026-10-26): the eyes give off red alone, six times past white (`EyeGlowColour`), and the game's bloom spreads that into the glow round them; their own colour made brighter brought its little green and blue up too and turned them pink. The material's emission must be flagged `RealtimeEmissive`, or the keyword is dropped when it is saved and they do not glow at all. A haze of particles round each eye was tried and taken out: added to what was behind it, laid over it or multiplied into it, drawn after the body, it showed past the head's edge and never on the ghost's own face (why was not found: the body's shader writes no depth). `WraithCapture`'s `wraith-eyes-*` pictures are taken through the game's own grade (bloom, tonemap); its other pictures have none |
| Surface | `View/Shaders/CityXGhost.shader` | see-through, brighter and more solid at the edges (Fresnel), smoke drifting up inside; unlit, drawn after the cel look's black lines so nothing outlines it, no shadow; `_Fade` and `_Flash` set live |
| Gas | below | |
| Glow | a point light at its chest | a pool of pale blue on the street at night |
| Collider | a capsule, hips to the top of the head | what blows and bullets meet |

## Mending the body by hand

Owner, 2026-11-05: "the lines around the arms and neck are a bit jagged ... you can tell its two models stitched together", and they will
mend it themselves. The model in the project has no arms (the player's are put on by `WraithBuilder`), so the joins exist only in what
the builder makes. `CityX > Export Wraith For Mending` (`Character/Editor/WraithExport.cs`; in batch: `-executeMethod
CityX.CharacterEditor.WraithExport.Export`) writes that as `CityX/SourceArt/Wraith/WraithAsBuilt.obj`: an object `Body` (head, torso,
tail and arms, 36k vertices) and `Teeth` (for reference), in metres, Y up, as Blender's OBJ import takes them. The file is not
committed. Not built yet: taking a mended mesh back (its skin weights and the tail's fade would be copied over from the built mesh by
nearest point).

## Its tail turning to gas

Three things together, and no thrown ribbons (the first build's: "that just looks like a tornado sitting beneath him"):

1. **The tail wears away.** The vertex alpha falls from solid at 1.15 m to nothing at 0.3 m, above the tail's tip at 0.13 m, so it has no visible end. The ghost shader does not fade it evenly: it eats into it with rising streaks of noise.
2. **The tail writhes.** The shader's sway, growing to 14 cm at the tip.
3. **Soft puffs come off it** (the `Gas` particles, the flamethrower's smoke puff): 30 a second, staying where they came off as it drifts, each growing as it thins to nothing.

## The hunt (`Character/NPC/WraithHunt.cs`, pure; every number here is my first go)

| Phase | What it does |
|---|---|
| Drifting | as below. It notices a player within 16 m, and loses one past 28 m |
| Stalking | makes for a point 1.4 m behind them at 3.2 m/s while their body faces away (a player walks at 4). Within 60 degrees of their facing it is "seen": from far off it creeps at 1 m/s, within 5 m it goes round them at 1.8 m/s, and keeps off |
| Lunging | from behind and within 2.4 m. Half a second, committed: a glint at its head and its attack noise (the owner's recording, `Resources/Wraith/WraithAttack.mp3`) are the tell, and a dash or a step away beats it |
| Biting | 30 damage as the teeth go in, then 6 every 0.45 s for 1.9 s (about 50 in all, of a player's 100), each a 0.4 s stun and a spurt of blood from the neck. It stays on the neck as they move. It can be killed off them |
| Thrown | 0.77 s, flung back off them |
| Recovering | 0.6 s, then it leaves them alone for 8 s |

Reading which way a body faces is not reading its input (the owner's rule on fair enemies, `feedback_fair_enemies_no_input_reading`): it does not know where the camera looks.

**The clip** (`Wraith Neck Bite.fbx`, by `SourceArt/WraithBite/wraith_bite.py` and `SourceArt/recorded_clip.py`, into `Resources/Wraith/WraithBiteMotion.json`) is played bone by bone over the drifting pose (`Wraith.Bite.cs`). It holds only the upper body, and the whole Wraith is moved so its teeth are on the neck, whatever the recording's reach, since a player stands 2.6 m tall and the clip's body 1.8 m. Its teeth go to the back right of the neck: both heads are as wide as the shoulders, so straight behind it sat half inside the prey's, and that is the side the game's camera looks past.

**Blood:** `Gore.Blood` at the neck on every spurt, on top of the wound the blow itself shows on the victim. The Wraith itself still does not bleed (`IBloodless`).

## The punch (2026-11-11)

Owner: "give the wraith a second attack where he punches the opponent. make it so he always uses the bite as his first attack but after that it is 50/50 whether he does the punch or bite, but make it so if he is fighting a large opponent like crimson claw for example then he only uses the punch. no attack sound for the punch yet".

- **Which** (`WraithHunt.Choose`, pure): a large body is only punched; otherwise the first attack on a prey is the bite, and after that half are punches (`PunchShare`, rolled on `WraithHunt.Dice`). A new prey starts again from the bite (`NewPrey`). Large (`Wraith.MeasurePrey`): 3.5 m tall or more, or reaching 1.5 m or more out from its middle (the Claw is 3.1 m tall and 2.4 m out; a player stands 2.6 m and reaches 0.6). A wide prey is gone for from that much further off (`Prey.Radius`).
- **How** (`Wraith Punch.fbx`, by `SourceArt/WraithPunch/wraith_punch.py`, into `Resources/Wraith/WraithPunchMotion.json`): `WindingUp` plays its "swing" (drawn back, then over and down) in 0.5 s while the Wraith closes to where its fist lands on the prey's body (as far off its middle as the prey reaches out plus the fist's reach, measured on its own body as the blow lands); the blow lands as the swing ends if the fist is within 0.7 m of the body (20 damage, 0.5 s stun, no blood); `Punching` plays its "follow" back to the stance in 0.52 s and it rests as after a bite. Both play at twice the recording's pace, as the bite does (each is written out at 60 frames a second from 30). The tell is the glint; no sound yet.
- **On every screen:** the two are phases of their own, so a copy told the phase plays the right clip with nothing more sent.

## How it moves when not hunting (`Character/NPC/WraithDrift.cs`, pure)

- It drifts toward a heading for 4-9 s at 1.5 m/s, then lingers 2-5 s, hanging in the air and turning to look somewhere new, then drifts again. It turns through its headings at 45 degrees a second (a curving path) and its speed eases in and out. Past 45 m from where it was born it heads home.
- It hovers with its root 0.3 m over the real ground beneath it (a ray down each frame), and bobs 12 cm up and down every 2.8 s.
- `NpcSteering` steers it round walls, as the civilians are.

## How it holds itself (`Character/NPC/WraithPose.cs`, pure)

No Animator: `Wraith.LateUpdate` sets every bone from a few slow sines over the body's rest, each a turn about the body's own axes. The trunk leans 12 degrees into its drift and banks 6 into a turn; the arms hang and swing like pendulums, the two out of step; the head wanders idly, and turns to look at a player within 10 m. Hit, it jerks 15 degrees back and its arms fly up 25, settling over 0.9 s, and it flashes white.

## Fighting it (`Character/NPC/Wraith.cs`)

- 18 health: one pistol round or two punches ("dies very quickly"). Killing it is worth 15 XP.
- Dead, it fades away over 1.1 s, rising, and is gone; the gas stops.
- **Its prey** (2026-11-08): the nearest of what the enemies fight, a player or a summon on their side (`Opponents`); a body without a human
  neck (a Wraith, the Crimson Claw) is bitten at its head or high on its back. Its hunt does not steer round the body it hunts.
- **Summoned** by the Wraith Staff it fights on its summoner's side, and a wild one killed leaves the staff one time in 33 (docs/WRAITH_STAFF.md).
- **In a session** (`NetworkedWraith`): the machine that spawned it hunts and bites; on every other machine a copy follows it, posed from how it moves. The owner tells the copies the phase, whose neck it is at, and each spurt of blood. A blow on a copy is sent to the owner, and its health comes back. Whoever it bites takes the damage through their own copy (`NetworkedDamage`), so a bite on another player's body lands on their machine.

## Tests

- `WraithHuntTests` (EditMode, pure): the first attack a bite, then 50/50, a large body only punched, a new prey bitten first again; the punch landing as its swing ends, and nothing on a miss; it comes on behind a turned back and lunges; faced, it goes round and never lunges; a lunge that misses or is dashed through does nothing; a bite's damage; the wait after; losing someone who gets away; letting go of the dead.
- `WraithDriftTests`, `WraithPoseTests` (EditMode, pure).
- `WraithTests` (EditMode, the prefab): the model on the player's arms, worn away above its tip; the head on the head bone and the tail on the hips; the gas; the materials; the eyes and teeth; two punches kill it.
- `WraithPlayTests`: it hovers and bobs; floats about; looks at the player; **sneaks up behind a real player and bites their neck, with blood**; **faced all the time it never bites**; a copy told of the bite plays it and does nothing of its own; killed it fades and is gone; `/spawn wraith` through the real console.
- `NetworkedPlayerStateSyncPlayTests.AWraithsBiteShowsOnTheOtherPlayersCopyWithItsBlood`.
- `WraithCapture` (Explicit, `-Graphics`): by day beside the player, its tail close, floating, the lunge and the bite from three sides, thrown off, by night, and hit. `Logs/CityShots/wraith-*.png`.

## Known

- 72k triangles with no lighter version: fine for a few, not a crowd. The arms and hands are most of that, and could share the body's lighter versions when those exist.
- Several hunt alone: they do not take turns (`EnemyPack`).
- The owner's Meshy file is not in the project, only the cut-down `.glb`.
