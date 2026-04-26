extends Node
## Global application state.
##
## Holds the current authenticated user, role, online/offline mode and the
## currently active hunt (if any). Other autoloads and scenes read/write
## through signals so the UI stays reactive.

signal auth_changed(user: Dictionary)
signal mode_changed(online: bool)
signal active_hunt_changed(hunt: Dictionary)

enum Role { NONE, PLAYER, CREATOR, SUPER_ADMIN }

const ROLE_NAMES := {
	Role.NONE: "none",
	Role.PLAYER: "player",
	Role.CREATOR: "creator",
	Role.SUPER_ADMIN: "super_admin",
}

var user: Dictionary = {}
var role: Role = Role.NONE
var auth_token: String = ""
var online: bool = true
var active_hunt: Dictionary = {}


func is_authenticated() -> bool:
	return not auth_token.is_empty()


func set_session(p_user: Dictionary, p_token: String, p_role: Role) -> void:
	user = p_user
	auth_token = p_token
	role = p_role
	auth_changed.emit(user)


func clear_session() -> void:
	user = {}
	auth_token = ""
	role = Role.NONE
	active_hunt = {}
	auth_changed.emit(user)


func set_online(value: bool) -> void:
	if online == value:
		return
	online = value
	mode_changed.emit(online)


func set_active_hunt(hunt: Dictionary) -> void:
	active_hunt = hunt
	active_hunt_changed.emit(hunt)
