package com.dodorassik.device;

import android.Manifest;
import android.app.Activity;
import android.content.pm.PackageManager;
import androidx.core.content.ContextCompat;
import org.godotengine.godot.Dictionary;

/**
 * One-shot GPS lookup. Never starts a foreground/background service.
 * Permission is checked but not requested here — the GDScript caller is
 * expected to ask via the Godot permission API right before invocation.
 */
class LocationModule {

    private final Activity activity;
    private final DodorassikDevice plugin;

    LocationModule(Activity activity, DodorassikDevice plugin) {
        this.activity = activity;
        this.plugin = plugin;
    }

    Dictionary requestOneShot() {
        Dictionary out = new Dictionary();
        if (ContextCompat.checkSelfPermission(activity, Manifest.permission.ACCESS_FINE_LOCATION)
                != PackageManager.PERMISSION_GRANTED) {
            out.put("ok", false);
            out.put("error", "permission_denied");
            return out;
        }
        // TODO: integrate FusedLocationProviderClient.getCurrentLocation()
        // and resolve the returned Task synchronously on a background
        // thread, then post the result to the Godot main thread via
        // plugin.emitLocation(...).
        out.put("ok", false);
        out.put("error", "not_implemented");
        return out;
    }
}
