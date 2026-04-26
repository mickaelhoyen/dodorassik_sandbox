extends Node
## Minimal SignalR JSON WebSocket client for CompetitiveHuntHub.
##
## Protocol flow:
##   1. POST /hubs/competitive/negotiate?negotiateVersion=1  → connectionId
##   2. WebSocket → ws://host/hubs/competitive?id=<cid>&access_token=<jwt>
##   3. WS STATE_OPEN  → client sends: {"protocol":"json","version":1}\x1e
##   4. Server acks:    {}\x1e  (type absent = handshake ack)
##   5. Client sends:   {"type":1,"target":"JoinHunt","arguments":["<huntId>"]}\x1e
##   6. Server pushes:  {"type":1,"target":"LeaderboardUpdated","arguments":[{...}]}\x1e
##
## Signals: leaderboard_updated(data), hub_connected(), hub_disconnected(reason)

signal leaderboard_updated(data: Dictionary)
signal hub_connected()
signal hub_disconnected(reason: String)

const RS: int = 0x1e  # ASCII record separator — SignalR message delimiter.

var _ws: WebSocketPeer = null
## idle | connecting | handshaking | awaiting_ack | joined | closing
var _state: String = "idle"
var _hunt_id: String = ""


func connect_to_hunt(hunt_id: String) -> void:
	if _state != "idle":
		disconnect_hub()
	_hunt_id = hunt_id
	_state = "connecting"
	_negotiate_and_connect()


func disconnect_hub() -> void:
	if _ws != null:
		_ws.close()
		_ws = null
	var was_active := _state != "idle"
	_state = "idle"
	_hunt_id = ""
	if was_active:
		hub_disconnected.emit("manual")


func _process(_delta: float) -> void:
	if _ws == null or _state == "idle" or _state == "connecting":
		return
	_ws.poll()
	match _ws.get_ready_state():
		WebSocketPeer.STATE_OPEN:
			if _state == "handshaking":
				_state = "awaiting_ack"
				_send('{"protocol":"json","version":1}')
			while _ws.get_available_packet_count() > 0:
				_handle_raw(_ws.get_packet().get_string_from_utf8())
		WebSocketPeer.STATE_CLOSED:
			_ws = null
			var prev := _state
			_state = "idle"
			if prev != "connecting":
				hub_disconnected.emit("closed")


func _negotiate_and_connect() -> void:
	if not AppState.is_authenticated():
		_state = "idle"
		hub_disconnected.emit("not_authenticated")
		return

	var http := HTTPRequest.new()
	add_child(http)
	http.request(
		ApiClient.base_url + "/hubs/competitive/negotiate?negotiateVersion=1",
		["Content-Type: application/json", "Authorization: Bearer %s" % AppState.auth_token],
		HTTPClient.METHOD_POST, ""
	)
	var result: Array = await http.request_completed
	http.queue_free()

	if int(result[1]) != 200:
		push_warning("CompetitiveHubClient: negotiate returned %d" % int(result[1]))
		_state = "idle"
		hub_disconnected.emit("negotiate_failed_%d" % int(result[1]))
		return

	var body: Variant = JSON.parse_string(result[3].get_string_from_utf8())
	if typeof(body) != TYPE_DICTIONARY:
		_state = "idle"
		hub_disconnected.emit("negotiate_parse_error")
		return

	var cid: String = String(body.get("connectionId", ""))
	if cid.is_empty():
		_state = "idle"
		hub_disconnected.emit("no_connection_id")
		return

	var ws_url: String = "%s/hubs/competitive?id=%s&access_token=%s" % [
		ApiClient.base_url.replace("https://", "wss://").replace("http://", "ws://"),
		cid.uri_encode(),
		AppState.auth_token,
	]

	_ws = WebSocketPeer.new()
	if _ws.connect_to_url(ws_url) != OK:
		_ws = null
		_state = "idle"
		hub_disconnected.emit("ws_connect_error")
		return

	_state = "handshaking"
	hub_connected.emit()


func _handle_raw(raw: String) -> void:
	for part: String in raw.split(char(RS)):
		if part.is_empty():
			continue
		var msg: Variant = JSON.parse_string(part)
		if typeof(msg) != TYPE_DICTIONARY:
			continue
		match _state:
			"awaiting_ack":
				# Handshake ack has no "type" field. Error ack has "error".
				if msg.has("error"):
					push_error("CompetitiveHubClient: handshake error: %s" % msg["error"])
					disconnect_hub()
				else:
					_state = "joined"
					_send(JSON.stringify({
						"type": 1, "target": "JoinHunt", "arguments": [_hunt_id],
					}))
			"joined":
				_dispatch(msg)


func _dispatch(msg: Dictionary) -> void:
	match int(msg.get("type", 0)):
		1: # Server→Client invocation
			var target: String = String(msg.get("target", ""))
			var args: Array = msg.get("arguments", [])
			if target == "LeaderboardUpdated" and args.size() > 0 and typeof(args[0]) == TYPE_DICTIONARY:
				leaderboard_updated.emit(args[0])
		6: # Ping → Pong
			_send('{"type":6}')


func _send(json: String) -> void:
	if _ws != null and _ws.get_ready_state() == WebSocketPeer.STATE_OPEN:
		_ws.send_text(json + char(RS))
