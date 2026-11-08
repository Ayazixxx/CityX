# Enemies fighting together

Owner, 2026-10-24: "make mobs that are hostile to you work together if they are both attacking you at once. like they are aware of
eachothers existance and dont just both push you at the same time completely ignoring eachother and hitting eachother with their attacks
meant for you. research how multiple enemies attack players in games like shadow of war and i guess gta and minecraft." Then: "not that
there wont be any mobs that are hostile to eachother and you but we will work that out later for now just do enemies that are only hostile
to you".

## What the research says

- **Attack tokens** (DOOM 2016's GDC talk, The Last of Us Part II, and the Unreal write-ups built on them): each target hands out a few
  turns. An enemy attacks only while it holds one, claimed before its wind-up and given back when the attack ends. Typical budgets are one
  melee attacker, two shooters and one heavy at a time, because a player can follow about one incoming blow at a time. The rest keep up
  the pressure without attacking (strafing, closing to a waiting distance, taking cover) instead of standing idle.
- **Engagement rings**: the waiting enemies hold set distances (close for the one attacking, mid for the waiting melee, far for the
  shooters) and spread round the target instead of stacking on one side.
- **Shadow of Mordor and Shadow of War**: crowds of orcs where mostly one, now and then two, attack at once. Players read and counter one
  blow at a time.
- **GTA police (and the tactical mods built on them)**: they take turns to advance, flank and hold fire instead of rushing all at once.
- **Minecraft**: a skeleton's stray arrow that hits another mob starts a fight between them (infighting). Here the enemies are on one side
  for now, so their blows on each other do nothing.

## What it does

- **Turns** (`Character/NPC/AttackTurns.cs`, pure): each player has one melee turn and two ranged turns. The swordsman and the Crimson Claw
  share the melee one, so only one of them swings at you at a time; up to two Shootas fire at you at once. Turns go in the order they were
  asked for. A turn is given back when the attack is over, and its holder then rests for 1 s if someone else is waiting (a lone enemy
  never waits). A turn held for 4 s is taken back, and the turn of anyone who stops asking (dead, gone, lost you) is freed after 0.5 s.
- **Waiting**: a swordsman waiting his turn circles at 5.2 m instead of 3.4, still guarding and dashing, and never swings or punishes. A
  waiting Crimson Claw hovers at 5.5 m, drifting round. A Shoota waiting his turn keeps aiming and doesn't fire.
- **Spreading out** (`EnemyPack`): each keeps 1.8 m from the others (a step away when closer) and circles away from its nearest packmate,
  so two end up on opposite sides of you.
- **A clear line**: none of them swings or shoots while a packmate stands across the line to you. A Shoota steps sideways to clear it.
- **No friendly fire**: a blow from one enemy on another does nothing (`DamageContext.Strike` asks `EnemyPack.AreAllies`). This includes a
  swing a packmate stepped into and a stray bullet.
- Alone, every enemy fights exactly as before.

Enemies hostile to each other (and to you) come later. The pack then needs sides instead of "every enemy".

Since 2026-11-08 the enemies fight not only players but any summon on their side (the Wraith Staff's Wraith: `Opponents`, docs/WRAITH_STAFF.md):
each goes for the nearest of them, and a turn (`AttackTurns`) is asked of whichever it fights.

## Tests

- `AttackTurnsTests` (EditMode): one swings and two shoot; the next in line gets a given-back turn while the giver rests; a lone attacker
  never waits; turns are forgotten or taken back; each target has its own.
- `DuelistMindTests`: waiting, he holds off even at an opening and circles further out, and steps in once given the turn; he circles away
  from his packmate.
- `EnemyPackPlayTests`: two real swordsmen against a player walking in at them for 25 s. Both get turns, they are never attacking at once,
  they stay more than 0.7 m apart and spread more than 60 degrees round the player (measured: 0 frames together, 2.25 m, 180 degrees). One
  swordsman's blow on the other does nothing. A packmate on the line is in the way, off to the side or behind is not.

## Known

- A swordsman doesn't step in on a player who just stands there: he circles at fighting distance and waits for you to come to him (as he
  did before this). Against a passive player, a pack of them circles.
