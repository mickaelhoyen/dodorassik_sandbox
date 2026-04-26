extends Node
## Centralised navigation: swaps the active screen under the Main scene's
## `ScreenHost` container. Screens are simple Control scripts (no .tscn
## needed) so the project stays easy to extend without the editor.

signal screen_changed(name: String)

var _host: Control
var _current: Control
var _stack: Array[String] = []


func bind_host(host: Control) -> void:
	_host = host
	# Show the role selection by default.
	go("role_selection")


func go(name: String, args: Dictionary = {}) -> void:
	if _host == null:
		push_warning("Router: no host bound, ignoring go(%s)" % name)
		return
	var screen := _build_screen(name, args)
	if screen == null:
		push_error("Router: unknown screen '%s'" % name)
		return
	if _current != null:
		_current.queue_free()
	_host.add_child(screen)
	screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_current = screen
	_stack.append(name)
	screen_changed.emit(name)


func back() -> void:
	if _stack.size() <= 1:
		return
	_stack.pop_back()
	var prev: String = _stack.pop_back()
	go(prev)


func _build_screen(name: String, args: Dictionary) -> Control:
	var script: GDScript = null
	match name:
		"role_selection":
			script = load("res://scripts/ui/role_selection_screen.gd")
		"login":
			script = load("res://scripts/ui/login_screen.gd")
		"family_select":
			script = load("res://scripts/ui/family_select_screen.gd")
		"super_admin_home":
			script = load("res://scripts/ui/super_admin_home.gd")
		"creator_home":
			script = load("res://scripts/ui/creator_home.gd")
		"hunt_editor":
			script = load("res://scripts/ui/hunt_editor.gd")
		"player_home":
			script = load("res://scripts/ui/player_home.gd")
		"hunt_runner":
			script = load("res://scripts/ui/hunt_runner.gd")
		"signup":
			script = load("res://scripts/ui/signup_screen.gd")
		"team_select":
			script = load("res://scripts/ui/team_select_screen.gd")
		"leaderboard":
			script = load("res://scripts/ui/leaderboard_screen.gd")
		_:
			return null
	var screen: Control = script.new()
	screen.set("route_args", args)
	return screen
