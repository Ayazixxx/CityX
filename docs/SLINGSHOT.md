# The slingshot

Owner, 2026-10-30: "next make a slingshot weapon, the starter ranged non magic weapon. research the slingshot mechanics in legend of zelda as
well as bully. i guess mostly just use their animations for inspiration. make it work similarly to the bow and arrow where you hold to draw and
shoot at full power, make the draw quicker than the bow and just make it shoot a little pebble". `slingshot` (`/equip slingshot` or `sling`).

## What the games do (researched 2026-10-30)

| Game | The slingshot |
|---|---|
| Zelda (Ocarina of Time, Twilight Princess, Skyward Sword; [Zelda Dungeon](https://www.zeldadungeon.net/wiki/Slingshot), [Skyward Sword guide](https://www.gamerguides.com/the-legend-of-zelda-skyward-sword-hd/guide/gear-and-items/items/slingshot)) | the starter ranged weapon the bow later replaces: less reach, a stun more than a kill, seeds as ammo; aimed over the shoulder, the fork held out in one hand and the pouch pulled back to the face with the other |
| Bully ([GameFAQs guide](https://gamefaqs.gamespot.com/xbox360/942199-bully-scholarship-edition/faqs/77494/chapter-1-making-new-friends-and-enemies), [Bully wiki](https://bully.fandom.com/wiki/Slingshot)) | hold to pull back for a stronger shot, a full charge to the head the best; the Super Slingshot (with a scope) charges fully in about 1.5 s. Jimmy holds it out at arm's length and draws the pouch to his chin |

## What it is (`DrawnProfile.Slingshot`, `SlingshotRules`)

It is the bow (`BowWeapon` with `Kind` Slingshot), as the crossbow is a gun with its own profile: right-click aims, left-click held draws, let
go it shoots; a hitscan over a flat range that grows with the draw, the pebble then falling (`ArrowProjectile` with `Blunt`).

| | Bow | Slingshot |
|---|---|---|
| Full draw | 1 s | **0.5 s** |
| Damage, tap to full | 18 to 55 | **5 to 20** (Firearm Damage applies) |
| Flat range, tap to full | 12 to 60 m | **8 to 30 m** |
| Falling on at | 20 to 55 m/s | 15 to 35 m/s |
| Between shots | none (its nock is part of the draw) | **0.25 s** to fetch the next pebble into the pouch: tapping (about 18 a second) never beats drawing fully (about 27) |
| Seen flying | 180 m/s | 90 m/s, a small grey stone with a short faint streak |
| Where it lands | sticks | **drops off** what it hits and bounces and rolls (a loose physics stone) for 3 s; no blood (a pebble does not cut) |
| Fire Enhancement | the arrow burns | not taken: the enhancement waits for the next swing, bullet or arrow |

The numbers are mine. Pebbles are not counted (no ammo yet, as arrows).

## The hold (`PlayerLocomotion.Slingshot.cs`)

Bully's Jimmy's (the owner, 2026-11-01, with two Bully screenshots: "they actually put the slingshot in the left hand but angled it way better",
"hold it further away from his head unbend his arm when holding right click or scoping in"; a right-handed try and two square-on tries, the
fork behind the big head and the drawing arm through it, were dropped). Aimed (right-click), the torso turns 45 degrees side-on, the left shoulder
leading, and the head turns back to look along the aim. The left arm is straight (99.5% of its length: 97% still bent it 29 degrees), the fork
upright in the fist and leaning 30 degrees in toward the drawing hand. The pouch is held up by the right cheekbone, at an anchor just outside the
head (0.85, 0, 0.75 head radii to the right, up and forward of its middle), the right hand pinching it with the fingers toward the fork and the
palm to the cheek, the elbow low and back so the forearm rises to the face; the fork stands on the line from that anchor along the aim, as a
shooter lines it up with the eye, which also keeps it clear of the head from the camera over the right shoulder.

Aiming alone leaves the bands slack (owner, 2026-11-05: "when you hold right click the animation stretches the string instead of it doing it only when you hold click"): the pouch and pebble hang between the prongs, and the hand waits 45% of the way back from the fork (`SlingshotRestPull`), since the arm cannot reach the fork from the turned-back shoulder. Holding left click draws: the pouch is in the hand within the first quarter of the pull (`BowWeapon.PouchInHandByPull`), and both go back to the cheek. On the loose the bands snap back and shiver, as the bow's string does, and the hand flicks off
them. `SlingshotCapture` renders it from the side and from the game's own aiming camera (`slingshot-drawn-aim-view.png`: the player's view).

## The model (`ArcheryBuilder.BuildSlingshot`, `BuildPebble`)

A stand-in drawn in code until the owner's: a wooden fork (a leather-wrapped grip under the crotch, two prongs curving up and out) 29 cm tall,
rubber bands drawn as a line through a leather pouch (the line wide in its middle), and a lumpy grey pebble.

## Sounds (`ArcherySounds`)

The bands' snap (a low short twang with a slap of noise), the bow's creak while drawing, a pebble's clack on the world and a thud on a body.

## Other players (docs/MULTIPLAYER_STATE.md)

The slingshot is an equipped item (`ICosmeticMountable`): another player's copy holds it. The draw, the loose and the lowering are `Archery`'s
events (`IReplicatedAbility`) as the bow's are; a loose says which weapon (`B`, the `DrawnKind`), so the copy flings a pebble of its own
(`Archery.PebblePrefab`) that hurts nothing, the shooter's machine having decided the hit.

## Tests

`SlingshotRulesTests` (EditMode: quicker, lighter and shorter than the bow; tapping no quicker to kill with than drawing), `SlingshotPlayTests` (in
the left hand, fully drawn in half a second with the pouch back by the face and the pebble in it; a full draw hits 20 m off for its full damage
and the pebble drops off rather than sticking, gone after 3 s; a second shot waits for the next pebble; a copy draws with the shooter and flings
a pebble of its own), the replication guard tests. `SlingshotCapture` (Explicit, -Graphics) renders `Logs/CityShots/slingshot-*.png`.
