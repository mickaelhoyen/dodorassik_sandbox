extends "res://scripts/ui/base_screen.gd"
## Team selection before a competitive hunt.
## Route args: { "hunt": <hunt_dict> }

var _hunt: Dictionary
var _teams_list: VBoxContainer


func build() -> void:
	_hunt = route_args.get("hunt", AppState.active_hunt)
	if _hunt.is_empty():
		add_title("?")
		add_button(tr("BTN_BACK"), func() -> void: Router.go("player_home"))
		return

	add_title(tr("TEAM_TITLE") % String(_hunt.get("name", "")))
	add_subtitle(tr("TEAM_SUBTITLE"))

	add_separator()
	var create_label := Label.new()
	create_label.text = tr("TEAM_CREATE_SECTION")
	create_label.add_theme_font_size_override("font_size", 16)
	add_node(create_label)

	var name_input := LineEdit.new()
	name_input.placeholder_text = tr("TEAM_NAME_PLACEHOLDER")
	name_input.max_length = 64
	add_node(name_input)

	var color_input := LineEdit.new()
	color_input.placeholder_text = tr("TEAM_COLOR_PLACEHOLDER")
	color_input.max_length = 7
	add_node(color_input)

	add_button(tr("TEAM_CREATE_BTN"), func() -> void:
		var team_name: String = name_input.text.strip_edges()
		if team_name.is_empty():
			set_status(tr("TEAM_ERR_NAME"), true)
			return
		_create_team(team_name, color_input.text.strip_edges()))

	add_separator()
	var teams_label := Label.new()
	teams_label.text = tr("TEAM_JOIN_SECTION")
	teams_label.add_theme_font_size_override("font_size", 16)
	add_node(teams_label)

	_teams_list = VBoxContainer.new()
	_teams_list.add_theme_constant_override("separation", 6)
	add_node(_teams_list)

	add_separator()
	add_button(tr("BTN_BACK"), func() -> void: Router.go("player_home"))

	_load_teams()


func _load_teams() -> void:
	for child in _teams_list.get_children():
		child.queue_free()

	var loading := Label.new()
	loading.text = tr("TEAM_LOADING")
	_teams_list.add_child(loading)

	var hunt_id: String = String(_hunt.get("id", ""))
	var resp: Dictionary = await ApiClient.list_teams(hunt_id)

	for child in _teams_list.get_children():
		child.queue_free()

	if not resp["ok"]:
		var err := Label.new()
		err.text = resp.get("error", "?")
		_teams_list.add_child(err)
		return

	var teams: Array = resp.get("data", [])
	if teams.is_empty():
		var none := Label.new()
		none.text = tr("TEAM_NO_TEAMS")
		_teams_list.add_child(none)
		return

	for team in teams:
		var row := HBoxContainer.new()
		var lbl := Label.new()
		lbl.text = tr("TEAM_MEMBER_COUNT") % [
			String(team.get("name", "?")),
			int(Array(team.get("members", [])).size()),
		]
		lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(lbl)

		var join_btn := Button.new()
		join_btn.text = tr("TEAM_JOIN_BTN")
		var team_id: String = String(team.get("id", ""))
		join_btn.pressed.connect(func() -> void: _join_team(team_id))
		row.add_child(join_btn)
		_teams_list.add_child(row)


func _create_team(team_name: String, color: String) -> void:
	set_status(tr("TEAM_CREATING"))
	var hunt_id: String = String(_hunt.get("id", ""))
	var payload: Dictionary = {"name": team_name}
	if color.length() == 7 and color.begins_with("#"):
		payload["color"] = color

	var resp: Dictionary = await ApiClient.create_team(hunt_id, payload)
	if not resp["ok"]:
		set_status(resp.get("error", "?"), true)
		return

	AppState.set_active_team(resp.get("data", {}))
	Router.go("hunt_runner", {"hunt": _hunt})


func _join_team(team_id: String) -> void:
	set_status(tr("TEAM_JOINING"))
	var hunt_id: String = String(_hunt.get("id", ""))
	var resp: Dictionary = await ApiClient.join_team(hunt_id, team_id)
	if not resp["ok"] and resp.get("error", "") != "already_member":
		set_status(resp.get("error", "?"), true)
		return

	var teams_resp: Dictionary = await ApiClient.list_teams(hunt_id)
	if teams_resp["ok"]:
		for t in teams_resp.get("data", []):
			if String(t.get("id", "")) == team_id:
				AppState.set_active_team(t)
				break
	else:
		# Refresh failed — use minimal dict so the run can still start.
		AppState.set_active_team({"id": team_id, "name": "?"})

	Router.go("hunt_runner", {"hunt": _hunt})
