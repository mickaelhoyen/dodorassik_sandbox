package com.dodorassik.device;

import android.app.Activity;
import org.godotengine.godot.Dictionary;

import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Bluetooth LE scan, restricted to a whitelist of MAC addresses. Devices
 * whose MAC is not in the whitelist are *never* surfaced to the GDScript
 * layer — protecting the privacy of bystanders' phones and headphones.
 */
class BluetoothModule {

    private final Activity activity;
    private final DodorassikDevice plugin;

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

        // TODO:
        //  1. Acquire BluetoothAdapter, get BluetoothLeScanner.
        //  2. Build ScanFilters from `whitelist` (one filter per MAC).
        //  3. Start scan with low-latency mode.
        //  4. Stop after timeoutMs OR first match.
        //  5. For each match, emit `bluetooth_device_found` AND store the
        //     first one in `out`. Drop everything else silently.
        out.put("ok", false);
        out.put("error", "not_implemented");
        return out;
    }

    private static Set<String> normalise(List<String> raw) {
        Set<String> out = new HashSet<>(raw.size());
        for (String s : raw) {
            if (s != null) out.add(s.trim().toUpperCase());
        }
        return out;
    }
}
