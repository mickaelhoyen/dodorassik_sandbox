extends Node
## Wraps platform-specific capabilities used by the game (GPS, camera,
## Bluetooth scanning). On Android the native plugin `DodorassikDevice`
## (godot/android/plugin/) exposes the calls; on desktop / editor we fall
## back to deterministic stubs so dev iteration doesn't need a phone.
##
## Privacy invariants (see docs/PRIVACY.md):
## - GPS lookups are one-shot, on demand. Never start a location service.
## - Camera shots stay in the OS-private app folder. Upload is opt-in.
## - Bluetooth scans match against an explicit MAC whitelist passed in.

signal location_updated(lat: float, lon: float, accuracy: float)
signal photo_captured(path: String, size_bytes: int)
signal bluetooth_device_found(name: String, address: String, rssi: int)

const PLUGIN_NAME := "DodorassikDevice"

var _plugin: Object = null


func _ready() -> void:
	if Engine.has_singleton(PLUGIN_NAME):
		_plugin = Engine.get_singleton(PLUGIN_NAME)
		if _plugin.has_signal("location_updated"):
			_plugin.connect("location_updated", _on_native_location)
		if _plugin.has_signal("photo_captured"):
			_plugin.connect("photo_captured", _on_native_photo)
		if _plugin.has_signal("bluetooth_device_found"):
			_plugin.connect("bluetooth_device_found", _on_native_bt)


func has_gps() -> bool:
	return _plugin != null or OS.has_feature("mobile")


func has_camera() -> bool:
	return _plugin != null or OS.has_feature("mobile")


func has_bluetooth() -> bool:
	return _plugin != null or OS.has_feature("mobile")


# ---------- Location (synchronous, ≤5s timeout in native) -------------------

func request_location() -> Dictionary:
	if _plugin != null and _plugin.has_method("request_location"):
		var raw: Variant = _plugin.call("request_location")
		if typeof(raw) != TYPE_DICTIONARY:
			return {"ok": false, "error": "native_returned_invalid"}
		var ok: bool = bool(raw.get("ok", false))
		if not ok:
			return {"ok": false, "error": String(raw.get("error", "unknown"))}
		return {
			"ok": true,
			"data": {
				"lat": float(raw["lat"]),
				"lon": float(raw["lon"]),
				"accuracy": float(raw.get("accuracy", 0.0)),
			},
		}

	# Dev fallback: deterministic fake position in a Paris park.
	var fake := {"lat": 48.8606, "lon": 2.3376, "accuracy": 25.0}
	location_updated.emit(fake["lat"], fake["lon"], fake["accuracy"])
	return {"ok": true, "data": fake, "stub": true}


# ---------- Camera (asynchronous via signal) --------------------------------

func capture_photo() -> Dictionary:
	if _plugin != null and _plugin.has_method("capture_photo"):
		var raw: Variant = _plugin.call("capture_photo")
		if typeof(raw) != TYPE_DICTIONARY:
			return {"ok": false, "error": "native_returned_invalid"}
		if not bool(raw.get("ok", false)):
			return {"ok": false, "error": String(raw.get("error", "unknown"))}
		# Native side sent the intent; await the activity result signal.
		var result: Array = await photo_captured
		var path: String = String(result[0])
		var size: int = int(result[1])
		if path.is_empty():
			return {"ok": false, "error": "cancelled"}
		return {"ok": true, "data": {"path": path, "size_bytes": size}}

	return {"ok": false, "error": "no_camera_available", "stub": true}


# ---------- Bluetooth (asynchronous via signal, whitelist enforced) ---------

func scan_bluetooth(allowed_addresses: PackedStringArray, timeout_seconds: float = 8.0) -> Dictionary:
	## Only returns matches against `allowed_addresses` — never leaks info
	## about other nearby devices. Bystanders' phones/headphones cannot
	## reach this layer (the native plugin filters them out).
	if allowed_addresses.is_empty():
		return {"ok": false, "error": "no_whitelist"}

	if _plugin != null and _plugin.has_method("scan_bluetooth"):
		var raw: Variant = _plugin.call("scan_bluetooth", allowed_addresses, timeout_seconds)
		if typeof(raw) != TYPE_DICTIONARY:
			return {"ok": false, "error": "native_returned_invalid"}
		if not bool(raw.get("ok", false)):
			return {"ok": false, "error": String(raw.get("error", "unknown"))}
		# Wait for a matching device or the native timeout sentinel.
		var result: Array = await bluetooth_device_found
		var address: String = String(result[1])
		if address.is_empty():
			return {"ok": false, "error": "timeout"}
		return {
			"ok": true,
			"data": {
				"name": String(result[0]),
				"address": address,
				"rssi": int(result[2]),
			},
		}

	return {"ok": false, "error": "no_bluetooth_available", "stub": true}


# ---------- Native → Godot signal forwarders --------------------------------

func _on_native_location(lat: float, lon: float, accuracy: float) -> void:
	location_updated.emit(lat, lon, accuracy)


func _on_native_photo(path: String, size_bytes: int) -> void:
	photo_captured.emit(path, size_bytes)


func _on_native_bt(name: String, address: String, rssi: int) -> void:
	bluetooth_device_found.emit(name, address, rssi)


# ---------- Pure helpers ---------------------------------------------------

static func haversine_meters(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
	## Great-circle distance between two GPS points, used to validate
	## location-based steps without contacting the server.
	var r := 6371000.0
	var d_lat := deg_to_rad(lat2 - lat1)
	var d_lon := deg_to_rad(lon2 - lon1)
	var a := sin(d_lat / 2.0) ** 2 + cos(deg_to_rad(lat1)) * cos(deg_to_rad(lat2)) * sin(d_lon / 2.0) ** 2
	var c := 2.0 * atan2(sqrt(a), sqrt(1.0 - a))
	return r * c
