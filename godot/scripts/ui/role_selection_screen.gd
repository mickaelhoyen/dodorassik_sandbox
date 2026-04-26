extends "res://scripts/ui/base_screen.gd"
## First screen. Picks the role and lets the user switch language.


func build() -> void:
	add_title(tr("APP_TITLE"))
	add_subtitle(tr("APP_SUBTITLE"))
	add_separator()

	add_button(tr("ROLE_PLAYER_BTN"), _on_player)
	add_button(tr("ROLE_CREATOR_BTN"), _on_creator)
	add_button(tr("ROLE_SUPERADMIN_BTN"), _on_super_admin)

	add_separator()

	var mode_label := Label.new()
	mode_label.text = tr("LBL_ONLINE_MODE") % (tr("LBL_ONLINE") if AppState.online else tr("LBL_OFFLINE"))
	mode_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_node(mode_label)

	var toggle := CheckButton.new()
	toggle.text = tr("LBL_OFFLINE_TOGGLE")
	toggle.button_pressed = not AppState.online
	toggle.toggled.connect(func(pressed: bool) -> void:
		AppState.set_online(not pressed)
		mode_label.text = tr("LBL_ONLINE_MODE") % (tr("LBL_ONLINE") if AppState.online else tr("LBL_OFFLINE")))
	add_node(toggle)

	add_separator()

	# Language switcher
	var lang_row := HBoxContainer.new()
	var lang_lbl := Label.new()
	lang_lbl.text = tr("LBL_LANGUAGE") + " : "
	lang_row.add_child(lang_lbl)
	var lang_btn_fr := Button.new()
	lang_btn_fr.text = "🇫🇷 Français"
	lang_btn_fr.pressed.connect(func() -> void:
		AppLocale.set_locale("fr")
		Router.go("role_selection"))
	lang_row.add_child(lang_btn_fr)
	var lang_btn_en := Button.new()
	lang_btn_en.text = "🇬🇧 English"
	lang_btn_en.pressed.connect(func() -> void:
		AppLocale.set_locale("en")
		Router.go("role_selection"))
	lang_row.add_child(lang_btn_en)
	add_node(lang_row)


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
