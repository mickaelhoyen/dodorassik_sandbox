extends "res://scripts/ui/base_screen.gd"
## Super-admin moderation queue. Lists submitted hunts and allows approve /
## reject / takedown. Uses AdminHuntsController API endpoints.

var _queue_box: VBoxContainer


func build() -> void:
	add_title(tr("ADMIN_MOD_TITLE"))
	add_subtitle(tr("ADMIN_MOD_SUBTITLE"))

	_queue_box = VBoxContainer.new()
	_queue_box.add_theme_constant_override("separation", 8)
	add_node(_queue_box)

	add_separator()
	add_button(tr("LB_REFRESH_BTN"), func() -> void: _load_queue())
	add_button(tr("BTN_BACK"), func() -> void: Router.go("super_admin_home"))

	_load_queue()


func _load_queue() -> void:
	for child in _queue_box.get_children():
		child.queue_free()

	var loading := Label.new()
	loading.text = tr("ADMIN_MOD_LOADING")
	_queue_box.add_child(loading)

	var resp: Dictionary = await ApiClient.admin_moderation_queue()

	for child in _queue_box.get_children():
		child.queue_free()

	if not resp["ok"]:
		set_status(resp.get("error", "?"), true)
		return

	var hunts: Array = resp.get("data", [])
	if hunts.is_empty():
		var empty := Label.new()
		empty.text = tr("ADMIN_MOD_EMPTY")
		empty.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_queue_box.add_child(empty)
		return

	for hunt in hunts:
		_queue_box.add_child(_make_hunt_card(hunt))


func _make_hunt_card(hunt: Dictionary) -> Control:
	var card := PanelContainer.new()
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 4)
	card.add_child(vbox)

	# Header: name + step count
	var hdr_lbl := Label.new()
	var steps_count: int = int(Array(hunt.get("steps", [])).size())
	hdr_lbl.text = "%s — %s" % [
		String(hunt.get("name", "?")),
		tr("ADMIN_MOD_STEPS") % steps_count,
	]
	hdr_lbl.add_theme_font_size_override("font_size", 16)
	hdr_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	vbox.add_child(hdr_lbl)

	var desc_lbl := Label.new()
	var full_desc: String = String(hunt.get("description", ""))
	desc_lbl.text = full_desc.left(120) + ("…" if full_desc.length() > 120 else "")
	desc_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	desc_lbl.add_theme_color_override("font_color", Color(0.8, 0.8, 0.8))
	vbox.add_child(desc_lbl)

	var hunt_id: String = String(hunt.get("id", ""))

	# Action row: approve + reject
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 8)

	var approve_btn := Button.new()
	approve_btn.text = tr("ADMIN_MOD_APPROVE_BTN")
	approve_btn.pressed.connect(func() -> void: _approve(hunt_id, card))
	actions.add_child(approve_btn)

	var reject_btn := Button.new()
	reject_btn.text = tr("ADMIN_MOD_REJECT_BTN")
	reject_btn.pressed.connect(func() -> void: _show_reject_form(hunt_id, vbox))
	actions.add_child(reject_btn)

	vbox.add_child(actions)

	return card


func _approve(hunt_id: String, card: Control) -> void:
	set_status(tr("ADMIN_MOD_APPROVING"))
	var resp: Dictionary = await ApiClient.admin_approve_hunt(hunt_id)
	if not resp["ok"]:
		set_status(resp.get("error", "?"), true)
		return
	set_status(tr("ADMIN_MOD_APPROVED"))
	card.queue_free()


func _show_reject_form(hunt_id: String, vbox: VBoxContainer) -> void:
	# Remove existing reject form if already shown.
	var existing: Node = vbox.find_child("reject_form", false, false)
	if existing != null:
		existing.queue_free()
		return

	var form := VBoxContainer.new()
	form.name = "reject_form"
	form.add_theme_constant_override("separation", 4)

	var reason_edit := LineEdit.new()
	reason_edit.placeholder_text = tr("ADMIN_MOD_REASON_PLACEHOLDER")
	form.add_child(reason_edit)

	var confirm_btn := Button.new()
	confirm_btn.text = tr("ADMIN_MOD_CONFIRM_REJECT_BTN")
	confirm_btn.pressed.connect(func() -> void:
		var reason: String = reason_edit.text.strip_edges()
		if reason.length() < 5:
			set_status(tr("ADMIN_MOD_ERR_REASON"), true)
			return
		_reject(hunt_id, reason, vbox.get_parent()))
	form.add_child(confirm_btn)

	vbox.add_child(form)


func _reject(hunt_id: String, reason: String, card: Control) -> void:
	set_status(tr("ADMIN_MOD_REJECTING"))
	var resp: Dictionary = await ApiClient.admin_reject_hunt(hunt_id, reason)
	if not resp["ok"]:
		set_status(resp.get("error", "?"), true)
		return
	set_status(tr("ADMIN_MOD_REJECTED"))
	card.queue_free()
