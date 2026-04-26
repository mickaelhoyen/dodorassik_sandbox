package com.dodorassik.device;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Environment;
import android.provider.MediaStore;
import androidx.core.content.ContextCompat;
import androidx.core.content.FileProvider;
import org.godotengine.godot.Dictionary;
import org.godotengine.godot.plugin.GodotPlugin;

import java.io.File;
import java.io.IOException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

/**
 * Camera capture using ACTION_IMAGE_CAPTURE. The picture is stored in the
 * app's private external folder (`getExternalFilesDir(DIRECTORY_PICTURES)`),
 * which is NOT visible in the user's gallery and is wiped on uninstall.
 *
 * Returns immediately with `{ ok: true, pending: true }`. The actual file
 * path is delivered later via the `photo_captured` signal — GDScript awaits
 * it through `device_services.gd`.
 */
class CameraModule {

    static final int REQUEST_CODE = 0xD0D0; // arbitrary unique-ish

    private final Activity activity;
    private final DodorassikDevice plugin;

    private File pendingFile;

    CameraModule(Activity activity, DodorassikDevice plugin) {
        this.activity = activity;
        this.plugin = plugin;
    }

    Dictionary capture() {
        Dictionary out = new Dictionary();
        if (ContextCompat.checkSelfPermission(activity, Manifest.permission.CAMERA)
                != PackageManager.PERMISSION_GRANTED) {
            out.put("ok", false);
            out.put("error", "permission_denied");
            return out;
        }

        File destination;
        try {
            destination = createUniqueFile();
        } catch (IOException ioe) {
            out.put("ok", false);
            out.put("error", "storage_error");
            return out;
        }

        Uri uri;
        try {
            String authority = activity.getPackageName() + ".fileprovider";
            uri = FileProvider.getUriForFile(activity, authority, destination);
        } catch (IllegalArgumentException iae) {
            out.put("ok", false);
            out.put("error", "fileprovider_misconfigured");
            return out;
        }

        Intent intent = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
        intent.putExtra(MediaStore.EXTRA_OUTPUT, uri);
        intent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION);

        if (intent.resolveActivity(activity.getPackageManager()) == null) {
            out.put("ok", false);
            out.put("error", "no_camera_app");
            return out;
        }

        pendingFile = destination;
        activity.startActivityForResult(intent, REQUEST_CODE);

        out.put("ok", true);
        out.put("pending", true);
        return out;
    }

    /**
     * Called by the plugin's onMainActivityResult forwarder.
     * @return true if the activity result was for this module.
     */
    boolean handleActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode != REQUEST_CODE) return false;
        File f = pendingFile;
        pendingFile = null;
        if (f != null && resultCode == Activity.RESULT_OK && f.exists() && f.length() > 0) {
            plugin.emitPhotoCaptured(f.getAbsolutePath(), f.length());
        } else {
            // Either the user cancelled or the file is empty.
            if (f != null && f.exists()) f.delete();
            plugin.emitPhotoCaptured("", 0L);
        }
        return true;
    }

    private File createUniqueFile() throws IOException {
        File dir = activity.getExternalFilesDir(Environment.DIRECTORY_PICTURES);
        if (dir == null) throw new IOException("no_external_files_dir");
        if (!dir.exists() && !dir.mkdirs()) throw new IOException("mkdirs_failed");
        String timestamp = new SimpleDateFormat("yyyyMMdd_HHmmss", Locale.US).format(new Date());
        return File.createTempFile("hunt_" + timestamp + "_", ".jpg", dir);
    }
}
