extends "res://scripts/ui/base_screen.gd"
## Leaderboard — competitive (SignalR push) or relaxed (10 s polling).
## Route args: { "hunt": <hunt_dict> }

const POLL_INTERVAL_SEC := 10.0

var _hunt: Dictionary
var _rankings_container: VBoxContainer
var _last_updated_label: Label
var _poll_timer: float = 0.0
var _use_signalr: bool = false


func build() -> void:
	_hunt = route_args.get("hunt", AppState.active_hunt)
	if _hunt.is_empty():
		add_title(tr("LB_TITLE"))
		add_button(tr("BTN_BACK"), func() -> void: Router.back())
		return

	var is_competitive: bool = String(_hunt.get("mode", "")) == "competitive"
	add_title(tr("LB_TITLE"))
	add_subtitle(String(_hunt.get("name", "")))

	_rankings_container = VBoxContainer.new()
	_rankings_container.add_theme_constant_override("separation", 4)
	add_node(_rankings_container)

	_last_updated_label = Label.new()
	_last_updated_label.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
	add_node(_last_updated_label)

	add_separator()
	add_button(tr("LB_REFRESH_BTN"), func() -> void: _fetch_leaderboard())
	add_button(tr("BTN_BACK"), func() -> void:
		_cleanup_signalr()
		Router.back())

	if is_competitive and AppState.is_authenticated():
		_use_signalr = true
		CompetitiveHubClient.leaderboard_updated.connect(_on_leaderboard_updated)
		CompetitiveHubClient.connect_to_hunt(String(_hunt.get("id", "")))

	_fetch_leaderboard()


func _process(delta: float) -> void:
	if _use_signalr:
		return
	_poll_timer += delta
	if _poll_timer >= POLL_INTERVAL_SEC:
		_poll_timer = 0.0
		_fetch_leaderboard()


func _fetch_leaderboard() -> void:
	var hunt_id: String = String(_hunt.get("id", ""))
	var resp: Dictionary = await ApiClient.get_leaderboard(hunt_id)
	if resp["ok"] and typeof(resp.get("data")) == TYPE_DICTIONARY:
		_render_leaderboard(resp["data"])


func _on_leaderboard_updated(data: Dictionary) -> void:
	_render_leaderboard(data)


func _render_leaderboard(data: Dictionary) -> void:
	for child in _rankings_container.get_children():
		child.queue_free()

	var rankings: Array = data.get("rankings", [])
	if rankings.is_empty():
		var empty := Label.new()
		empty.text = tr("LB_EMPTY")
		empty.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_rankings_container.add_child(empty)
	else:
		for entry in rankings:
			_rankings_container.add_child(_build_entry_row(entry))

	_last_updated_label.text = tr("LB_UPDATED") % Time.get_datetime_string_from_system()


func _build_entry_row(entry: Dictionary) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)

	var rank_lbl := Label.new()
	rank_lbl.text = _rank_medal(int(entry.get("rank", 0)))
	rank_lbl.custom_minimum_size = Vector2(32, 0)
	row.add_child(rank_lbl)

	var name_lbl := Label.new()
	name_lbl.text = String(entry.get("teamName", entry.get("familyName", "?")))
	name_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_lbl.autowrap_mode = TextServer.AUTOWRAP_OFF
	row.add_child(name_lbl)

	var steps_lbl := Label.new()
	steps_lbl.text = "%d/%d" % [int(entry.get("stepsCompleted", 0)), int(entry.get("totalSteps", 0))]
	steps_lbl.custom_minimum_size = Vector2(40, 0)
	steps_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	row.add_child(steps_lbl)

	var pts_lbl := Label.new()
	pts_lbl.text = "%d pts" % int(entry.get("totalPoints", 0))
	pts_lbl.custom_minimum_size = Vector2(60, 0)
	pts_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	row.add_child(pts_lbl)

	var duration: Variant = entry.get("duration")
	if duration != null and typeof(duration) == TYPE_STRING and duration != "":
		var dur_lbl := Label.new()
		dur_lbl.text = _format_duration(duration)
		dur_lbl.add_theme_color_override("font_color", Color(0.5, 0.5, 0.5))
		dur_lbl.custom_minimum_size = Vector2(64, 0)
		dur_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		row.add_child(dur_lbl)

	return row


func _rank_medal(rank: int) -> String:
	match rank:
		1: return "🥇"
		2: return "🥈"
		3: return "🥉"
		_: return "#%d" % rank


func _format_duration(iso: String) -> String:
	var parts: PackedStringArray = iso.split(":")
	if parts.size() >= 3:
		var h: int = int(parts[0])
		var m: int = int(parts[1])
		var s: int = int(float(parts[2]))
		if h > 0:
			return "%dh%02dm%02ds" % [h, m, s]
		return "%dm%02ds" % [m, s]
	return iso


func _cleanup_signalr() -> void:
	if _use_signalr:
		if CompetitiveHubClient.leaderboard_updated.is_connected(_on_leaderboard_updated):
			CompetitiveHubClient.leaderboard_updated.disconnect(_on_leaderboard_updated)
		CompetitiveHubClient.disconnect_hub()
	_use_signalr = false
