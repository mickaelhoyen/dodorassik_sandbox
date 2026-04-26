extends "res://scripts/ui/base_screen.gd"


func build() -> void:
	add_title(tr("CREATOR_TITLE"))
	add_subtitle(tr("CREATOR_SUBTITLE"))

	add_button(tr("CREATOR_NEW_BTN"), func() -> void: Router.go("hunt_editor", {"hunt": {}}))

	add_separator()
	_load_hunts()

	add_separator()
	add_button(tr("BTN_LOGOUT"), func() -> void:
		AppState.clear_session()
		Router.go("role_selection"))


func _load_hunts() -> void:
	set_status(tr("LBL_LOADING"))
	var resp: Dictionary = await ApiClient.list_hunts()
	if not resp["ok"]:
		set_status(resp["error"], true)
		return

	var hunts: Array = resp["data"] if typeof(resp["data"]) == TYPE_ARRAY else []
	if hunts.is_empty():
		set_status(tr("CREATOR_NO_HUNTS"))
		return

	set_status(tr("CREATOR_HUNT_COUNT") % hunts.size())
	for hunt in hunts:
		var name := String(hunt.get("name", "?"))
		add_button(name, func() -> void: Router.go("hunt_editor", {"hunt": hunt}))
