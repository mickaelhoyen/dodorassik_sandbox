package com.dodorassik.device;

import android.Manifest;
import android.app.Activity;
import android.content.pm.PackageManager;
import android.location.Location;
import androidx.core.content.ContextCompat;
import com.google.android.gms.location.FusedLocationProviderClient;
import com.google.android.gms.location.LocationServices;
import com.google.android.gms.location.Priority;
import com.google.android.gms.tasks.CancellationTokenSource;
import org.godotengine.godot.Dictionary;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/**
 * One-shot GPS lookup. Never starts a foreground/background service.
 *
 * Privacy invariants:
 *   - No callback registered beyond a single fix.
 *   - Coordinates returned to GDScript and emitted via signal exactly once.
 *   - Native log lines never include the resolved coordinates — only the
 *     status (ok / denied / timeout / no_provider).
 */
class LocationModule {

    private static final long TIMEOUT_MS = 5_000;

    private final Activity activity;
    private final DodorassikDevice plugin;
    private final FusedLocationProviderClient client;

    LocationModule(Activity activity, DodorassikDevice plugin) {
        this.activity = activity;
        this.plugin = plugin;
        this.client = LocationServices.getFusedLocationProviderClient(activity);
    }

    Dictionary requestOneShot() {
        Dictionary out = new Dictionary();
        if (!hasFinePermission()) {
            out.put("ok", false);
            out.put("error", "permission_denied");
            return out;
        }

        final CountDownLatch latch = new CountDownLatch(1);
        final double[] coords = new double[3];
        final boolean[] success = {false};
        final String[] errorOut = {"timeout"};
        final CancellationTokenSource cancel = new CancellationTokenSource();

        try {
            client.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, cancel.getToken())
                    .addOnSuccessListener(activity, (Location loc) -> {
                        if (loc != null) {
                            coords[0] = loc.getLatitude();
                            coords[1] = loc.getLongitude();
                            coords[2] = loc.getAccuracy();
                            success[0] = true;
                        } else {
                            errorOut[0] = "no_fix";
                        }
                        latch.countDown();
                    })
                    .addOnFailureListener(activity, e -> {
                        errorOut[0] = "provider_error";
                        latch.countDown();
                    });
        } catch (SecurityException se) {
            // Permission revoked between check and call.
            out.put("ok", false);
            out.put("error", "permission_denied");
            return out;
        }

        boolean completed = false;
        try {
            completed = latch.await(TIMEOUT_MS, TimeUnit.MILLISECONDS);
        } catch (InterruptedException ie) {
            Thread.currentThread().interrupt();
        }
        if (!completed) {
            cancel.cancel();
            out.put("ok", false);
            out.put("error", "timeout");
            return out;
        }
        if (!success[0]) {
            out.put("ok", false);
            out.put("error", errorOut[0]);
            return out;
        }

        out.put("ok", true);
        out.put("lat", coords[0]);
        out.put("lon", coords[1]);
        out.put("accuracy", coords[2]);

        plugin.emitLocation(coords[0], coords[1], coords[2]);
        return out;
    }

    private boolean hasFinePermission() {
        return ContextCompat.checkSelfPermission(activity, Manifest.permission.ACCESS_FINE_LOCATION)
                == PackageManager.PERMISSION_GRANTED
                || ContextCompat.checkSelfPermission(activity, Manifest.permission.ACCESS_COARSE_LOCATION)
                == PackageManager.PERMISSION_GRANTED;
    }
}
