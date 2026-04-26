# Keep the plugin entry point and modules — Godot reflects on them.
-keep class com.dodorassik.device.** { *; }

# Play Services Location uses generated classes; preserve them.
-keep class com.google.android.gms.location.** { *; }
