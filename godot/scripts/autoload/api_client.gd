extends Node
## Thin async HTTP client wrapping HTTPRequest for the Dodorassik backend.
##
## Calls return a Dictionary with `ok`, `status`, `data`, `error`. The base
## URL is read from `user://config.json` if present, else falls back to
## DEFAULT_BASE_URL. JWT token (when AppState is authenticated) is sent as
## `Authorization: Bearer ...` automatically.

const DEFAULT_BASE_URL := "http://localhost:5080"
const CONFIG_PATH := "user://config.json"

var base_url: String = DEFAULT_BASE_URL


func _ready() -> void:
	_load_config()


func _load_config() -> void:
	if not FileAccess.file_exists(CONFIG_PATH):
		return
	var f := FileAccess.open(CONFIG_PATH, FileAccess.READ)
	if f == null:
		return
	var data: Variant = JSON.parse_string(f.get_as_text())
	if typeof(data) == TYPE_DICTIONARY and data.has("base_url"):
		base_url = String(data["base_url"])


func set_base_url(url: String) -> void:
	base_url = url.rstrip("/")
	var f := FileAccess.open(CONFIG_PATH, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify({"base_url": base_url}))


# ---------- High-level helpers ----------

func login(email: String, password: String) -> Dictionary:
	return await request("POST", "/api/auth/login", {
		"email": email,
		"password": password,
	})


func register(email: String, password: String, display_name: String) -> Dictionary:
	return await request("POST", "/api/auth/register", {
		"email": email,
		"password": password,
		"displayName": display_name,
	})


func list_hunts() -> Dictionary:
	return await request("GET", "/api/hunts")


func get_hunt(hunt_id: String) -> Dictionary:
	return await request("GET", "/api/hunts/%s" % hunt_id)


func create_hunt(payload: Dictionary) -> Dictionary:
	return await request("POST", "/api/hunts", payload)


func submit_step(hunt_id: String, step_id: String, payload: Dictionary) -> Dictionary:
	return await request("POST", "/api/hunts/%s/steps/%s/submit" % [hunt_id, step_id], payload)


func my_family() -> Dictionary:
	return await request("GET", "/api/families/me")


func create_family(name: String) -> Dictionary:
	return await request("POST", "/api/families", {"name": name})


func join_family(family_id: String) -> Dictionary:
	return await request("POST", "/api/families/%s/join" % family_id)


# ---------- Core request ----------

func request(method: String, path: String, body: Variant = null) -> Dictionary:
	var http := HTTPRequest.new()
	add_child(http)
	var headers: PackedStringArray = ["Content-Type: application/json", "Accept: application/json"]
	if AppState.is_authenticated():
		headers.append("Authorization: Bearer %s" % AppState.auth_token)

	var http_method := _method_for(method)
	var body_str := ""
	if body != null:
		body_str = JSON.stringify(body)

	var url := base_url + path
	var err := http.request(url, headers, http_method, body_str)
	if err != OK:
		http.queue_free()
		return {"ok": false, "status": 0, "data": null, "error": "request_failed:%d" % err}

	var result: Array = await http.request_completed
	http.queue_free()

	# Signal payload: result, response_code, headers, body
	var status: int = int(result[1])
	var response_body: PackedByteArray = result[3]
	var text := response_body.get_string_from_utf8()
	var data: Variant = null
	if text.length() > 0:
		data = JSON.parse_string(text)

	var ok := status >= 200 and status < 300
	return {
		"ok": ok,
		"status": status,
		"data": data,
		"error": "" if ok else _extract_error(data, status),
	}


func _method_for(name: String) -> int:
	match name.to_upper():
		"GET": return HTTPClient.METHOD_GET
		"POST": return HTTPClient.METHOD_POST
		"PUT": return HTTPClient.METHOD_PUT
		"PATCH": return HTTPClient.METHOD_PATCH
		"DELETE": return HTTPClient.METHOD_DELETE
		_: return HTTPClient.METHOD_GET


func _extract_error(data: Variant, status: int) -> String:
	if typeof(data) == TYPE_DICTIONARY:
		if data.has("error"):
			return String(data["error"])
		if data.has("title"):
			return String(data["title"])
	return "http_%d" % status
