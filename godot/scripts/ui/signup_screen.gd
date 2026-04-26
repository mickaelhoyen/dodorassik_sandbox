extends "res://scripts/ui/base_screen.gd"
## route_args: "target_role" (AppState.Role), "back_to" (screen name)

var _display_name: LineEdit
var _email: LineEdit
var _password: LineEdit
var _confirm: LineEdit
var _role_btn: CheckButton


func build() -> void:
	var target: int = int(route_args.get("target_role", AppState.Role.NONE))
	var is_creator_target: bool = target == AppState.Role.CREATOR

	add_title(tr("SIGNUP_TITLE"))
	add_subtitle(tr("SIGNUP_SUBTITLE"))

	_display_name = LineEdit.new()
	_display_name.placeholder_text = tr("SIGNUP_DISPLAY_NAME")
	_display_name.custom_minimum_size = Vector2(0, 48)
	add_node(_display_name)

	_email = LineEdit.new()
	_email.placeholder_text = tr("SIGNUP_EMAIL")
	_email.custom_minimum_size = Vector2(0, 48)
	add_node(_email)

	_password = LineEdit.new()
	_password.placeholder_text = tr("SIGNUP_PASSWORD")
	_password.secret = true
	_password.custom_minimum_size = Vector2(0, 48)
	add_node(_password)

	_confirm = LineEdit.new()
	_confirm.placeholder_text = tr("SIGNUP_CONFIRM")
	_confirm.secret = true
	_confirm.custom_minimum_size = Vector2(0, 48)
	add_node(_confirm)

	if not is_creator_target:
		_role_btn = CheckButton.new()
		_role_btn.text = tr("SIGNUP_IS_CREATOR")
		_role_btn.button_pressed = (target == AppState.Role.CREATOR)
		add_node(_role_btn)

	add_separator()
	add_button(tr("SIGNUP_BTN"), _on_register)
	add_button(tr("SIGNUP_ALREADY_ACCOUNT"),
		func() -> void: Router.go("login", {"target_role": target}))
	add_button(tr("BTN_BACK"),
		func() -> void:
			var back: String = String(route_args.get("back_to", "role_selection"))
			Router.go(back))


func _on_register() -> void:
	var display_name: String = _display_name.text.strip_edges()
	var email: String = _email.text.strip_edges()
	var password: String = _password.text
	var confirm: String = _confirm.text

	if display_name.length() < 1 or display_name.length() > 64:
		set_status(tr("SIGNUP_ERR_NAME"), true)
		return
	if not _is_valid_email(email):
		set_status(tr("SIGNUP_ERR_EMAIL"), true)
		return
	if password.length() < 8:
		set_status(tr("SIGNUP_ERR_PASSWORD_SHORT"), true)
		return
	if password != confirm:
		set_status(tr("SIGNUP_ERR_PASSWORD_MISMATCH"), true)
		return

	var target: int = int(route_args.get("target_role", AppState.Role.NONE))
	var role: String = "player"
	if _role_btn != null:
		role = "creator" if _role_btn.button_pressed else "player"
	elif target == AppState.Role.CREATOR:
		role = "creator"

	set_status(tr("SIGNUP_LOADING"))
	var resp: Dictionary = await ApiClient.register(email, password, display_name, role)
	if not resp["ok"]:
		var err: String = String(resp.get("error", ""))
		match err:
			"email_taken":    set_status(tr("SIGNUP_ERR_EMAIL_TAKEN"), true)
			"invalid_role":   set_status(tr("SIGNUP_ERR_INVALID_ROLE"), true)
			_:                set_status(err, true)
		return

	var data: Dictionary = resp["data"]
	var token: String = String(data.get("token", ""))
	var user: Dictionary = data.get("user", {})
	var role_str: String = String(user.get("role", "player"))

	AppState.set_session(user, token, _role_from_string(role_str))

	var family_id: Variant = user.get("familyId", null)
	if family_id == null:
		Router.go("family_select")
		return

	match AppState.role:
		AppState.Role.CREATOR:     Router.go("creator_home")
		AppState.Role.SUPER_ADMIN: Router.go("super_admin_home")
		_:                         Router.go("player_home")


func _is_valid_email(email: String) -> bool:
	return email.length() > 3 and "@" in email and "." in email.split("@")[-1]


func _role_from_string(name: String) -> AppState.Role:
	match name:
		"super_admin": return AppState.Role.SUPER_ADMIN
		"creator":     return AppState.Role.CREATOR
		"player":      return AppState.Role.PLAYER
		_:             return AppState.Role.NONE
