# Playing online with a friend (a 1v1 duel)

Written 2026-11-12 for the owner's first online test ("i want to 1v1 my friend in a duel"). How the game connects is in
`docs/MULTIPLAYER_ARCHITECTURE.md`; this page is the how-to.

## What each of you needs

- **The same build of the game.** Both players must run the exact same version: a hit is sent as numbers both sides must read the same
  way. Make it with `CityX > Build Game To Share` in the editor (or headless: `Unity.exe -batchmode -quit -projectPath CityX -executeMethod
  CityX.ViewEditor.ShareBuild.BuildGameToShare`, about 20 minutes), zip the `CityX/Builds/Play` folder and send the whole zip. Unzip it
  anywhere and run `CityX.exe`. The host plays the same zip, not the editor, so both run the same version.
- **One of you hosts, the other joins.** The host's computer runs the world; the joiner types the host's address.

## Connecting

The game connects straight from the joiner to the host on **port 7770 (UDP)**. There is no server in between, so the joiner must be able to
reach the host's computer. Three ways, easiest first:

1. **Same house / same Wi-Fi:** the host's address is their computer's local address (shown on the Host screen, usually `192.168.x.x`).
2. **Different houses, with a free gaming VPN (recommended):** both install the same one, for example Radmin VPN, ZeroTier or Tailscale, and
   join the same network in it. The host's address is then the VPN one (Radmin's starts with `26.`, ZeroTier's and Tailscale's are shown in
   their apps; the Host screen lists it too). Nothing to change on the router.
3. **Different houses, without a VPN:** the host forwards **UDP port 7770** on their router to their computer, and gives the joiner their
   public address (search "what is my IP" on the host's computer). Routers differ; this is the fiddliest way.

The first time the host presses Host, **Windows asks whether to let CityX through the firewall: allow it** (tick both private and public
networks). If that was refused, allow `CityX.exe` again in Windows Security > Firewall > Allow an app.

## On the title screen

- Host: Multiplayer > Host (leave the port blank for 7770). The Host screen shows this computer's addresses to read out.
- Join: Multiplayer > Join, type the host's address (`26.12.34.56` or `26.12.34.56:7770`), Join.
- You appear next to each other at the spawn.
- In a session Escape opens the menu without stopping the game (nobody can pause a world someone else is in): your character stands
  where it is and can be hit. The game also keeps running if you click on another window.
- Characters are kept by the host's computer (docs/SERVER_SAVING.md): a friend picks or makes one on the host's list after joining, and
  the ones on their own title screen are their own computer's.

## A two-minute check before the friend joins

On the host's own computer, open `CityX.exe` twice (windowed). In the first, Host. In the second, Join `127.0.0.1`. If the two characters see
each other, the build and the firewall on this computer are fine, and only the connection between the two houses is left.

## If it does not connect

- "Couldn't connect": the address is wrong, the host is not hosting yet, the firewall blocked it, or (way 3) the port is not forwarded.
- Both on the VPN: check both show as connected in the VPN app and that the joiner can see the host there.
- Different versions of the game: send the same zip again to both.
