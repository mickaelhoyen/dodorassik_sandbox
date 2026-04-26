extends "res://scripts/ui/base_screen.gd"
## Creator dashboard. Lists hunts owned by the connected creator and lets
## them open the editor or publish/unpublish.


func build() -> void:
	add_title("Mes parcours")
	add_subtitle("Créez et gérez vos chasses au trésor")

	add_button("Nouveau parcours", func() -> void: Router.go("hunt_editor", {"hunt": {}}))

	add_separator()
	_load_hunts()

	add_separator()
	add_button("Se déconnecter", func() -> void:
		AppState.clear_session()
		Router.go("role_selection"))


func _load_hunts() -> void:
	set_status("Chargement…")
	var resp: Dictionary = await ApiClient.list_hunts()
	if not resp["ok"]:
		set_status("Erreur: %s" % resp["error"], true)
		return

	var hunts: Array = resp["data"] if typeof(resp["data"]) == TYPE_ARRAY else []
	if hunts.is_empty():
		set_status("Aucun parcours pour le moment.")
		return

	set_status("%d parcours" % hunts.size())
	for hunt in hunts:
		var name := String(hunt.get("name", "Sans titre"))
		add_button(name, func() -> void: Router.go("hunt_editor", {"hunt": hunt}))
