extends "res://scripts/ui/base_screen.gd"
## Super-administrator dashboard (dodorassik staff).
## Read-only views are wired here; mutating actions hit the same REST API.


func build() -> void:
	add_title("Super-administrateur")
	add_subtitle("Gestion globale de la plateforme")

	add_button("Familles & utilisateurs", func() -> void: set_status("TODO: liste utilisateurs"))
	add_button("Parcours publiés", func() -> void: set_status("TODO: modération parcours"))
	add_button("Support / signalements", func() -> void: set_status("TODO: tickets support"))
	add_button("Statistiques globales", func() -> void: set_status("TODO: dashboard analytics"))

	add_separator()
	add_button("Se déconnecter", func() -> void:
		AppState.clear_session()
		Router.go("role_selection"))
