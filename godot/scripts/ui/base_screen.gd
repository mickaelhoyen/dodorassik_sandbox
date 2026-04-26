extends Control
class_name BaseScreen
## Helpers shared by every screen: padded VBox layout, title, status bar.

var route_args: Dictionary = {}

var _root: VBoxContainer
var _status: Label


func _ready() -> void:
	_build_chrome()
	build()


func build() -> void:
	## Override in subclasses.
	pass


func _build_chrome() -> void:
	var margin := MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 24)
	margin.add_theme_constant_override("margin_right", 24)
	margin.add_theme_constant_override("margin_top", 32)
	margin.add_theme_constant_override("margin_bottom", 24)
	add_child(margin)

	_root = VBoxContainer.new()
	_root.add_theme_constant_override("separation", 12)
	margin.add_child(_root)


func add_title(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 28)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_root.add_child(label)
	return label


func add_subtitle(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 16)
	label.modulate = Color(1, 1, 1, 0.7)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_root.add_child(label)
	return label


func add_button(text: String, on_pressed: Callable) -> Button:
	var btn := Button.new()
	btn.text = text
	btn.custom_minimum_size = Vector2(0, 56)
	btn.pressed.connect(on_pressed)
	_root.add_child(btn)
	return btn


func add_separator() -> void:
	var sep := HSeparator.new()
	_root.add_child(sep)


func add_node(node: Node) -> void:
	_root.add_child(node)


func set_status(text: String, error: bool = false) -> void:
	if _status == null:
		_status = Label.new()
		_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		_root.add_child(_status)
	_status.text = text
	_status.modulate = Color(1, 0.4, 0.4) if error else Color(0.6, 1, 0.6)
