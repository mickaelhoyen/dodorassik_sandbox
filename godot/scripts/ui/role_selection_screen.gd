extends "res://scripts/ui/base_screen.gd"
## First screen the user sees. Picks the role they will use for this session.
## Players proceed straight in (a single phone often serves a whole family of
## kids), creators and super-admins must authenticate.


func build() -> void:
	add_title("Dodorassik")
	add_subtitle("Chasse au trésor familiale en environnement réel")
	add_separator()

	add_button("Je joue (famille)", _on_player)
	add_button("Je crée un parcours", _on_creator)
	add_button("Super-administrateur", _on_super_admin)

	add_separator()
	var mode_label := Label.new()
	mode_label.text = "Mode: %s" % ("en ligne" if AppState.online else "hors ligne")
	mode_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_node(mode_label)

	var toggle := CheckButton.new()
	toggle.text = "Mode hors ligne"
	toggle.button_pressed = not AppState.online
	toggle.toggled.connect(func(pressed: bool) -> void:
		AppState.set_online(not pressed)
		mode_label.text = "Mode: %s" % ("en ligne" if AppState.online else "hors ligne"))
	add_node(toggle)


func _on_player() -> void:
	AppState.role = AppState.Role.PLAYER
	Router.go("player_home")


func _on_creator() -> void:
	if AppState.is_authenticated() and AppState.role == AppState.Role.CREATOR:
		Router.go("creator_home")
	else:
		Router.go("login", {"target_role": AppState.Role.CREATOR})


func _on_super_admin() -> void:
	if AppState.is_authenticated() and AppState.role == AppState.Role.SUPER_ADMIN:
		Router.go("super_admin_home")
	else:
		Router.go("login", {"target_role": AppState.Role.SUPER_ADMIN})
