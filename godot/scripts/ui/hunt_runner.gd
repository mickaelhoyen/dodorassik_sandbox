extends "res://scripts/ui/base_screen.gd"
## Runs a hunt step by step. Validation strategies depend on the step type.
## In competitive mode, shows elapsed time, redirects to team_select if needed,
## and applies anti-cheat rejection messages.

var _hunt: Dictionary
var _step_index: int = 0
var _content: VBoxContainer
var _is_competitive: bool = false
var _start_time: float = 0.0
var _timer_label: Label = null


func build() -> void:
	_hunt = route_args.get("hunt", AppState.active_hunt)
	if _hunt.is_empty():
		add_title(tr("RUNNER_NO_HUNT"))
		add_button(tr("BTN_BACK"), func() -> void: Router.go("player_home"))
		return

	_is_competitive = String(_hunt.get("mode", "")) == "competitive"

	if _is_competitive and AppState.active_team.is_empty() and AppState.is_authenticated():
		Router.go("team_select", {"hunt": _hunt})
		return

	add_title(String(_hunt.get("name", "")))

	if _is_competitive:
		add_subtitle(tr("RUNNER_COMPETITIVE_SUBTITLE") % String(AppState.active_team.get("name", "?")))
		_timer_label = Label.new()
		_timer_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_timer_label.add_theme_font_size_override("font_size", 14)
		add_node(_timer_label)
		_start_time = Time.get_unix_time_from_system()

	_content = VBoxContainer.new()
	_content.add_theme_constant_override("separation", 8)
	add_node(_content)

	_render_step()
	add_separator()

	if _is_competitive:
		add_button(tr("RUNNER_LEADERBOARD_BTN"),
			func() -> void: Router.go("leaderboard", {"hunt": _hunt}))

	add_button(tr("RUNNER_QUIT_BTN"), func() -> void: Router.go("player_home"))


func _process(delta: float) -> void:
	if _timer_label == null:
		return
	var elapsed: float = Time.get_unix_time_from_system() - _start_time
	var minutes: int = int(elapsed) / 60
	var seconds: int = int(elapsed) % 60
	_timer_label.text = "⏱ %02d:%02d" % [minutes, seconds]


func _render_step() -> void:
	for child in _content.get_children():
		child.queue_free()

	var steps: Array = _hunt.get("steps", [])
	if _step_index >= steps.size():
		var done := Label.new()
		done.text = tr("RUNNER_DONE_COMPETITIVE") if _is_competitive else tr("RUNNER_DONE")
		done.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		done.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		_content.add_child(done)

		if _is_competitive:
			var lb_btn := Button.new()
			lb_btn.text = tr("RUNNER_LEADERBOARD_FINAL_BTN")
			lb_btn.pressed.connect(func() -> void: Router.go("leaderboard", {"hunt": _hunt}))
			_content.add_child(lb_btn)
		return

	var step: Dictionary = steps[_step_index]
	var title := Label.new()
	title.text = tr("RUNNER_TITLE_STEP") % [_step_index + 1, steps.size(), String(step.get("title", ""))]
	title.add_theme_font_size_override("font_size", 20)
	title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_content.add_child(title)

	var desc := Label.new()
	desc.text = String(step.get("description", ""))
	desc.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_content.add_child(desc)

	var validator := _build_validator(step)
	if validator != null:
		_content.add_child(validator)


func _build_validator(step: Dictionary) -> Node:
	match String(step.get("type", "manual")):
		"manual":       return _manual_validator(step)
		"location":     return _location_validator(step)
		"photo":        return _photo_validator(step)
		"bluetooth":    return _bluetooth_validator(step)
		"text_answer":  return _text_validator(step)
		"clue_collect": return _text_validator(step)
		_:              return _manual_validator(step)


func _manual_validator(step: Dictionary) -> Node:
	var btn := Button.new()
	btn.text = tr("RUNNER_MANUAL_BTN")
	btn.pressed.connect(func() -> void: _complete_step(step, {"manual": true}))
	return btn


func _location_validator(step: Dictionary) -> Node:
	var btn := Button.new()
	btn.text = tr("RUNNER_LOCATION_BTN")
	btn.pressed.connect(func() -> void:
		var resp: Dictionary = await DeviceServices.request_location()
		if not resp["ok"]:
			set_status(tr("RUNNER_GPS_UNAVAILABLE") % resp.get("error", "?"), true)
			return
		var loc: Dictionary = resp["data"]
		var params: Dictionary = step.get("params", {})
		var target_lat: float = float(params.get("lat", loc["lat"]))
		var target_lon: float = float(params.get("lon", loc["lon"]))
		var radius: float = float(params.get("radius_m", 30.0))
		var d: float = DeviceServices.haversine_meters(loc["lat"], loc["lon"], target_lat, target_lon)
		if d <= radius:
			_complete_step(step, {"lat": loc["lat"], "lon": loc["lon"], "distance_m": d})
		else:
			set_status(tr("RUNNER_GPS_DISTANCE") % int(d - radius), true))
	return btn


func _photo_validator(step: Dictionary) -> Node:
	var btn := Button.new()
	btn.text = tr("RUNNER_PHOTO_BTN")
	btn.pressed.connect(func() -> void:
		var resp: Dictionary = await DeviceServices.capture_photo()
		if not resp["ok"]:
			set_status(tr("RUNNER_CAMERA_UNAVAILABLE") % resp.get("error", "?"), true)
			return
		var data: Dictionary = resp.get("data", {})
		_complete_step(step, {"photo_size_bytes": int(data.get("size_bytes", 0)), "photo_taken": true}))
	return btn


func _bluetooth_validator(step: Dictionary) -> Node:
	var btn := Button.new()
	btn.text = tr("RUNNER_BLUETOOTH_BTN")
	btn.pressed.connect(func() -> void:
		var params: Dictionary = step.get("params", {})
		var raw: Variant = params.get("allowed_addresses", [])
		var whitelist := PackedStringArray()
		if typeof(raw) == TYPE_ARRAY:
			for v in raw:
				whitelist.append(String(v))
		if whitelist.is_empty():
			set_status(tr("RUNNER_BLUETOOTH_NO_WHITELIST"), true)
			return
		var resp: Dictionary = await DeviceServices.scan_bluetooth(whitelist)
		if not resp["ok"]:
			set_status(tr("RUNNER_BLUETOOTH_UNAVAILABLE") % resp.get("error", "?"), true)
			return
		_complete_step(step, {"bluetooth": resp.get("data", {})}))
	return btn


func _text_validator(step: Dictionary) -> Node:
	var box := VBoxContainer.new()
	var input := LineEdit.new()
	input.placeholder_text = tr("RUNNER_TEXT_PLACEHOLDER")
	box.add_child(input)
	var btn := Button.new()
	btn.text = tr("RUNNER_TEXT_VALIDATE_BTN")
	btn.pressed.connect(func() -> void:
		var expected := String(step.get("params", {}).get("expected", ""))
		if expected.is_empty() or input.text.strip_edges().to_lower() == expected.strip_edges().to_lower():
			_complete_step(step, {"answer": input.text})
		else:
			set_status(tr("RUNNER_TEXT_WRONG"), true))
	box.add_child(btn)
	return box


func _complete_step(step: Dictionary, payload: Dictionary) -> void:
	var hunt_id := String(_hunt.get("id", ""))
	var step_id := String(step.get("id", ""))
	var team_id := String(AppState.active_team.get("id", ""))

	if AppState.online and not hunt_id.is_empty():
		var resp: Dictionary = await ApiClient.submit_step(hunt_id, step_id, payload, team_id)
		if not resp["ok"]:
			var err: String = resp.get("error", "")
			match err:
				"step_order_violation": set_status(tr("RUNNER_ANTICHEAT_ORDER"), true); return
				"gps_speed_implausible": set_status(tr("RUNNER_ANTICHEAT_GPS"), true); return
			OfflineCache.queue_submission(hunt_id, step_id, payload)
			set_status(tr("RUNNER_OFFLINE_QUEUED"))
	else:
		OfflineCache.queue_submission(hunt_id, step_id, payload)
	_step_index += 1
	_render_step()
