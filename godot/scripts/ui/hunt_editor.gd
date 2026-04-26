extends "res://scripts/ui/base_screen.gd"
## Full hunt editor. Handles:
##   - Hunt metadata (name, description, mode)
##   - Ordered steps with per-step editing (type, title, description, params)
##   - Physical clues (code, title, reveal text, points)
##
## A hunt without an "id" key is a creation (POST); one with an existing "id"
## triggers a full replace (PUT). Steps/clues carry their "id" when they come
## from the server so the backend can upsert them in place.

const STEP_TYPES := [
	"manual",
	"location",
	"photo",
	"bluetooth",
	"text_answer",
	"clue_collect",
]

func _step_type_labels() -> Dictionary:
	return {
		"manual":       tr("EDITOR_STEP_TYPE_MANUAL"),
		"location":     tr("EDITOR_STEP_TYPE_LOCATION"),
		"photo":        tr("EDITOR_STEP_TYPE_PHOTO"),
		"bluetooth":    tr("EDITOR_STEP_TYPE_BLUETOOTH"),
		"text_answer":  tr("EDITOR_STEP_TYPE_TEXT"),
		"clue_collect": tr("EDITOR_STEP_TYPE_CLUE"),
	}

var _hunt: Dictionary = {}

# UI references
var _name_edit: LineEdit
var _description_edit: TextEdit
var _mode_option: OptionButton
var _steps_box: VBoxContainer
var _clues_box: VBoxContainer

# In-memory working copies
var _steps: Array = []
var _clues: Array = []


func build() -> void:
	_hunt = route_args.get("hunt", {}).duplicate(true)
	_steps = _hunt.get("steps", []).duplicate(true) if _hunt.has("steps") else []
	_clues = _hunt.get("clues", []).duplicate(true) if _hunt.has("clues") else []

	if DeviceServices.has_map():
		DeviceServices.map_confirmed.connect(_on_map_confirmed)
		DeviceServices.map_cancelled.connect(_on_map_cancelled)

	add_title(tr("EDITOR_TITLE"))

	# ---- Metadata -----------------------------------------------------------
	_name_edit = LineEdit.new()
	_name_edit.placeholder_text = tr("EDITOR_NAME_PLACEHOLDER")
	_name_edit.text = String(_hunt.get("name", ""))
	add_node(_name_edit)

	_description_edit = TextEdit.new()
	_description_edit.placeholder_text = tr("EDITOR_DESC_PLACEHOLDER")
	_description_edit.text = String(_hunt.get("description", ""))
	_description_edit.custom_minimum_size = Vector2(0, 100)
	add_node(_description_edit)

	var mode_row := HBoxContainer.new()
	var mode_lbl := Label.new()
	mode_lbl.text = tr("EDITOR_MODE_LABEL")
	mode_lbl.custom_minimum_size = Vector2(80, 0)
	mode_row.add_child(mode_lbl)
	_mode_option = OptionButton.new()
	_mode_option.add_item(tr("EDITOR_MODE_RELAXED"), 0)
	_mode_option.add_item(tr("EDITOR_MODE_COMPETITIVE"), 1)
	_mode_option.selected = 1 if String(_hunt.get("mode", "relaxed")) == "competitive" else 0
	_mode_option.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mode_row.add_child(_mode_option)
	add_node(mode_row)

	add_separator()

	# ---- Steps section ------------------------------------------------------
	var steps_hdr := _make_section_header(tr("EDITOR_STEPS_SECTION"), func() -> void: _add_step())
	add_node(steps_hdr)

	# Library + map buttons row
	var tools_row := HBoxContainer.new()
	tools_row.add_theme_constant_override("separation", 8)

	var lib_btn := Button.new()
	lib_btn.text = tr("EDITOR_LIBRARY_BTN")
	lib_btn.pressed.connect(func() -> void:
		Router.go("step_library", {
			"on_use": func(tmpl: Dictionary) -> void: _inject_template(tmpl),
		}))
	tools_row.add_child(lib_btn)

	if DeviceServices.has_map():
		var map_btn := Button.new()
		map_btn.text = tr("MAP_EDITOR_OPEN_BTN")
		map_btn.pressed.connect(func() -> void: _open_map())
		tools_row.add_child(map_btn)

	add_node(tools_row)

	_steps_box = VBoxContainer.new()
	_steps_box.add_theme_constant_override("separation", 4)
	add_node(_steps_box)
	_render_steps()

	add_separator()

	# ---- Clues section ------------------------------------------------------
	var clues_hdr := _make_section_header(tr("EDITOR_CLUES_SECTION"), func() -> void: _add_clue())
	add_node(clues_hdr)

	_clues_box = VBoxContainer.new()
	_clues_box.add_theme_constant_override("separation", 4)
	add_node(_clues_box)
	_render_clues()

	add_separator()

	# ---- Actions ------------------------------------------------------------
	add_button(tr("EDITOR_SAVE_BTN"), func() -> void: _save())
	add_button(tr("BTN_BACK"), func() -> void: Router.go("creator_home"))


# ============================================================  Steps  ========

func _render_steps() -> void:
	for child in _steps_box.get_children():
		child.queue_free()

	for i in _steps.size():
		_steps_box.add_child(_make_step_row(i))


func _make_step_row(i: int) -> Control:
	var step: Dictionary = _steps[i]
	var card := PanelContainer.new()
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 4)
	card.add_child(vbox)

	# Header row: order label + type selector + move buttons + delete
	var hdr := HBoxContainer.new()
	var order_lbl := Label.new()
	order_lbl.text = "%d." % (i + 1)
	order_lbl.custom_minimum_size = Vector2(28, 0)
	hdr.add_child(order_lbl)

	var type_btn := OptionButton.new()
	var _labels := _step_type_labels()
	for t in STEP_TYPES:
		type_btn.add_item(_labels[t])
	type_btn.selected = STEP_TYPES.find(String(step.get("type", "manual")))
	type_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	type_btn.item_selected.connect(func(idx: int) -> void:
		_steps[i]["type"] = STEP_TYPES[idx]
		_render_steps())
	hdr.add_child(type_btn)

	var up_btn := Button.new()
	up_btn.text = "↑"
	up_btn.disabled = i == 0
	up_btn.pressed.connect(func() -> void:
		var tmp: Dictionary = _steps[i - 1]
		_steps[i - 1] = _steps[i]
		_steps[i] = tmp
		_render_steps())
	hdr.add_child(up_btn)

	var dn_btn := Button.new()
	dn_btn.text = "↓"
	dn_btn.disabled = i == _steps.size() - 1
	dn_btn.pressed.connect(func() -> void:
		var tmp: Dictionary = _steps[i + 1]
		_steps[i + 1] = _steps[i]
		_steps[i] = tmp
		_render_steps())
	hdr.add_child(dn_btn)

	var del_btn := Button.new()
	del_btn.text = "✕"
	del_btn.pressed.connect(func() -> void:
		_steps.remove_at(i)
		_render_steps())
	hdr.add_child(del_btn)
	vbox.add_child(hdr)

	# Title field
	var title_edit := LineEdit.new()
	title_edit.placeholder_text = tr("EDITOR_STEP_TITLE_PLACEHOLDER")
	title_edit.text = String(step.get("title", ""))
	title_edit.text_changed.connect(func(v: String) -> void: _steps[i]["title"] = v)
	vbox.add_child(title_edit)

	# Description field
	var desc_edit := TextEdit.new()
	desc_edit.placeholder_text = tr("EDITOR_STEP_DESC_PLACEHOLDER")
	desc_edit.text = String(step.get("description", ""))
	desc_edit.custom_minimum_size = Vector2(0, 60)
	desc_edit.text_changed.connect(func() -> void: _steps[i]["description"] = desc_edit.text)
	vbox.add_child(desc_edit)

	# Type-specific params
	var params_node: Node = _make_params_editor(i, step)
	if params_node != null:
		vbox.add_child(params_node)

	# Points
	var pts_row := HBoxContainer.new()
	var pts_lbl := Label.new()
	pts_lbl.text = tr("LBL_POINTS")
	pts_lbl.custom_minimum_size = Vector2(60, 0)
	pts_row.add_child(pts_lbl)
	var pts_spin := SpinBox.new()
	pts_spin.min_value = 0
	pts_spin.max_value = 1000
	pts_spin.value = int(step.get("points", 10))
	pts_spin.value_changed.connect(func(v: float) -> void: _steps[i]["points"] = int(v))
	pts_row.add_child(pts_spin)
	vbox.add_child(pts_row)

	return card


func _make_params_editor(i: int, step: Dictionary) -> Node:
	var type: String = String(step.get("type", "manual"))
	var params: Dictionary = step.get("params", {}) if typeof(step.get("params", {})) == TYPE_DICTIONARY else {}

	match type:
		"location":
			return _make_location_params(i, params)
		"text_answer", "clue_collect":
			return _make_text_params(i, params)
		"bluetooth":
			return _make_bluetooth_params(i, params)
		_:
			return null


func _make_location_params(i: int, params: Dictionary) -> Node:
	var box := VBoxContainer.new()
	var row1 := HBoxContainer.new()

	var lat_lbl := Label.new(); lat_lbl.text = tr("EDITOR_LOCATION_LAT"); lat_lbl.custom_minimum_size = Vector2(40, 0)
	row1.add_child(lat_lbl)
	var lat_edit := LineEdit.new()
	lat_edit.text = String(params.get("lat", ""))
	lat_edit.placeholder_text = "48.8566"
	lat_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	lat_edit.text_changed.connect(func(v: String) -> void: _update_step_param(i, "lat", float(v) if v.is_valid_float() else ""))
	row1.add_child(lat_edit)

	var lon_lbl := Label.new(); lon_lbl.text = tr("EDITOR_LOCATION_LON"); lon_lbl.custom_minimum_size = Vector2(40, 0)
	row1.add_child(lon_lbl)
	var lon_edit := LineEdit.new()
	lon_edit.text = String(params.get("lon", ""))
	lon_edit.placeholder_text = "2.3522"
	lon_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	lon_edit.text_changed.connect(func(v: String) -> void: _update_step_param(i, "lon", float(v) if v.is_valid_float() else ""))
	row1.add_child(lon_edit)
	box.add_child(row1)

	var row2 := HBoxContainer.new()
	var r_lbl := Label.new(); r_lbl.text = tr("EDITOR_LOCATION_RADIUS"); r_lbl.custom_minimum_size = Vector2(80, 0)
	row2.add_child(r_lbl)
	var r_spin := SpinBox.new()
	r_spin.min_value = 5; r_spin.max_value = 5000; r_spin.step = 5
	r_spin.value = int(params.get("radius_m", 30))
	r_spin.value_changed.connect(func(v: float) -> void: _update_step_param(i, "radius_m", int(v)))
	row2.add_child(r_spin)
	box.add_child(row2)

	return box


func _make_text_params(i: int, params: Dictionary) -> Node:
	var row := HBoxContainer.new()
	var lbl := Label.new(); lbl.text = tr("EDITOR_TEXT_ANSWER"); lbl.custom_minimum_size = Vector2(140, 0)
	row.add_child(lbl)
	var edit := LineEdit.new()
	edit.text = String(params.get("expected", ""))
	edit.placeholder_text = tr("EDITOR_TEXT_PLACEHOLDER")
	edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	edit.text_changed.connect(func(v: String) -> void: _update_step_param(i, "expected", v))
	row.add_child(edit)
	return row


func _make_bluetooth_params(i: int, params: Dictionary) -> Node:
	var raw: Variant = params.get("allowed_addresses", [])
	var addrs: String = ", ".join(PackedStringArray(raw if typeof(raw) == TYPE_ARRAY else []))
	var row := HBoxContainer.new()
	var lbl := Label.new(); lbl.text = tr("EDITOR_BLUETOOTH_MAC"); lbl.custom_minimum_size = Vector2(120, 0)
	row.add_child(lbl)
	var edit := LineEdit.new()
	edit.text = addrs
	edit.placeholder_text = "AA:BB:CC:DD:EE:FF, ..."
	edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	edit.text_changed.connect(func(v: String) -> void:
		var list := Array()
		for part in v.split(","):
			var trimmed := part.strip_edges()
			if not trimmed.is_empty():
				list.append(trimmed)
		_update_step_param(i, "allowed_addresses", list))
	row.add_child(edit)
	return row


func _update_step_param(i: int, key: String, value: Variant) -> void:
	if not _steps[i].has("params") or typeof(_steps[i]["params"]) != TYPE_DICTIONARY:
		_steps[i]["params"] = {}
	_steps[i]["params"][key] = value


func _inject_template(tmpl: Dictionary) -> void:
	_steps.append({
		"type": String(tmpl.get("type", "manual")),
		"title": String(tmpl.get("title", "")),
		"description": String(tmpl.get("description", "")),
		"params": tmpl.get("params", {}),
		"points": int(tmpl.get("defaultPoints", 10)),
		"blocksNext": true,
	})
	_render_steps()


func _add_step() -> void:
	_steps.append({
		"type": "manual",
		"title": tr("EDITOR_NEW_STEP_TITLE"),
		"description": "",
		"params": {},
		"points": 10,
		"blocksNext": true,
	})
	_render_steps()


# ============================================================  Clues  ========

func _render_clues() -> void:
	for child in _clues_box.get_children():
		child.queue_free()

	for i in _clues.size():
		_clues_box.add_child(_make_clue_row(i))


func _make_clue_row(i: int) -> Control:
	var clue: Dictionary = _clues[i]
	var card := PanelContainer.new()
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 4)
	card.add_child(vbox)

	# Header: code + delete
	var hdr := HBoxContainer.new()
	var code_edit := LineEdit.new()
	code_edit.placeholder_text = tr("EDITOR_CLUE_CODE_PLACEHOLDER")
	code_edit.text = String(clue.get("code", ""))
	code_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	code_edit.text_changed.connect(func(v: String) -> void: _clues[i]["code"] = v.to_upper().strip_edges())
	hdr.add_child(code_edit)

	var del_btn := Button.new()
	del_btn.text = "✕"
	del_btn.pressed.connect(func() -> void:
		_clues.remove_at(i)
		_render_clues())
	hdr.add_child(del_btn)
	vbox.add_child(hdr)

	# Title
	var title_edit := LineEdit.new()
	title_edit.placeholder_text = tr("EDITOR_CLUE_TITLE_PLACEHOLDER")
	title_edit.text = String(clue.get("title", ""))
	title_edit.text_changed.connect(func(v: String) -> void: _clues[i]["title"] = v)
	vbox.add_child(title_edit)

	# Reveal text
	var reveal_edit := TextEdit.new()
	reveal_edit.placeholder_text = tr("EDITOR_CLUE_REVEAL_PLACEHOLDER")
	reveal_edit.text = String(clue.get("reveal", ""))
	reveal_edit.custom_minimum_size = Vector2(0, 60)
	reveal_edit.text_changed.connect(func() -> void: _clues[i]["reveal"] = reveal_edit.text)
	vbox.add_child(reveal_edit)

	# Points
	var pts_row := HBoxContainer.new()
	var pts_lbl := Label.new(); pts_lbl.text = tr("LBL_POINTS"); pts_lbl.custom_minimum_size = Vector2(60, 0)
	pts_row.add_child(pts_lbl)
	var pts_spin := SpinBox.new()
	pts_spin.min_value = 0; pts_spin.max_value = 1000
	pts_spin.value = int(clue.get("points", 5))
	pts_spin.value_changed.connect(func(v: float) -> void: _clues[i]["points"] = int(v))
	pts_row.add_child(pts_spin)
	vbox.add_child(pts_row)

	return card


func _add_clue() -> void:
	_clues.append({
		"code": "A%d" % (_clues.size() + 1),
		"title": "",
		"reveal": "",
		"points": 5,
	})
	_render_clues()


# ============================================================  Save  =========

func _save() -> void:
	var name: String = _name_edit.text.strip_edges()
	if name.is_empty():
		set_status(tr("EDITOR_NAME_REQUIRED"), true)
		return

	# Build steps payload, preserving server-assigned ids for upsert
	var steps_payload: Array = []
	for s in _steps:
		var entry: Dictionary = {
			"title": String(s.get("title", tr("EDITOR_DEFAULT_STEP_TITLE"))),
			"description": String(s.get("description", "")),
			"type": String(s.get("type", "manual")),
			"params": s.get("params", {}),
			"points": int(s.get("points", 10)),
			"blocksNext": bool(s.get("blocksNext", true)),
		}
		if s.has("id") and String(s["id"]) != "":
			entry["id"] = String(s["id"])
		steps_payload.append(entry)

	# Build clues payload
	var clues_payload: Array = []
	for c in _clues:
		var code: String = String(c.get("code", "")).strip_edges().to_upper()
		if code.is_empty():
			continue
		var entry: Dictionary = {
			"code": code,
			"title": String(c.get("title", "")),
			"reveal": String(c.get("reveal", "")),
			"points": int(c.get("points", 5)),
		}
		if c.has("id") and String(c["id"]) != "":
			entry["id"] = String(c["id"])
		clues_payload.append(entry)

	var mode_val: String = "competitive" if _mode_option.selected == 1 else "relaxed"
	var hunt_id: String = String(_hunt.get("id", ""))

	set_status(tr("EDITOR_SAVING"))

	var resp: Dictionary
	if hunt_id.is_empty():
		var payload := {
			"name": name,
			"description": _description_edit.text,
			"mode": mode_val,
			"steps": steps_payload,
			"clues": clues_payload,
		}
		resp = await ApiClient.create_hunt(payload)
	else:
		var payload := {
			"name": name,
			"description": _description_edit.text,
			"mode": mode_val,
			"steps": steps_payload,
			"clues": clues_payload,
		}
		resp = await ApiClient.update_hunt(hunt_id, payload)

	if not resp["ok"]:
		set_status(tr("EDITOR_ERR_SAVE") % resp["error"], true)
		return

	# Refresh local state with server-assigned ids for subsequent saves
	var saved: Dictionary = resp["data"] if typeof(resp["data"]) == TYPE_DICTIONARY else {}
	if saved.has("id"):
		_hunt = saved.duplicate(true)
		_steps = _hunt.get("steps", []).duplicate(true)
		_clues = _hunt.get("clues", []).duplicate(true)
		_render_steps()
		_render_clues()

	set_status(tr("EDITOR_SAVED"))


# ============================================================  Helpers  ======

# ============================================================  Map  =========

func _open_map() -> void:
	var location_steps: Array = []
	for i in _steps.size():
		var s: Dictionary = _steps[i]
		if String(s.get("type", "")) != "location":
			continue
		var p: Dictionary = s.get("params", {}) if typeof(s.get("params", {})) == TYPE_DICTIONARY else {}
		location_steps.append({
			"stepIndex": i,
			"title": String(s.get("title", "Étape %d" % (i + 1))),
			"lat": float(p.get("lat", 0.0)),
			"lon": float(p.get("lon", 0.0)),
			"radius_m": int(p.get("radius_m", 30)),
		})

	if location_steps.is_empty():
		set_status(tr("MAP_EDITOR_NO_LOCATION"), true)
		return

	DeviceServices.load_map_steps(JSON.stringify(location_steps))
	DeviceServices.show_map()


func _on_map_confirmed(result_json: String) -> void:
	DeviceServices.hide_map()
	var result: Variant = JSON.parse_string(result_json)
	if typeof(result) != TYPE_ARRAY:
		set_status(tr("MAP_EDITOR_ERR_PARSE"), true)
		return
	for entry in result:
		var idx: int = int(entry.get("stepIndex", -1))
		if idx < 0 or idx >= _steps.size():
			continue
		if typeof(_steps[idx].get("params", null)) != TYPE_DICTIONARY:
			_steps[idx]["params"] = {}
		_steps[idx]["params"]["lat"] = float(entry.get("lat", 0.0))
		_steps[idx]["params"]["lon"] = float(entry.get("lon", 0.0))
		_steps[idx]["params"]["radius_m"] = int(entry.get("radius_m", 30))
	_render_steps()
	set_status("Coordonnées mises à jour.")


func _on_map_cancelled() -> void:
	DeviceServices.hide_map()


# ============================================================  Helpers  ======

func _make_section_header(title: String, on_add: Callable) -> Control:
	var row := HBoxContainer.new()
	var lbl := Label.new()
	lbl.text = title
	lbl.add_theme_font_size_override("font_size", 18)
	lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(lbl)
	var add_btn := Button.new()
	add_btn.text = tr("BTN_ADD")
	add_btn.pressed.connect(on_add)
	row.add_child(add_btn)
	return row
