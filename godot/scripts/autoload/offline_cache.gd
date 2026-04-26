extends Node
## Local persistence layer for offline play.
##
## A creator/player downloads a hunt while online; the full payload (steps,
## clues, media references) is stored under `user://hunts/<id>.json`. The
## player can then run the hunt with no network — submissions are queued in
## `user://pending_submissions.json` and replayed when ApiClient is reachable.

const HUNTS_DIR := "user://hunts"
const PENDING_PATH := "user://pending_submissions.json"


func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(HUNTS_DIR)


# ---------- Hunts ----------

func save_hunt(hunt: Dictionary) -> bool:
	if not hunt.has("id"):
		return false
	var path := "%s/%s.json" % [HUNTS_DIR, String(hunt["id"])]
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		return false
	f.store_string(JSON.stringify(hunt))
	return true


func load_hunt(hunt_id: String) -> Dictionary:
	var path := "%s/%s.json" % [HUNTS_DIR, hunt_id]
	if not FileAccess.file_exists(path):
		return {}
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return {}
	var parsed: Variant = JSON.parse_string(f.get_as_text())
	return parsed if typeof(parsed) == TYPE_DICTIONARY else {}


func list_local_hunts() -> Array:
	var out: Array = []
	var dir := DirAccess.open(HUNTS_DIR)
	if dir == null:
		return out
	dir.list_dir_begin()
	var name := dir.get_next()
	while name != "":
		if not dir.current_is_dir() and name.ends_with(".json"):
			var hunt := load_hunt(name.get_basename())
			if not hunt.is_empty():
				out.append(hunt)
		name = dir.get_next()
	return out


func delete_hunt(hunt_id: String) -> void:
	var path := "%s/%s.json" % [HUNTS_DIR, hunt_id]
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)


# ---------- Pending submissions queue ----------

func queue_submission(hunt_id: String, step_id: String, payload: Dictionary) -> void:
	var queue := _read_pending()
	queue.append({
		"hunt_id": hunt_id,
		"step_id": step_id,
		"payload": payload,
		"created_at": Time.get_unix_time_from_system(),
	})
	_write_pending(queue)


func pending_count() -> int:
	return _read_pending().size()


func flush_pending() -> int:
	## Replay queued submissions through ApiClient. Returns number sent.
	var queue := _read_pending()
	if queue.is_empty():
		return 0
	var remaining: Array = []
	var sent := 0
	for entry in queue:
		var resp: Dictionary = await ApiClient.submit_step(
			String(entry["hunt_id"]),
			String(entry["step_id"]),
			entry["payload"],
		)
		if resp["ok"]:
			sent += 1
		else:
			remaining.append(entry)
	_write_pending(remaining)
	return sent


func _read_pending() -> Array:
	if not FileAccess.file_exists(PENDING_PATH):
		return []
	var f := FileAccess.open(PENDING_PATH, FileAccess.READ)
	if f == null:
		return []
	var parsed: Variant = JSON.parse_string(f.get_as_text())
	return parsed if typeof(parsed) == TYPE_ARRAY else []


func _write_pending(queue: Array) -> void:
	var f := FileAccess.open(PENDING_PATH, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(queue))
