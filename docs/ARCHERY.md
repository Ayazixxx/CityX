# Bow and crossbow

Owner, 2026-10-28: "research video game bow and arrow mechanics and put a bow and arrow and crossbow into the game. im thinking the bow you hold
right click to aim then hold click to fire and it shoots farther depending on how long how long you hold it. i think that on full charge it
should travel a large distance instantly like the bullets travel instantly but the arrow will eventually start to fall after traveling a long
distance. and the crossbow will also be right click to aim in but no hold click to fire charged shots. just reload and click to fire, reload
time should be kind of long because crossbow does big damage. the crossbow bolt also travels a long distance instantly then begins to fall ...
research video games with bow and arrow and crossbow mechanics and decide what would work best in our game. and make basic models for both of
them i will make better ones later when needed".

## What other games do (researched 2026-10-28)

| Game | Draw / load | Damage by draw | Flight | Notes |
|---|---|---|---|---|
| Skyrim ([UESP](https://en.uesp.net/wiki/Skyrim:Archery)) | full draw 0.4 s + 1.66 s / bow speed, about 1.6 s | not full until fully drawn; a fuller draw flies faster and farther | a real projectile with drop; a slower bow shoots higher with less drop | hold to draw, release to loose |
| Breath of the Wild ([Zelda Wiki](https://www.zeldawiki.wiki/wiki/Ancient_Bow), [guide](https://gamertagmythras.com/blog/zelda-breath-of-the-wild/botw-best-bows-guide)) | per-bow draw speed (Great Eagle 1.8x a Royal Bow) | none | per-arrow gravity, default -9.8; the Ancient Bow's flies straight a long way before it drops | bullet time in the air |
| Horizon Zero Dawn ([wiki](https://horizon.fandom.com/wiki/Sharpshot_Bow)) | hold to draw; a cross appears at full | a half draw is far less precise | drop over range | Handling = draw speed |
| Minecraft ([feedback post](https://feedback.minecraft.net/hc/en-us/community/posts/9754428706189-Bow-and-Crossbow-differences)) | bow full draw ~1 s; crossbow load 1.25 s | bow 1..6 (crit at full); crossbow 7-11 fixed | both fall | crossbow: load, then click; always full power |
| Apex Legends, Bocek ([Prima](https://primagames.com/featured/apex-legends-bocek-guide)) | full draw ~0.5-0.8 s | 25 undrawn, 60 full | speed and trajectory scale with the draw | hip-fire accurate |
| Valheim ([guide](https://gamever.io/knowledge-base/every-bow-in-valheim-six-weapons-and-the-skill-is-the-real-upgrade)) | 2.5 s at skill 0 to 0.5 s at 100 | max at full draw | 35 m/s arcs, 60 m/s flat | |
| Rust ([Facepunch wiki](https://wiki.facepunch.com/rust/item/arrow.hv)) | compound bow drawn 1 s, fully only standing still | crossbow 48, bows 40, headshot x1.5 | arrows drop | the crossbow reload is slow |
| Hunt: Showdown ([wiki](https://huntshowdown.wiki.gg/wiki/Weapons/Crossbow)) | crossbow reload 5.3 s | 246: a one-shot | 150 m/s, drop from 20 m | the sniper of bows |

Common to all: **hold to draw, release to shoot; damage, speed and flatness grow with the draw, full in about a second**; a crossbow is **loaded
then clicked, always full power, slower to reload, harder hitting**; arrows are real things that fall, faster ones flatter.

## What was built (2026-10-28)

| | |
|---|---|
| The items | `bow` (`/equip bow`) and `crossbow` (`/equip crossbow`), primary-slot weapons with stand-in models drawn in code (`ArcheryBuilder`: a recurve bow with a string, an arrow with a steel head and three vanes, a crossbow with a stock, a rail, a bow across the front and the bolt on the rail while loaded) |
| The bow | `BowWeapon`, held in the **left** hand. Right-click aims (the gun's aim camera; `PlayerInputReader` treats it as a gun, through `IDrawnWeapon`). Left-click held draws: full in **1 s** (`ArcheryRules.FullDrawSeconds`); let go, it looses. A tap is the least draw (15%). Arrows are not counted (yet) |
| The arrow | `ArrowProjectile`. **A hitscan over its flat range, then a falling projectile**: the hit is decided at once from the camera (what the crosshair is on, as a gun's), the arrow is then *seen* crossing that range from the bow's nock at 180 m/s and sticks where it hit; past the flat range it carries on as a real projectile under gravity, swept each frame, hurting what it lands in. Flat range 12 m at a tap to **60 m at full draw**; damage 18 to **55** (an AK round is 34); it leaves the flat range at 20 to 55 m/s. Stuck arrows stay 20 s in the world, 8 s in someone |
| The crossbow | a gun (`PistolWeapon`, `FirearmProfile.Crossbow`): aimed and held as a rifle, one bolt, fired at once at full power -- **90 damage over an 80 m flat range**, falling past it at 65 m/s -- and then it **spans itself for 2.5 s** (`ReloadsAfterEveryShot`); no casing, no flash. Its shot is an arrow (`ArrowFallSpeed` on the profile makes the gun's hitscan show an arrow instead of a tracer) |
| The draw animation | (owner: "make the drawback animations with the string on the bow, maybe research red dead as a guide") Red Dead's archer, keyed over the draw in `PlayerLocomotion.Bow.cs` with two-bone IK: aimed, the bow arm goes straight out along the aim with the bow upright in the fist and the string hand rests at the nock behind the grip; the first 30% of the draw is the **nock** -- the string hand reaches over the right shoulder to the quiver, the arrow appears in it, and it brings the arrow to the string -- and the rest the **pull**, the hand drawn back along the aim and up to the cheek, the elbow high and out, the string and the arrow following the hand (`BowWeapon.LateUpdate`). On the loose the string springs back and shivers for a fifth of a second and the hand flicks back off it. Lowered, the bow hangs upright at the side. The same on other players' copies (the draw is told ten times a second). A Blender clip can replace the keys later (no quiver object on the back yet) |
| The spanning animation | (owner: "make a reload animation for the crossbow, maybe look at chivalry") Chivalry's, keyed over the crossbow's 2.5 s reload timer in `CrossbowSpan`: the crossbow comes off the shoulder to the low carry (`PlayerLocomotion.RifleSpanning`), dips and tips nose-down in the hand; the left hand leaves the fore-stock for the string at the bow (0-18%), hauls it back along the rail to the latch with the string's middle following the hand (18-55%), reaches over the shoulder for a bolt and lays it on the rail (55-82%; the bolt appears when the span ends), and goes back to the fore-stock as the crossbow comes up (82-100%); the ratchet of clicks and the latch's snap play with it. Chivalry plants the crossbow with a foot; ours stays in the hands (no foot pose). The crossbow otherwise uses the rifle pose |
| Sounds | made in code (`ArcherySounds`): the string's twang (the crossbow's lower, with the latch's clack), the creak of a draw, a thud in a body or a knock in the world, the crossbow spanned |
| Other players | `Archery` on the body (`IReplicatedAbility`): the draw (the aim and how far, ten times a second), the loose (their copy looses a cosmetic arrow the same way; the archer's machine decided the hit) and the bow lowered. The crossbow's shots reach copies as every gun's do, its bolt shown the same way |

Not yet: a quiver on the back (and ammo for the bow), a foot on the crossbow's stirrup, headshots, the owner's models, Blender-made clips in place of the keyed poses.

## Tests and pictures

`ArcheryTests` (the draw buys range, damage and speed up to full; a bolt hits harder and further and reloads longer; the crossbow's profile),
`ArcheryPlayTests` (a tap is a weak lob and a full draw hits harder and further; past its flat range the arrow falls and lands in the ground;
the arms come up at the aim and the string hand draws to the cheek; the crossbow fires at once at full power then spans for 2.5 s; a copy draws
with the archer and looses an arrow of its own), `ArcheryCapture` (`Logs/CityShots/archery-*.png`).
