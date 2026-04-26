package com.dodorassik.device;

import android.app.Activity;
import org.godotengine.godot.Dictionary;

/**
 * Camera capture. Returns a path inside the app's private storage; the
 * picture is *not* added to the user's gallery. Upload to the server
 * remains explicit and opt-in (see PRIVACY.md §3).
 */
class CameraModule {

    private final Activity activity;
    private final DodorassikDevice plugin;

    CameraModule(Activity activity, DodorassikDevice plugin) {
        this.activity = activity;
        this.plugin = plugin;
    }

    Dictionary capture() {
        Dictionary out = new Dictionary();
        // TODO:
        //  1. Build an ACTION_IMAGE_CAPTURE intent.
        //  2. Provide a FileProvider Uri pointing at
        //     getExternalFilesDir(Environment.DIRECTORY_PICTURES).
        //  3. Start the activity with startActivityForResult.
        //  4. Wait for the result on the main thread (use a callback bus).
        //  5. Return { ok, path, mimeType, sizeBytes } — *not* the raw Uri.
        out.put("ok", false);
        out.put("error", "not_implemented");
        return out;
    }
}
