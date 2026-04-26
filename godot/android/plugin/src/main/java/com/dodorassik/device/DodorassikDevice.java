package com.dodorassik.device;

import android.app.Activity;
import android.content.Intent;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import org.godotengine.godot.Godot;
import org.godotengine.godot.plugin.GodotPlugin;
import org.godotengine.godot.plugin.SignalInfo;
import org.godotengine.godot.plugin.UsedByGodot;

import java.util.Arrays;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import org.godotengine.godot.Dictionary;

/**
 * Godot 4.6 Android plugin entry point. Routes calls from GDScript to the
 * narrow modules below. Each public method returns a Godot Dictionary so
 * the GDScript wrapper in `device_services.gd` can stay symmetrical with
 * the dev stubs.
 *
 * Privacy invariants enforced here (see docs/PRIVACY.md):
 *   - Location: one-shot. No background service, no callback registration
 *     beyond a single fix.
 *   - Bluetooth: scan results not matching the caller's whitelist are
 *     dropped before reaching Godot.
 *   - Camera: photo path is not logged. The Uri is exposed via FileProvider
 *     so other apps cannot guess filesystem paths.
 */
public class DodorassikDevice extends GodotPlugin {

    private final LocationModule location;
    private final CameraModule camera;
    private final BluetoothModule bluetooth;
    private final MapModule map;

    public DodorassikDevice(Godot godot) {
        super(godot);
        Activity activity = godot.getActivity();
        location = new LocationModule(activity, this);
        camera = new CameraModule(activity, this);
        bluetooth = new BluetoothModule(activity, this);
        map = new MapModule(activity, this);
    }

    @NonNull
    @Override
    public String getPluginName() {
        return "DodorassikDevice";
    }

    @NonNull
    @Override
    public Set<SignalInfo> getPluginSignals() {
        Set<SignalInfo> signals = new HashSet<>();
        signals.add(new SignalInfo("location_updated", Double.class, Double.class, Double.class));
        signals.add(new SignalInfo("photo_captured", String.class, Long.class));
        signals.add(new SignalInfo("bluetooth_device_found", String.class, String.class, Integer.class));
        signals.add(new SignalInfo("map_confirmed", String.class));
        signals.add(new SignalInfo("map_cancelled"));
        return signals;
    }

    @Override
    public void onMainActivityResult(int requestCode, int resultCode, @Nullable Intent data) {
        super.onMainActivityResult(requestCode, resultCode, data);
        camera.handleActivityResult(requestCode, resultCode, data);
    }

    // ---------- GDScript entry points ----------

    @UsedByGodot
    public Dictionary request_location() {
        return location.requestOneShot();
    }

    @UsedByGodot
    public Dictionary capture_photo() {
        return camera.capture();
    }

    @UsedByGodot
    public Dictionary scan_bluetooth(@Nullable String[] allowedAddresses, double timeoutSeconds) {
        List<String> whitelist = allowedAddresses == null ? List.of() : Arrays.asList(allowedAddresses);
        return bluetooth.scan(whitelist, (long) (timeoutSeconds * 1000));
    }

    @UsedByGodot
    public void show_map() {
        map.showMap();
    }

    @UsedByGodot
    public void hide_map() {
        map.hideMap();
    }

    @UsedByGodot
    public void load_map_steps(String stepsJson) {
        map.loadSteps(stepsJson);
    }

    // ---------- Internal helpers (called by modules) ----------

    void emitLocation(double lat, double lon, double accuracy) {
        emitSignal("location_updated", lat, lon, accuracy);
    }

    void emitPhotoCaptured(String absolutePath, long sizeBytes) {
        emitSignal("photo_captured", absolutePath, sizeBytes);
    }

    void emitBluetoothDevice(String name, String address, int rssi) {
        emitSignal("bluetooth_device_found", name, address, rssi);
    }

    void emitMapConfirmed(String resultJson) {
        emitSignal("map_confirmed", resultJson);
    }

    void emitMapCancelled() {
        emitSignal("map_cancelled");
    }
}
