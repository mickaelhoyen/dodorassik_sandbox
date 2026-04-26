extends "res://scripts/ui/base_screen.gd"


func build() -> void:
	add_title(tr("PLAYER_HOME_TITLE"))
	add_subtitle(tr("PLAYER_HOME_SUBTITLE"))

	if AppState.online:
		_load_remote()
	else:
		_load_local()

	add_separator()
	var pending := OfflineCache.pending_count()
	if pending > 0:
		add_subtitle(tr("PLAYER_PENDING_SYNC") % pending)
		add_button(tr("PLAYER_SYNC_BTN"), func() -> void:
			set_status(tr("PLAYER_SYNCING"))
			var sent: int = await OfflineCache.flush_pending()
			set_status(tr("PLAYER_SYNCED") % sent))

	add_separator()
	if not AppState.is_authenticated():
		add_button(tr("PLAYER_CREATE_ACCOUNT_BTN"),
			func() -> void: Router.go("signup", {
				"target_role": AppState.Role.PLAYER,
				"back_to": "player_home",
			}))
	add_button(tr("BTN_BACK"), func() -> void: Router.go("role_selection"))


func _load_remote() -> void:
	set_status(tr("LBL_LOADING"))
	var resp: Dictionary = await ApiClient.list_hunts()
	if not resp["ok"]:
		set_status(tr("PLAYER_SWITCHING_OFFLINE"), true)
		_load_local()
		return
	var hunts: Array = resp["data"] if typeof(resp["data"]) == TYPE_ARRAY else []
	_render_hunts(hunts, true)


func _load_local() -> void:
	var hunts := OfflineCache.list_local_hunts()
	if hunts.is_empty():
		set_status(tr("PLAYER_NO_HUNTS_OFFLINE"), true)
		return
	_render_hunts(hunts, false)


func _render_hunts(hunts: Array, can_download: bool) -> void:
	if hunts.is_empty():
		set_status(tr("PLAYER_NO_HUNTS"))
		return
	for hunt in hunts:
		var name := String(hunt.get("name", "?"))
		add_button(tr("PLAYER_PLAY_BTN") % name, func() -> void:
			AppState.set_active_hunt(hunt)
			Router.go("hunt_runner", {"hunt": hunt}))
		if can_download:
			add_button(tr("PLAYER_DOWNLOAD_BTN") % name, func() -> void:
				if OfflineCache.save_hunt(hunt):
					set_status(tr("PLAYER_DOWNLOADED") % name)
				else:
					set_status(tr("PLAYER_DOWNLOAD_FAILED"), true))
