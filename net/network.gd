extends Node
## Autoload ("Net"): thin wrapper around Godot's high-level multiplayer API.
##
## This is deliberately transport-only right now (plain ENet, LAN/direct-IP)
## with no lobby UI wired up yet -- the title screen's Play button still
## goes straight to a local single-player session. The point of adding
## this now is architectural: once a lobby UI exists, it calls host_game/
## join_game here, and Player (see player.gd) and the character texture
## sync (see Character.get_atlas_png/apply_atlas_png) are already built to
## work under an active multiplayer session instead of needing a rewrite.
## Swapping the transport to Steam's relay later (via GodotSteam) would
## only change what's inside host_game/join_game, not any calling code.

signal peer_connected(id: int)
signal peer_disconnected(id: int)
signal server_created
signal connected_to_server
signal connection_failed
signal server_disconnected

const DEFAULT_PORT := 7777
const MAX_PLAYERS := 8

func _ready() -> void:
	multiplayer.peer_connected.connect(_on_peer_connected)
	multiplayer.peer_disconnected.connect(_on_peer_disconnected)
	multiplayer.connected_to_server.connect(func(): connected_to_server.emit())
	multiplayer.connection_failed.connect(func(): connection_failed.emit())
	multiplayer.server_disconnected.connect(func(): server_disconnected.emit())

func host_game(port: int = DEFAULT_PORT) -> Error:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_server(port, MAX_PLAYERS)
	if err != OK:
		return err
	multiplayer.multiplayer_peer = peer
	server_created.emit()
	return OK

func join_game(address: String, port: int = DEFAULT_PORT) -> Error:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_client(address, port)
	if err != OK:
		return err
	multiplayer.multiplayer_peer = peer
	return OK

func disconnect_game() -> void:
	if multiplayer.multiplayer_peer != null:
		multiplayer.multiplayer_peer.close()
		multiplayer.multiplayer_peer = null

## multiplayer_peer != null is NOT enough on its own -- Godot 4.3+ defaults
## every SceneTree's own multiplayer API to a real (non-null)
## OfflineMultiplayerPeer instance whenever nothing else has been assigned,
## specifically so is_multiplayer_authority()/rpc() calls stay well-behaved
## with no real session active. Checking only for null meant this returned
## true UNCONDITIONALLY, even with zero networking ever attempted --
## confirmed directly: main.gd's own singleplayer spawn path
## (_spawn_singleplayer, which is what spawns the CPU dummy and the
## pick-up-able greatsword) was being skipped in favor of the networked
## path on EVERY run, hosted or not, since is_networked() never actually
## returned false.
func is_networked() -> bool:
	return multiplayer.multiplayer_peer != null and not (multiplayer.multiplayer_peer is OfflineMultiplayerPeer)

func is_server() -> bool:
	return is_networked() and multiplayer.is_server()

func _on_peer_connected(id: int) -> void:
	peer_connected.emit(id)

func _on_peer_disconnected(id: int) -> void:
	peer_disconnected.emit(id)
