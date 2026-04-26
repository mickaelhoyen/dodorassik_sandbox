extends "res://scripts/ui/base_screen.gd"
## Shown right after login when the connected adult has no family yet.
## Privacy: family.name is free-form — we invite a nickname, not a surname.

var _name_edit: LineEdit
var _join_id_edit: LineEdit


func build() -> void:
	add_title(tr("FAMILY_TITLE"))
	add_subtitle(tr("FAMILY_SUBTITLE"))

	var section_create := Label.new()
	section_create.text = tr("FAMILY_CREATE_SECTION")
	section_create.add_theme_font_size_override("font_size", 18)
	add_node(section_create)

	_name_edit = LineEdit.new()
	_name_edit.placeholder_text = tr("FAMILY_NAME_PLACEHOLDER")
	_name_edit.max_length = 64
	add_node(_name_edit)
	add_button(tr("BTN_CREATE"), _on_create)

	add_separator()

	var section_join := Label.new()
	section_join.text = tr("FAMILY_JOIN_SECTION")
	section_join.add_theme_font_size_override("font_size", 18)
	add_node(section_join)

	_join_id_edit = LineEdit.new()
	_join_id_edit.placeholder_text = tr("FAMILY_ID_PLACEHOLDER")
	add_node(_join_id_edit)
	add_button(tr("BTN_JOIN"), _on_join)

	add_separator()
	add_button(tr("FAMILY_LATER_BTN"), _on_skip)


func _on_create() -> void:
	var name := _name_edit.text.strip_edges()
	if name.is_empty():
		set_status(tr("FAMILY_ERR_NAME_EMPTY"), true)
		return
	set_status(tr("FAMILY_CREATING"))
	var resp: Dictionary = await ApiClient.create_family(name)
	if not resp["ok"]:
		set_status(resp["error"], true)
		return
	_route_after_login()


func _on_join() -> void:
	var id := _join_id_edit.text.strip_edges()
	if id.is_empty():
		set_status(tr("FAMILY_ERR_ID_EMPTY"), true)
		return
	set_status(tr("FAMILY_JOINING"))
	var resp: Dictionary = await ApiClient.join_family(id)
	if not resp["ok"]:
		set_status(resp["error"], true)
		return
	_route_after_login()


func _on_skip() -> void:
	_route_after_login()


func _route_after_login() -> void:
	match AppState.role:
		AppState.Role.SUPER_ADMIN: Router.go("super_admin_home")
		AppState.Role.CREATOR:     Router.go("creator_home")
		_:                         Router.go("player_home")
