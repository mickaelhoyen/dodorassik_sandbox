extends Node
## Wraps platform-specific capabilities used by the game (GPS, camera,
## Bluetooth scanning). Implementations are stubbed for desktop/dev and
## are expected to be backed by a Godot Android plugin on real devices.
##
## Each method returns a Dictionary with `ok` + result data so callers can
## handle gracefully when a capability is unavailable in the editor.

signal location_updated(lat: float, lon: float, accuracy: float)
signal bluetooth_device_found(name: String, address: String, rssi: int)


func has_gps() -> bool:
	return OS.has_feature("mobile")


func has_camera() -> bool:
	return OS.has_feature("mobile")


func has_bluetooth() -> bool:
	return OS.has_feature("mobile")


func request_location() -> Dictionary:
	if not has_gps():
		# Dev fallback: deterministic fake position in a Paris park.
		var fake := {"lat": 48.8606, "lon": 2.3376, "accuracy": 25.0}
		location_updated.emit(fake["lat"], fake["lon"], fake["accuracy"])
		return {"ok": true, "data": fake, "stub": true}
	# TODO: bridge to Android plugin (FusedLocationProvider).
	return {"ok": false, "error": "not_implemented_native_gps"}


func capture_photo() -> Dictionary:
	if not has_camera():
		return {"ok": false, "error": "no_camera_in_editor", "stub": true}
	# TODO: bridge to Android camera intent / iOS UIImagePicker.
	return {"ok": false, "error": "not_implemented_native_camera"}


func scan_bluetooth(timeout_seconds: float = 8.0) -> Dictionary:
	if not has_bluetooth():
		return {"ok": false, "error": "no_bluetooth_in_editor", "stub": true}
	# TODO: bridge to Android BluetoothLeScanner with the requested timeout.
	_ = timeout_seconds
	return {"ok": false, "error": "not_implemented_native_bluetooth"}


static func haversine_meters(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
	## Great-circle distance between two GPS points, used to validate
	## location-based steps without contacting the server.
	var r := 6371000.0
	var d_lat := deg_to_rad(lat2 - lat1)
	var d_lon := deg_to_rad(lon2 - lon1)
	var a := sin(d_lat / 2.0) ** 2 + cos(deg_to_rad(lat1)) * cos(deg_to_rad(lat2)) * sin(d_lon / 2.0) ** 2
	var c := 2.0 * atan2(sqrt(a), sqrt(1.0 - a))
	return r * c
