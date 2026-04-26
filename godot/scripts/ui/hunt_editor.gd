extends "res://scripts/ui/base_screen.gd"
## Editor for a single hunt. Lets the creator describe the hunt, add/remove
## steps and pick the validation type per step (photo, GPS, Bluetooth beacon,
## free text, manual confirmation by the adult, etc.).
##
## Validation types are open-ended on purpose — collecting a bag of trash
## during a hike is a manual step the adult confirms; spotting a tree is a
## photo step; arriving at a checkpoint is a GPS step.

const STEP_TYPES := [
	"manual",         # adult ticks the box (litter pickup, stretch break, riddle)
	"location",       # GPS within radius
	"photo",          # take a picture
	"bluetooth",      # scan for a known beacon MAC
	"text_answer",    # free-form answer compared to expected
	"clue_collect",   # pick up a physical clue, type code into phone
]

var _name_edit: LineEdit
var _description_edit: TextEdit
var _steps_box: VBoxContainer
var _steps: Array = []


func build() -> void:
	var hunt: Dictionary = route_args.get("hunt", {})
	_steps = hunt.get("steps", []).duplicate(true) if hunt.has("steps") else []

	add_title("Éditeur de parcours")

	_name_edit = LineEdit.new()
	_name_edit.placeholder_text = "Nom du parcours"
	_name_edit.text = String(hunt.get("name", ""))
	add_node(_name_edit)

	_description_edit = TextEdit.new()
	_description_edit.placeholder_text = "Description (histoire, public visé…)"
	_description_edit.text = String(hunt.get("description", ""))
	_description_edit.custom_minimum_size = Vector2(0, 120)
	add_node(_description_edit)

	add_separator()
	var steps_label := Label.new()
	steps_label.text = "Étapes"
	steps_label.add_theme_font_size_override("font_size", 20)
	add_node(steps_label)

	_steps_box = VBoxContainer.new()
	_steps_box.add_theme_constant_override("separation", 6)
	add_node(_steps_box)
	_render_steps()

	add_button("+ Ajouter une étape", _on_add_step)

	add_separator()
	add_button("Enregistrer", func() -> void: _save(hunt))
	add_button("Retour", func() -> void: Router.go("creator_home"))


func _render_steps() -> void:
	for child in _steps_box.get_children():
		child.queue_free()
	for i in _steps.size():
		var step: Dictionary = _steps[i]
		var row := HBoxContainer.new()
		var label := Label.new()
		label.text = "%d. [%s] %s" % [i + 1, String(step.get("type", "manual")), String(step.get("title", "(sans titre)"))]
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(label)

		var del := Button.new()
		del.text = "✕"
		del.pressed.connect(func() -> void:
			_steps.remove_at(i)
			_render_steps())
		row.add_child(del)
		_steps_box.add_child(row)


func _on_add_step() -> void:
	# Picks the next step type round-robin so the creator sees the full range
	# without us shipping a modal yet. Swap to a proper picker later.
	var next_type: String = STEP_TYPES[_steps.size() % STEP_TYPES.size()]
	_steps.append({
		"id": "tmp_%d" % Time.get_ticks_msec(),
		"type": next_type,
		"title": "Nouvelle étape",
		"description": "",
		"params": {},
	})
	_render_steps()


func _save(existing: Dictionary) -> void:
	var payload := {
		"id": existing.get("id"),
		"name": _name_edit.text,
		"description": _description_edit.text,
		"steps": _steps,
	}
	set_status("Sauvegarde…")
	var resp: Dictionary = await ApiClient.create_hunt(payload)
	if not resp["ok"]:
		set_status("Erreur: %s" % resp["error"], true)
		return
	set_status("Parcours enregistré.")
	Router.go("creator_home")
