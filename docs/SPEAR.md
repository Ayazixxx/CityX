# The spear (2026-11-18)

Owner: "research video games with spears and try to make good spear animations/mechanics. i feel like it should be mostly thrusting stabs
but some swinging as well maybe the heavy attacks are swings, also should be usable with shields maybe not all". Earlier (2026-11-12) the
owner's idea for a spear was "melee you can left click + hold right click to aim and throw": **the throw is not built yet** (see Not done).

`/equip spear`. Common (my placement). A stand-in model drawn in code.

**Parked (owner, 2026-11-19):** "I kind of dont like the spear it doesnt really even make sense in our games setting, leave it for now but im
unsure it will make it into the final game". It stays in as it is; nothing more is built for it (the throw included) unless the owner asks.

## What the research said (sources at the bottom)

- **Elden Ring**: a spear is the long-reach thrusting weapon; its mark is that it can attack from behind a raised shield (the "guard
  poke"), slower and a little weaker than a normal thrust; its charged heavy is a long lunge that goes through a guard.
- **Nioh**: the same weapon does both jobs by stance: quick pokes (low), wide sweeps for groups (mid), the longest-reaching heavy blow
  (high). Thrusts for one target, sweeps for several.
- **For Honor's Valkyrie** (spear and shield): light thrusts, and a low spear sweep as the centrepiece that ends her chains.
- **Real two-handed spear** (Academie Duello's "Attacks of the Spear"): the rear hand near the butt drives; the lead hand a quarter to a
  third of the way up only GUIDES, and for a long strike the shaft is pushed THROUGH it until the hands meet. The power is the rear arm
  and the body's weight going forward, not a swing of the shoulders.

What was taken from that: thrusts as the lights and a sweep as the heavy (the owner's own shape, and Nioh's and the Valkyrie's); the
longest reach, each thrust quicker and lighter than a cut; a place beside a shield; and the front hand that slides.

## How it plays

| | |
|---|---|
| Left-click | The chain: **jab** (lands 0.33 s after the click, 85% of the spear's 12 damage, a 0.30 m step), **thrust** (on a higher line at the neck, 0.35 s, 95%, 0.40 m), **lunge** (the finisher: drawn right back and held a beat, 0.51 s, 210%, a 0.80 m stride, the body stretched low; a long recovery that cannot be cancelled) |
| Left-click held | The heavy: **the sweep**, a backhand from the left round the front to the right, held wound up 0.32 s, lands at 0.85 s, 170%, guard-breaking as every heavy, and it catches everyone in front |
| Left-click at a sprint | The running lunge |
| Right-click | With no shield, the guard: the shaft held across the front in both hands. With a shield, the shield's (raised) |
| Reach | 1.35 times a sword's (`SpearRules.ReachShare`): 2.97 m ahead |
| Stagger | A thrust staggers for less than a cut (0.38 and 0.42 s against a sword's 0.52) |

All the numbers are mine (`SoulsSwordCombat.SpearLight1`..., `SpearRules`).

## How it is held and moved

- **Carried in the right hand alone**, low at the hip, the point ahead and a little up, the left arm the walk's own (`SwordStrikes.SpearCarry`).
  The walk is the ordinary one (neither sword's carry clips).
- **Side on to fight**: the left side to the enemy, the rear (right) hand low at the right hip, the shaft under that arm. The hand runs
  forward along the right side, never across the chest, so the butt passes beside the body. The shoulders stay turned to the right even as a
  thrust lands.
- **The front hand slides** (`PlayerLocomotion.SlideSupportHand`, `SpearRules.FrontHandFromShoulder`): for an attack or a guard the left hand
  takes the shaft, but it is not fixed to a point on it. It stays before its own shoulder and the shaft is pushed through it, between 0.24
  and 0.80 m up the shaft from the rear hand: on a jab the hands start most of a metre apart and end about a third of a metre apart.
- **Beside a shield** the left hand never goes to the shaft: the same thrusts with the right arm alone, from beside the shield. The round
  shield can be up while thrusting (it always could); the riot shield opens for half a second on a click (its own rule, docs/SHIELD.md). The
  spear counts as a one-handed weapon for the riot shield.
- **A thrust keeps its line** (`SwordFrame.HoldsItsLine`): under an attack's clip the game turns a sword arm with the clip's shoulders,
  which draws a blade further back and through; a point 1.75 m out it swung a foot to the side on its way to the target. A spear's
  thrusts keep the body's own axes, and their clips' turns are the keys' own.
- **The hold** (`SpearBuilder`, `SpearRules.GripTiltDegrees`): the shaft lies diagonally across the palm, leaned 35 degrees toward the
  fingertips, so the forearm comes in behind it at an angle. Gripped 0.75 m from the butt of a 2.4 m spear.

## The pieces

| Piece | What |
|---|---|
| `SwordStrikeKind.SpearJab/Thrust/Lunge/Sweep/Run` | the attacks' keys (`SwordStrikes`), names `spear_jab`..., timings and steps |
| `SourceArt/SwordMoves/sword_moves_pose.py` | their full-body clips (legs, hips, torso, head), in `SwordMotion.json` with the swords' |
| `SoulsSwordCombat` | the chain, the heavy, the running opener, the reach (`ISwordBlade.IsSpear`) |
| `SwordWeapon.Spear` | the weapon; `SpearBuilder` the stand-in (shaft, butt cap, flat leaf head; 2.4 m) |
| `SwordPoseDriver` | the spear's carry and guard |
| `PlayerLocomotion.SupportHand` | the sliding front hand, only in a two-handed pose and never with a shield |

## Other players

It is a `SwordWeapon` (`ICosmeticMountable`, in `NetworkedPlayerWiring`): seen in the hand, its attacks announced by name as a sword's are,
and a copy's left hand takes the shaft and slides the same way (the rule runs on every body). Damage as every blow.

## Not done

- **The throw** (the owner's idea of 2026-11-12: hold right-click to aim, click to throw, it sticks where it lands and you fight
  bare-handed until you pick it up). Right-click is the guard, or the shield's, for now.
- **No attack from behind the riot shield** without opening it (Elden Ring's guard poke): the riot shield's own rule says an attack opens it.
- The model is a stand-in. The spear is blunt for dismemberment (it cuts nothing off).
- Enemies do not use it. Balance is untested by play.
- The old spin attack (key 2) has not been looked at with a spear in hand.

## Tests

`SpearTests` (the names; the lights are thrusts: the point goes out along the shaft, on one straight line, and stops, the body side on;
the heavy sweeps level from left to right; quicker, lighter and longer than a sword; the clips; the built spear), `SpearPlayTests` (on the
real body: carried in one hand; a jab with both hands, the shaft sliding through the front one, the point straight and landing where a sword
cannot reach; beside a shield with the right hand alone; the sweep catching people either side and the chain's order; another player's
copy), `SwordArmsClearPlayTests` (every frame of every spear attack: the arms out of the body, the shaft out of it), and the swords' own
`SwordStrikesTests` and `SwordMotionTests`, which now cover the spear's keys and clips. Pictures: `SwordStrikeCapture.RenderTheSpearAttacks`
(Explicit, -Graphics): `Logs/CityShots/strike-spear-*.png` and `strike-spear-shield-*.png`.

## Sources

- [Spears -- Elden Ring Wiki](https://eldenring.wiki.gg/wiki/Spears), [Spear -- Fextralife](https://eldenring.wiki.fextralife.com/Spear), [Impaling Thrust](https://eldenring.wiki.gg/wiki/Skill:_Impaling_Thrust), [Spears in Nightreign -- Mobalytics](https://mobalytics.gg/elden-ring-nightreign/wiki/spears) (the poke from behind a shield, the charged lunge)
- [Nioh stances -- RPG Site](https://www.rpgsite.net/feature/5278-nioh-guide-how-to-use-stances-to-tear-apart-your-enemies), [Spear skills -- Fextralife](https://nioh.wiki.fextralife.com/Spear+Skills), [Nioh 2 stances -- Push Square](https://www.pushsquare.com/news/2020/03/guide_nioh_2_-_when_to_use_high_mid_and_low_stances)
- [Valkyrie -- For Honor Wiki mirror](https://breezewiki.discard.no/forhonor/wiki/Valkyrie), [How to play Valkyrie -- Gamepressure](https://www.gamepressure.com/forhonor/valkyrie/z796ce) (the spear sweep)
- [Attacks of the Spear -- Academie Duello](https://www.academieduello.com/blog/attacks-of-the-spear) (the shaft through the lead hand; where the hands sit)
