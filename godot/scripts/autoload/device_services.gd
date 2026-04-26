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
signal bluetooth_device_found(name: String, address: String, rssi: int)

const PLUGIN_NAME := "DodorassikDevice"

var _plugin: Object = null


func _ready() -> void:
	if Engine.has_singleton(PLUGIN_NAME):
		_plugin = Engine.get_singleton(PLUGIN_NAME)
		if _plugin.has_signal("location_updated"):
			_plugin.connect("location_updated", _on_native_location)
		if _plugin.has_signal("bluetooth_device_found"):
			_plugin.connect("bluetooth_device_found", _on_native_bt)


func has_gps() -> bool:
	return _plugin != null or OS.has_feature("mobile")


func has_camera() -> bool:
	return _plugin != null or OS.has_feature("mobile")


func has_bluetooth() -> bool:
	return _plugin != null or OS.has_feature("mobile")


func request_location() -> Dictionary:
	if _plugin != null and _plugin.has_method("request_location"):
		var raw: Variant = _plugin.call("request_location")
		if typeof(raw) == TYPE_DICTIONARY and raw.has("lat"):
			return {"ok": true, "data": raw}
		return {"ok": false, "error": "native_returned_invalid"}

	# Dev fallback: deterministic fake position in a Paris park.
	var fake := {"lat": 48.8606, "lon": 2.3376, "accuracy": 25.0}
	location_updated.emit(fake["lat"], fake["lon"], fake["accuracy"])
	return {"ok": true, "data": fake, "stub": true}


func capture_photo() -> Dictionary:
	if _plugin != null and _plugin.has_method("capture_photo"):
		var raw: Variant = _plugin.call("capture_photo")
		if typeof(raw) == TYPE_DICTIONARY:
			return {"ok": raw.get("ok", false), "data": raw}
	return {"ok": false, "error": "no_camera_available", "stub": true}


func scan_bluetooth(allowed_addresses: PackedStringArray, timeout_seconds: float = 8.0) -> Dictionary:
	## Only returns matches against `allowed_addresses` — never leaks info
	## about other nearby devices.
	if _plugin != null and _plugin.has_method("scan_bluetooth"):
		var raw: Variant = _plugin.call("scan_bluetooth", allowed_addresses, timeout_seconds)
		if typeof(raw) == TYPE_DICTIONARY:
			return {"ok": raw.get("ok", false), "data": raw}
	return {"ok": false, "error": "no_bluetooth_available", "stub": true}


func _on_native_location(lat: float, lon: float, accuracy: float) -> void:
	location_updated.emit(lat, lon, accuracy)


func _on_native_bt(name: String, address: String, rssi: int) -> void:
	bluetooth_device_found.emit(name, address, rssi)


static func haversine_meters(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
	## Great-circle distance between two GPS points, used to validate
	## location-based steps without contacting the server.
	var r := 6371000.0
	var d_lat := deg_to_rad(lat2 - lat1)
	var d_lon := deg_to_rad(lon2 - lon1)
	var a := sin(d_lat / 2.0) ** 2 + cos(deg_to_rad(lat1)) * cos(deg_to_rad(lat2)) * sin(d_lon / 2.0) ** 2
	var c := 2.0 * atan2(sqrt(a), sqrt(1.0 - a))
	return r * c
