extends Control
## Entry point. Hands the screen host to the Router which then drives
## navigation. Also wires global UI feedback (online indicator, back input).

@onready var _host: Control = $ScreenHost


func _ready() -> void:
	Router.bind_host(_host)


func _input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_back"):
		Router.back()
