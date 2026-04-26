extends "res://scripts/ui/base_screen.gd"
## Allows family members to create or join a competitive team before starting
## a hunt. Shown automatically when a competitive hunt is launched without a
## team selected.
##
## Route args: { "hunt": <hunt_dict> }

var _hunt: Dictionary
var _teams_list: VBoxContainer
var _status_label: Label


func build() -> void:
	_hunt = route_args.get("hunt", AppState.active_hunt)
	if _hunt.is_empty():
		add_title("Erreur")
		add_button("Retour", func() -> void: Router.go("player_home"))
		return

	var hunt_name: String = String(_hunt.get("name", "Chasse"))
	add_title("Équipes — %s" % hunt_name)
	add_subtitle("Mode compétitif : rejoins ou crée une équipe pour ta famille.")

	# ---- Create team section ----
	add_separator()
	var create_label := Label.new()
	create_label.text = "Créer une équipe"
	create_label.add_theme_font_size_override("font_size", 16)
	add_node(create_label)

	var name_input := LineEdit.new()
	name_input.placeholder_text = "Nom de l'équipe (ex: Les Garçons)"
	name_input.max_length = 64
	add_node(name_input)

	var color_input := LineEdit.new()
	color_input.placeholder_text = "Couleur hex (ex: #FF6B35) — optionnel"
	color_input.max_length = 7
	add_node(color_input)

	add_button("Créer", func() -> void:
		var team_name: String = name_input.text.strip_edges()
		if team_name.is_empty():
			set_status("Donne un nom à ton équipe.", true)
			return
		_create_team(team_name, color_input.text.strip_edges()))

	# ---- Existing teams ----
	add_separator()
	var teams_label := Label.new()
	teams_label.text = "Équipes existantes"
	teams_label.add_theme_font_size_override("font_size", 16)
	add_node(teams_label)

	_teams_list = VBoxContainer.new()
	_teams_list.add_theme_constant_override("separation", 6)
	add_node(_teams_list)

	add_separator()
	add_button("Retour", func() -> void: Router.go("player_home"))

	_load_teams()


func _load_teams() -> void:
	for child in _teams_list.get_children():
		child.queue_free()

	var loading := Label.new()
	loading.text = "Chargement…"
	_teams_list.add_child(loading)

	var hunt_id: String = String(_hunt.get("id", ""))
	var resp: Dictionary = await ApiClient.list_teams(hunt_id)

	for child in _teams_list.get_children():
		child.queue_free()

	if not resp["ok"]:
		var err := Label.new()
		err.text = "Impossible de charger les équipes."
		_teams_list.add_child(err)
		return

	var teams: Array = resp.get("data", [])
	if teams.is_empty():
		var none := Label.new()
		none.text = "Aucune équipe pour l'instant — crée la première !"
		_teams_list.add_child(none)
		return

	for team in teams:
		var row := HBoxContainer.new()
		var lbl := Label.new()
		lbl.text = "%s (%d membres)" % [
			String(team.get("name", "?")),
			int(Array(team.get("members", [])).size()),
		]
		lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(lbl)

		var join_btn := Button.new()
		join_btn.text = "Rejoindre"
		var team_id: String = String(team.get("id", ""))
		join_btn.pressed.connect(func() -> void: _join_team(team_id))
		row.add_child(join_btn)

		_teams_list.add_child(row)


func _create_team(team_name: String, color: String) -> void:
	set_status("Création…")
	var hunt_id: String = String(_hunt.get("id", ""))
	var payload: Dictionary = {"name": team_name}
	if color.length() == 7 and color.begins_with("#"):
		payload["color"] = color

	var resp: Dictionary = await ApiClient.create_team(hunt_id, payload)
	if not resp["ok"]:
		set_status("Erreur : %s" % resp.get("error", "?"), true)
		return

	var team: Dictionary = resp.get("data", {})
	AppState.set_active_team(team)
	Router.go("hunt_runner", {"hunt": _hunt})


func _join_team(team_id: String) -> void:
	set_status("Rejoindre l'équipe…")
	var hunt_id: String = String(_hunt.get("id", ""))
	var resp: Dictionary = await ApiClient.join_team(hunt_id, team_id)
	if not resp["ok"]:
		var err: String = resp.get("error", "?")
		if err == "already_member":
			# Already in the team — just proceed.
			pass
		else:
			set_status("Erreur : %s" % err, true)
			return

	# Find the team dict to store in AppState.
	var teams_resp: Dictionary = await ApiClient.list_teams(hunt_id)
	if teams_resp["ok"]:
		var teams: Array = teams_resp.get("data", [])
		for t in teams:
			if String(t.get("id", "")) == team_id:
				AppState.set_active_team(t)
				break

	Router.go("hunt_runner", {"hunt": _hunt})
