extends "res://scripts/ui/base_screen.gd"
## Email/password login. The role obtained from the server must match the
## role the user picked on the previous screen, otherwise we refuse and
## display an error (a player-only account cannot reach the creator UI).

var _email: LineEdit
var _password: LineEdit


func build() -> void:
	add_title("Connexion")
	add_subtitle("Accès créateur ou super-administrateur")

	_email = LineEdit.new()
	_email.placeholder_text = "Email"
	_email.custom_minimum_size = Vector2(0, 48)
	add_node(_email)

	_password = LineEdit.new()
	_password.placeholder_text = "Mot de passe"
	_password.secret = true
	_password.custom_minimum_size = Vector2(0, 48)
	add_node(_password)

	add_button("Se connecter", _on_login)
	add_button("Retour", func() -> void: Router.go("role_selection"))


func _on_login() -> void:
	set_status("Connexion en cours…")
	var resp: Dictionary = await ApiClient.login(_email.text, _password.text)
	if not resp["ok"]:
		set_status("Échec: %s" % resp["error"], true)
		return

	var data: Dictionary = resp["data"]
	var token := String(data.get("token", ""))
	var user: Dictionary = data.get("user", {})
	var role_str := String(user.get("role", "player"))
	var role := _role_from_string(role_str)
	var target: int = int(route_args.get("target_role", AppState.Role.NONE))

	if target != AppState.Role.NONE and role != target:
		set_status("Ce compte n'a pas les droits requis.", true)
		return

	AppState.set_session(user, token, role)
	match role:
		AppState.Role.SUPER_ADMIN: Router.go("super_admin_home")
		AppState.Role.CREATOR: Router.go("creator_home")
		_: Router.go("player_home")


func _role_from_string(name: String) -> AppState.Role:
	match name:
		"super_admin": return AppState.Role.SUPER_ADMIN
		"creator": return AppState.Role.CREATOR
		"player": return AppState.Role.PLAYER
		_: return AppState.Role.NONE
