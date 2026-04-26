extends "res://scripts/ui/base_screen.gd"
## Player dashboard. Shows hunts available locally (offline cache) and, if
## online, the catalogue from the server. Players don't authenticate by
## default — anyone in the family can use the phone to assist the kids.


func build() -> void:
	add_title("Bienvenue !")
	add_subtitle("Choisis une chasse au trésor")

	if AppState.online:
		_load_remote()
	else:
		_load_local()

	add_separator()
	var pending := OfflineCache.pending_count()
	if pending > 0:
		add_subtitle("%d résultat(s) en attente d'envoi." % pending)
		add_button("Synchroniser maintenant", func() -> void:
			set_status("Synchronisation…")
			var sent: int = await OfflineCache.flush_pending()
			set_status("%d envoyés." % sent))

	add_separator()
	add_button("Retour", func() -> void: Router.go("role_selection"))


func _load_remote() -> void:
	set_status("Chargement…")
	var resp: Dictionary = await ApiClient.list_hunts()
	if not resp["ok"]:
		set_status("Hors ligne — bascule sur le cache local.", true)
		_load_local()
		return
	var hunts: Array = resp["data"] if typeof(resp["data"]) == TYPE_ARRAY else []
	_render_hunts(hunts, true)


func _load_local() -> void:
	var hunts := OfflineCache.list_local_hunts()
	if hunts.is_empty():
		set_status("Aucune chasse disponible hors ligne. Connecte-toi pour en télécharger.", true)
		return
	_render_hunts(hunts, false)


func _render_hunts(hunts: Array, can_download: bool) -> void:
	if hunts.is_empty():
		set_status("Aucune chasse disponible.")
		return
	for hunt in hunts:
		var name := String(hunt.get("name", "Sans titre"))
		add_button("▶ %s" % name, func() -> void:
			AppState.set_active_hunt(hunt)
			Router.go("hunt_runner", {"hunt": hunt}))
		if can_download:
			add_button("⬇ Télécharger '%s' (offline)" % name, func() -> void:
				if OfflineCache.save_hunt(hunt):
					set_status("'%s' téléchargée." % name)
				else:
					set_status("Échec du téléchargement.", true))
