extends Node
## Locale management. Supported locales: "fr" (default) and "en".
## The chosen locale is persisted in user://config.json alongside the API URL.

const DEFAULT_LOCALE := "fr"
const SUPPORTED := ["fr", "en"]
const CONFIG_PATH := "user://config.json"

var current_locale: String = DEFAULT_LOCALE


func _ready() -> void:
	_load_locale()
	TranslationServer.set_locale(current_locale)


func set_locale(locale: String) -> void:
	if locale not in SUPPORTED:
		return
	current_locale = locale
	TranslationServer.set_locale(locale)
	_save_locale()


func _load_locale() -> void:
	if not FileAccess.file_exists(CONFIG_PATH):
		return
	var f := FileAccess.open(CONFIG_PATH, FileAccess.READ)
	if f == null:
		return
	var data: Variant = JSON.parse_string(f.get_as_text())
	if typeof(data) == TYPE_DICTIONARY and data.has("locale"):
		var saved: String = String(data["locale"])
		if saved in SUPPORTED:
			current_locale = saved


func _save_locale() -> void:
	var data: Dictionary = {}
	if FileAccess.file_exists(CONFIG_PATH):
		var f := FileAccess.open(CONFIG_PATH, FileAccess.READ)
		if f != null:
			var parsed: Variant = JSON.parse_string(f.get_as_text())
			if typeof(parsed) == TYPE_DICTIONARY:
				data = parsed
	data["locale"] = current_locale
	var fw := FileAccess.open(CONFIG_PATH, FileAccess.WRITE)
	if fw != null:
		fw.store_string(JSON.stringify(data))
