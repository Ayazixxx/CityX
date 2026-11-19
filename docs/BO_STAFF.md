# The bo staff (2026-11-19)

Owner, with a short clip of a staff fighter from a video game: "heres a pretty good video ... you should try to make copy these movements
as best you can. make the model just a basic metal pole nothing special. make it about the length of the spikesword or however long you
think it should be based on the video", then "the first swing in that video that is starts mid swing with is questionable maybe that will be
the heavy hold click attack but the rest looks pretty good". Earlier (2026-11-12) the owner wanted a bo staff swung "like robin": melee only,
never thrown.

`/equip bostaff` (also `bo`, `pole`, `metalpole`). Common (my placement).

## What the clip shows

The clip (7.3 s, split into frames and read frame by frame) is a fighter with a long pole, about as tall as they are:

- they grip it **near one end** for the big blows, so the long end reaches (the thrust at 6 s and the chops);
- **a chop**: the pole raised up on the right and brought down across the front (1 s);
- **a low sweep**: sunk into a deep stance, the pole flat round the front at the shins (1.3 s); here a backhand, left to right, so it starts
  where the chop ends (crossing the front between blows put the left arm through the chest);
- **the lunging thrust, a stab**: a long stride, both arms forward, the pole straight out at the chest (6 s; lower versions at 1.6 and 5.5 s);
- **a rising blow**: wound low on the left, the pole swept up past the right shoulder, the body rising behind it (1.8 s, 5.7 s);
- **the opening spin** (the owner's heavy): the fighter turning right round with the pole out flat, knocking the enemy back.

## How it plays

| | |
|---|---|
| Left-click | The chain of four: the **chop**, the **low sweep** (at the shins), the **lunging thrust**, the **rising blow** (the finisher: it knocks down) |
| Left-click held | The **spin**: wound right round to the right and held a third of a second (the tell), then flat round the front with the whole body; it breaks guards and knocks down |
| Left-click at a sprint | The thrust on the run |
| Right-click | The block: the pole held across the front in both hands (a staff's guard does not turn bullets, as a sword's does) |
| Reach | 1.25 times a sword's (`StaffRules.ReachShare`) |
| Knocked down | The rising blow and the spin throw whoever they land on 6 m/s away and 3 m/s up, limp (`StaffRules.Knockdown*`) |

All the numbers are mine (`SoulsSwordCombat.StaffLight1`..., `StaffRules`).

## How it is held

- A **plain steel pole** with a dark rubber cap on each end (`BoStaffBuilder`), 2.2 m on this 2.6 m body: the clip's pole is as long as its
  fighter is tall, and the spike sword is about as long.
- **Swung near one end, both fists on it like a bat** (the two-handed blades' hold): 1.85 m of pole past the right fist, 0.15 m past the
  left. Held in the middle, as a bo is in a kata, the short end went through the body on every swing.
- **Carried upright in both hands** before the right shoulder, the long end standing up past the head (`SwordStrikes.StaffCarry`). Carried over
  the shoulder in one hand, the left arm crossed the chest to take the pole for every attack, and the pole swung through the head going back up
  after the high blows (`SwordArmsClearPlayTests` measured both).

## The pieces

| Piece | What |
|---|---|
| `SwordStrikeKind.StaffChop/Sweep/Thrust/Rising/Spin/Run` | the attacks; their keys (where the hands and the pole go) in `SwordStrikes.Staff.cs` |
| `SourceArt/SwordMoves/sword_moves_pose.py` | their full-body clips (legs, hips, torso, head), in `SwordMotion.json` with the swords' |
| `SoulsSwordCombat` | the chain of four, the heavy, the running opener, the reach, the knock-down (`ISwordBlade.IsStaff`, `SoulsAttack.KnocksDown`) |
| `SwordWeapon.Staff`, `BoStaffBuilder` | the weapon and its model |
| `SwordPoseDriver` | the carry and the block |

## Other players

It is a `SwordWeapon` (`ICosmeticMountable`, in `NetworkedPlayerWiring`): seen in both hands, its attacks announced by
name as a sword's are; a knock-down is sent to the player it lands on, as every throw is.

## Not done

- **Twirls**: the clip's fighter spins the pole through the hands between blows. Here the pole stays in the fists; the spins are the body's.
- The model is a stand-in (the owner's "basic metal pole").
- Enemies do not use it; balance is untested by play.

## Tests

`BoStaffTests` (the six attacks and their clips, the thrust's straight line, the pole's far end off the ground and the sweep at the shins, the
rising blow going up and the spin going round, the built pole), `BoStaffPlayTests` (on the real body: carried upright,
swung with both hands near the end past a sword's reach; the chain's order and the spin; the spin throwing a player down; another player's copy),
`SwordArmsClearPlayTests` (every frame of every staff attack: the arms and the pole out of the body), and the swords' `SwordStrikesTests` and
`SwordMotionTests`, which cover the staff's keys and clips. Pictures: `SwordStrikeCapture.RenderTheBoStaffAttacks` (Explicit, -Graphics).
