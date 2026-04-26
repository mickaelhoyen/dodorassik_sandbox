package com.dodorassik.device;

import android.Manifest;
import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothManager;
import android.bluetooth.le.BluetoothLeScanner;
import android.bluetooth.le.ScanCallback;
import android.bluetooth.le.ScanFilter;
import android.bluetooth.le.ScanResult;
import android.bluetooth.le.ScanSettings;
import android.content.Context;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import androidx.core.content.ContextCompat;
import org.godotengine.godot.Dictionary;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Bluetooth LE scan, restricted to a whitelist of MAC addresses. Devices
 * whose MAC is not in the whitelist are *never* surfaced to the GDScript
 * layer — this protects bystanders' phones, headphones, watches, etc.
 *
 * The scan emits `bluetooth_device_found` for every match and stops on the
 * first one (first-match-wins) or after the timeout.
 */
class BluetoothModule {

    private final Activity activity;
    private final DodorassikDevice plugin;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    private BluetoothLeScanner scanner;
    private InternalCallback activeCallback;

    BluetoothModule(Activity activity, DodorassikDevice plugin) {
        this.activity = activity;
        this.plugin = plugin;
    }

    Dictionary scan(List<String> allowedAddresses, long timeoutMs) {
        Dictionary out = new Dictionary();

        Set<String> whitelist = normalise(allowedAddresses);
        if (whitelist.isEmpty()) {
            out.put("ok", false);
            out.put("error", "no_whitelist");
            return out;
        }

        if (!hasScanPermission()) {
            out.put("ok", false);
            out.put("error", "permission_denied");
            return out;
        }

        BluetoothAdapter adapter = getAdapter();
        if (adapter == null || !adapter.isEnabled()) {
            out.put("ok", false);
            out.put("error", "bluetooth_off");
            return out;
        }

        scanner = adapter.getBluetoothLeScanner();
        if (scanner == null) {
            out.put("ok", false);
            out.put("error", "no_le_scanner");
            return out;
        }

        // Stop any previous scan still running.
        stopActive();

        List<ScanFilter> filters = new ArrayList<>(whitelist.size());
        for (String mac : whitelist) {
            filters.add(new ScanFilter.Builder().setDeviceAddress(mac).build());
        }
        ScanSettings settings = new ScanSettings.Builder()
                .setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY)
                .build();

        InternalCallback cb = new InternalCallback(whitelist);
        activeCallback = cb;
        try {
            scanner.startScan(filters, settings, cb);
        } catch (SecurityException se) {
            out.put("ok", false);
            out.put("error", "permission_denied");
            return out;
        }

        // Auto-stop after timeout.
        mainHandler.postDelayed(() -> {
            if (activeCallback == cb) {
                stopActive();
                plugin.emitBluetoothDevice("", "", -1); // sentinel: no device
            }
        }, Math.max(1_000, timeoutMs));

        out.put("ok", true);
        out.put("pending", true);
        return out;
    }

    void stopActive() {
        if (activeCallback != null && scanner != null) {
            try {
                scanner.stopScan(activeCallback);
            } catch (SecurityException ignored) {
                // Permission may have been revoked between start and stop.
            }
        }
        activeCallback = null;
    }

    private boolean hasScanPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            return ContextCompat.checkSelfPermission(activity, Manifest.permission.BLUETOOTH_SCAN)
                    == PackageManager.PERMISSION_GRANTED;
        }
        // Pre-Android 12: legacy BLUETOOTH + ACCESS_FINE_LOCATION is enough.
        return ContextCompat.checkSelfPermission(activity, Manifest.permission.ACCESS_FINE_LOCATION)
                == PackageManager.PERMISSION_GRANTED;
    }

    private BluetoothAdapter getAdapter() {
        BluetoothManager bm = (BluetoothManager) activity.getSystemService(Context.BLUETOOTH_SERVICE);
        return bm == null ? null : bm.getAdapter();
    }

    private static Set<String> normalise(List<String> raw) {
        Set<String> out = new HashSet<>(raw.size());
        for (String s : raw) {
            if (s != null) {
                String trimmed = s.trim().toUpperCase();
                if (!trimmed.isEmpty()) out.add(trimmed);
            }
        }
        return out;
    }

    /**
     * Receiving callback. Filters matches against the whitelist a second
     * time as belt-and-braces (the OS-level filter should already do it).
     * Emits the first match and stops the scan.
     */
    private class InternalCallback extends ScanCallback {
        private final Set<String> whitelist;

        InternalCallback(Set<String> whitelist) {
            this.whitelist = whitelist;
        }

        @Override
        public void onScanResult(int callbackType, ScanResult result) {
            String mac = result.getDevice() != null ? result.getDevice().getAddress() : null;
            if (mac == null) return;
            if (!whitelist.contains(mac.toUpperCase())) return;

            // Device name may be unavailable in BLE; pass empty string then.
            String name = "";
            try {
                String n = result.getDevice().getName();
                if (n != null) name = n;
            } catch (SecurityException ignored) {
                // CONNECT permission may not be granted; we don't need the name.
            }
            int rssi = result.getRssi();

            stopActive();
            plugin.emitBluetoothDevice(name, mac, rssi);
        }

        @Override
        public void onScanFailed(int errorCode) {
            stopActive();
            plugin.emitBluetoothDevice("", "", -1);
        }
    }
}
