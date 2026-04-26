extends "res://scripts/ui/base_screen.gd"
## Shown right after login when the connected adult has no family yet.
## They can either create a brand new family or join an existing one
## using the family ID shared by another adult of the same household.
##
## Privacy note: family.name is free-form. We invite the user to pick a
## non-identifying nickname (no surname, no kid first names) — a label
## displayed on the consent line below the input. See PRIVACY.md §1.

var _name_edit: LineEdit
var _join_id_edit: LineEdit


func build() -> void:
	add_title("Ta famille")
	add_subtitle("Crée une famille ou rejoins-en une existante. Évite d'utiliser le nom de famille — un surnom suffit.")

	var section_create := Label.new()
	section_create.text = "Créer une nouvelle famille"
	section_create.add_theme_font_size_override("font_size", 18)
	add_node(section_create)

	_name_edit = LineEdit.new()
	_name_edit.placeholder_text = "Surnom (ex: Les Aventuriers)"
	_name_edit.max_length = 64
	add_node(_name_edit)
	add_button("Créer", _on_create)

	add_separator()

	var section_join := Label.new()
	section_join.text = "Rejoindre une famille existante"
	section_join.add_theme_font_size_override("font_size", 18)
	add_node(section_join)

	_join_id_edit = LineEdit.new()
	_join_id_edit.placeholder_text = "ID de la famille"
	add_node(_join_id_edit)
	add_button("Rejoindre", _on_join)

	add_separator()
	add_button("Plus tard", _on_skip)


func _on_create() -> void:
	var name := _name_edit.text.strip_edges()
	if name.is_empty():
		set_status("Le surnom ne peut pas être vide.", true)
		return
	set_status("Création…")
	var resp: Dictionary = await ApiClient.create_family(name)
	if not resp["ok"]:
		set_status("Erreur: %s" % resp["error"], true)
		return
	_route_after_login()


func _on_join() -> void:
	var id := _join_id_edit.text.strip_edges()
	if id.is_empty():
		set_status("Saisis l'ID de la famille à rejoindre.", true)
		return
	set_status("Adhésion…")
	var resp: Dictionary = await ApiClient.join_family(id)
	if not resp["ok"]:
		set_status("Erreur: %s" % resp["error"], true)
		return
	_route_after_login()


func _on_skip() -> void:
	_route_after_login()


func _route_after_login() -> void:
	match AppState.role:
		AppState.Role.SUPER_ADMIN: Router.go("super_admin_home")
		AppState.Role.CREATOR: Router.go("creator_home")
		_: Router.go("player_home")
