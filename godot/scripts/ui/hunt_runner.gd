extends "res://scripts/ui/base_screen.gd"
## Runs a hunt step by step. Validation strategies depend on the step type:
## - manual:      adult ticks the box
## - location:    DeviceServices.request_location() within radius
## - photo:       DeviceServices.capture_photo() (kept locally; uploaded later)
## - bluetooth:   DeviceServices.scan_bluetooth() looks for a known address
## - text_answer: case-insensitive compare with `params.expected`
## - clue_collect: kid types the code printed on a physical clue card
##
## When offline, the result is queued via OfflineCache and replayed once we
## reconnect.

var _hunt: Dictionary
var _step_index: int = 0
var _content: VBoxContainer


func build() -> void:
	_hunt = route_args.get("hunt", AppState.active_hunt)
	if _hunt.is_empty():
		add_title("Pas de chasse active")
		add_button("Retour", func() -> void: Router.go("player_home"))
		return

	add_title(String(_hunt.get("name", "Chasse")))
	_content = VBoxContainer.new()
	_content.add_theme_constant_override("separation", 8)
	add_node(_content)

	_render_step()
	add_separator()
	add_button("Quitter", func() -> void: Router.go("player_home"))


func _render_step() -> void:
	for child in _content.get_children():
		child.queue_free()

	var steps: Array = _hunt.get("steps", [])
	if _step_index >= steps.size():
		var done := Label.new()
		done.text = "🎉 Bravo ! Toutes les étapes sont terminées."
		done.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		done.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		_content.add_child(done)
		return

	var step: Dictionary = steps[_step_index]
	var title := Label.new()
	title.text = "Étape %d/%d — %s" % [_step_index + 1, steps.size(), String(step.get("title", ""))]
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
	var type := String(step.get("type", "manual"))
	match type:
		"manual":           return _manual_validator(step)
		"location":         return _location_validator(step)
		"photo":            return _photo_validator(step)
		"bluetooth":        return _bluetooth_validator(step)
		"text_answer":      return _text_validator(step)
		"clue_collect":     return _text_validator(step)
		_:                  return _manual_validator(step)


func _manual_validator(step: Dictionary) -> Node:
	var btn := Button.new()
	btn.text = "✓ L'adulte valide"
	btn.pressed.connect(func() -> void: _complete_step(step, {"manual": true}))
	return btn


func _location_validator(step: Dictionary) -> Node:
	var btn := Button.new()
	btn.text = "📍 Vérifier ma position"
	btn.pressed.connect(func() -> void:
		var resp: Dictionary = await DeviceServices.request_location()
		if not resp["ok"]:
			set_status("GPS indisponible: %s" % resp.get("error", "?"), true)
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
			set_status("Encore %d m à parcourir." % int(d - radius), true))
	return btn


func _photo_validator(step: Dictionary) -> Node:
	var btn := Button.new()
	btn.text = "📷 Prendre une photo"
	btn.pressed.connect(func() -> void:
		var resp: Dictionary = await DeviceServices.capture_photo()
		if not resp["ok"]:
			set_status("Caméra indisponible: %s" % resp.get("error", "?"), true)
			return
		_complete_step(step, {"photo_path": resp.get("path", "")}))
	return btn


func _bluetooth_validator(step: Dictionary) -> Node:
	var btn := Button.new()
	btn.text = "🔵 Chercher la balise"
	btn.pressed.connect(func() -> void:
		var resp: Dictionary = await DeviceServices.scan_bluetooth()
		if not resp["ok"]:
			set_status("Bluetooth indisponible: %s" % resp.get("error", "?"), true)
			return
		_complete_step(step, {"bluetooth": resp.get("data", {})}))
	return btn


func _text_validator(step: Dictionary) -> Node:
	var box := VBoxContainer.new()
	var input := LineEdit.new()
	input.placeholder_text = "Ta réponse"
	box.add_child(input)
	var btn := Button.new()
	btn.text = "Valider"
	btn.pressed.connect(func() -> void:
		var expected := String(step.get("params", {}).get("expected", ""))
		if expected.is_empty() or input.text.strip_edges().to_lower() == expected.strip_edges().to_lower():
			_complete_step(step, {"answer": input.text})
		else:
			set_status("Pas tout à fait, réessayez.", true))
	box.add_child(btn)
	return box


func _complete_step(step: Dictionary, payload: Dictionary) -> void:
	var hunt_id := String(_hunt.get("id", ""))
	var step_id := String(step.get("id", ""))
	if AppState.online and not hunt_id.is_empty():
		var resp: Dictionary = await ApiClient.submit_step(hunt_id, step_id, payload)
		if not resp["ok"]:
			OfflineCache.queue_submission(hunt_id, step_id, payload)
			set_status("Hors ligne — résultat mis en file d'attente.")
	else:
		OfflineCache.queue_submission(hunt_id, step_id, payload)
	_step_index += 1
	_render_step()
