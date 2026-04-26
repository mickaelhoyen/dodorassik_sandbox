extends "res://scripts/ui/base_screen.gd"
## Super-administrator dashboard. Routes to the moderation queue and other
## admin tools. The moderation workflow (approve/reject/takedown) lives in
## admin_moderation_screen.gd.


func build() -> void:
	add_title(tr("SUPERADMIN_TITLE"))
	add_subtitle(tr("SUPERADMIN_SUBTITLE"))

	add_button(tr("SUPERADMIN_QUEUE_BTN"),
		func() -> void: Router.go("admin_moderation"))
	add_button(tr("SUPERADMIN_USERS_BTN"),
		func() -> void: set_status("TODO"))
	add_button(tr("SUPERADMIN_STATS_BTN"),
		func() -> void: set_status("TODO"))

	add_separator()
	add_button(tr("BTN_LOGOUT"), func() -> void:
		AppState.clear_session()
		Router.go("role_selection"))
