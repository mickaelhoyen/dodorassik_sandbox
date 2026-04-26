package com.dodorassik.device;

import android.app.Activity;
import android.util.Base64;
import android.view.View;
import android.webkit.JavascriptInterface;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;

import java.nio.charset.StandardCharsets;

/**
 * Full-screen WebView overlay that renders a Leaflet.js map for placing /
 * adjusting GPS markers on hunt steps. Loaded from the bundled asset
 * {@code map_editor.html} so no external server is required.
 *
 * Communication:
 *   GDScript → loadSteps(json)   → JS initBase64(b64)
 *   GDScript → showMap()         → overlay visible
 *   JS confirm → MapBridge.onConfirm(json) → DodorassikDevice "map_confirmed" signal
 *   JS cancel  → MapBridge.onCancel()      → DodorassikDevice "map_cancelled" signal
 *
 * Privacy: Leaflet fetches OSM tile images over HTTPS (no user location sent
 * to OSM). The module never logs or stores coordinates.
 */
class MapModule {

    private final Activity activity;
    private final DodorassikDevice plugin;

    // Only accessed on the UI thread.
    private WebView webView;
    private FrameLayout overlay;

    MapModule(Activity activity, DodorassikDevice plugin) {
        this.activity = activity;
        this.plugin = plugin;
    }

    void showMap() {
        activity.runOnUiThread(() -> {
            ensureWebView();
            overlay.setVisibility(View.VISIBLE);
        });
    }

    void hideMap() {
        if (overlay == null) return;
        activity.runOnUiThread(() -> overlay.setVisibility(View.GONE));
    }

    /** Load step data into the map before showing. Base64 avoids JS escaping issues. */
    void loadSteps(String stepsJson) {
        String b64 = Base64.encodeToString(
            stepsJson.getBytes(StandardCharsets.UTF_8), Base64.NO_WRAP);
        activity.runOnUiThread(() -> {
            ensureWebView();
            webView.evaluateJavascript("initBase64('" + b64 + "')", null);
        });
    }

    private void ensureWebView() {
        // Called only on the UI thread — no synchronization needed.
        if (webView != null) return;

        webView = new WebView(activity);
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        // Allow file:// origin to load HTTPS tile images from OSM CDN.
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);

        webView.addJavascriptInterface(new MapBridge(), "MapBridge");
        webView.setWebViewClient(new WebViewClient());

        overlay = new FrameLayout(activity);
        overlay.addView(webView, new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.MATCH_PARENT));
        activity.addContentView(overlay, new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.MATCH_PARENT));

        overlay.setVisibility(View.GONE);
        webView.loadUrl("file:///android_asset/map_editor.html");
    }

    private class MapBridge {
        @JavascriptInterface
        public void onConfirm(String resultJson) {
            plugin.emitMapConfirmed(resultJson);
        }

        @JavascriptInterface
        public void onCancel() {
            plugin.emitMapCancelled();
        }
    }
}
