extends "res://scripts/ui/base_screen.gd"
## Community / personal step template library.
## Browsable by type/tag/mine. Selecting a template calls _on_use_template
## signal so the hunt_editor can inject it as a new step.
##
## Route args: { "on_use": Callable(template_dict) }

signal template_selected(template: Dictionary)

const STEP_TYPES := ["", "manual", "location", "photo", "bluetooth", "text_answer", "clue_collect"]

var _on_use: Callable
var _results_box: VBoxContainer
var _mine_only: bool = false
var _type_filter: String = ""


func build() -> void:
	_on_use = route_args.get("on_use", Callable())

	add_title(tr("LIBRARY_TITLE"))
	add_subtitle(tr("LIBRARY_SUBTITLE"))

	# ---- Filters row ----
	var filters := HBoxContainer.new()
	filters.add_theme_constant_override("separation", 8)

	var mine_btn := CheckButton.new()
	mine_btn.text = tr("LIBRARY_MINE_BTN")
	mine_btn.toggled.connect(func(pressed: bool) -> void:
		_mine_only = pressed
		_load())
	filters.add_child(mine_btn)

	var type_opt := OptionButton.new()
	type_opt.add_item(tr("LIBRARY_FILTER_ALL"))
	type_opt.add_item(tr("EDITOR_STEP_TYPE_MANUAL"))
	type_opt.add_item(tr("EDITOR_STEP_TYPE_LOCATION"))
	type_opt.add_item(tr("EDITOR_STEP_TYPE_PHOTO"))
	type_opt.add_item(tr("EDITOR_STEP_TYPE_BLUETOOTH"))
	type_opt.add_item(tr("EDITOR_STEP_TYPE_TEXT"))
	type_opt.add_item(tr("EDITOR_STEP_TYPE_CLUE"))
	type_opt.item_selected.connect(func(idx: int) -> void:
		_type_filter = STEP_TYPES[idx]
		_load())
	type_opt.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	filters.add_child(type_opt)

	add_node(filters)
	add_separator()

	_results_box = VBoxContainer.new()
	_results_box.add_theme_constant_override("separation", 6)
	add_node(_results_box)

	add_separator()
	add_button(tr("BTN_BACK"), func() -> void: Router.back())

	_load()


func _load() -> void:
	for child in _results_box.get_children():
		child.queue_free()

	var loading := Label.new()
	loading.text = tr("LIBRARY_LOADING")
	_results_box.add_child(loading)

	var resp: Dictionary = await ApiClient.search_step_templates(_mine_only, _type_filter)

	for child in _results_box.get_children():
		child.queue_free()

	if not resp["ok"]:
		set_status(resp.get("error", "?"), true)
		return

	var templates: Array = resp.get("data", [])
	if templates.is_empty():
		var empty := Label.new()
		empty.text = tr("LIBRARY_EMPTY")
		empty.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_results_box.add_child(empty)
		return

	for tmpl in templates:
		_results_box.add_child(_make_template_row(tmpl))


func _make_template_row(tmpl: Dictionary) -> Control:
	var card := PanelContainer.new()
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 2)
	card.add_child(vbox)

	var hdr := HBoxContainer.new()
	var title_lbl := Label.new()
	title_lbl.text = "[%s] %s" % [String(tmpl.get("type", "?")), String(tmpl.get("title", "?"))]
	title_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title_lbl.autowrap_mode = TextServer.AUTOWRAP_OFF
	hdr.add_child(title_lbl)

	var use_btn := Button.new()
	use_btn.text = tr("LIBRARY_USE_BTN")
	use_btn.pressed.connect(func() -> void:
		if _on_use.is_valid():
			_on_use.call(tmpl)
		Router.back())
	hdr.add_child(use_btn)
	vbox.add_child(hdr)

	var by_lbl := Label.new()
	by_lbl.text = tr("LIBRARY_BY") % String(tmpl.get("createdByName", "?"))
	by_lbl.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
	vbox.add_child(by_lbl)

	var tags: String = String(tmpl.get("tags", ""))
	if not tags.is_empty():
		var tag_lbl := Label.new()
		tag_lbl.text = "🏷 %s" % tags
		tag_lbl.add_theme_color_override("font_color", Color(0.5, 0.7, 1.0))
		vbox.add_child(tag_lbl)

	return card
