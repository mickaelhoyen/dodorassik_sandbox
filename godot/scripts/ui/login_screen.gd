extends "res://scripts/ui/base_screen.gd"

var _email: LineEdit
var _password: LineEdit


func build() -> void:
	add_title(tr("LOGIN_TITLE"))
	add_subtitle(tr("LOGIN_SUBTITLE"))

	_email = LineEdit.new()
	_email.placeholder_text = tr("LOGIN_EMAIL")
	_email.custom_minimum_size = Vector2(0, 48)
	add_node(_email)

	_password = LineEdit.new()
	_password.placeholder_text = tr("LOGIN_PASSWORD")
	_password.secret = true
	_password.custom_minimum_size = Vector2(0, 48)
	add_node(_password)

	add_button(tr("LOGIN_BTN"), _on_login)
	add_separator()
	add_button(tr("LOGIN_CREATE_ACCOUNT"), func() -> void:
		Router.go("signup", {
			"target_role": int(route_args.get("target_role", AppState.Role.NONE)),
			"back_to": "login",
		}))
	add_button(tr("BTN_BACK"), func() -> void: Router.go("role_selection"))


func _on_login() -> void:
	set_status(tr("LOGIN_LOADING"))
	var resp: Dictionary = await ApiClient.login(_email.text, _password.text)
	if not resp["ok"]:
		set_status(tr("LOGIN_FAILED") % resp["error"], true)
		return

	var data: Dictionary = resp["data"]
	var token := String(data.get("token", ""))
	var user: Dictionary = data.get("user", {})
	var role_str := String(user.get("role", "player"))
	var role := _role_from_string(role_str)
	var target: int = int(route_args.get("target_role", AppState.Role.NONE))

	if target != AppState.Role.NONE and role != target:
		set_status(tr("LOGIN_WRONG_ROLE"), true)
		return

	AppState.set_session(user, token, role)
	var family_id: Variant = user.get("familyId", null)
	if family_id == null:
		Router.go("family_select")
		return
	match role:
		AppState.Role.SUPER_ADMIN: Router.go("super_admin_home")
		AppState.Role.CREATOR:     Router.go("creator_home")
		_:                         Router.go("player_home")


func _role_from_string(name: String) -> AppState.Role:
	match name:
		"super_admin": return AppState.Role.SUPER_ADMIN
		"creator":     return AppState.Role.CREATOR
		"player":      return AppState.Role.PLAYER
		_:             return AppState.Role.NONE
