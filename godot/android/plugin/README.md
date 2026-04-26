# Plugin Android — DodorassikDevice

Plugin Godot 4.6 (Android Editor Plugin) qui expose au moteur :

- **GPS** (FusedLocationProvider) — lecture *one-shot*, jamais en arrière-plan.
- **Caméra** — capture une image et la stocke dans le dossier privé de l'app.
- **Bluetooth LE** — scan limité à une *whitelist d'adresses MAC* fournie par
  l'appel ; les autres appareils ne sont jamais retournés au moteur.

Les invariants Privacy (`docs/PRIVACY.md`) sont implémentés *au niveau du
plugin*, pas seulement côté GDScript. Aucune donnée ne sort du téléphone.

## Structure

```
godot/android/plugin/
├── DodorassikDevice.gdap            # Manifeste lu par l'export Godot
├── src/main/java/com/dodorassik/device/
│   ├── DodorassikDevice.java        # Classe principale (singleton)
│   ├── LocationModule.java          # Wrapper FusedLocationClient
│   ├── CameraModule.java            # Intent ACTION_IMAGE_CAPTURE
│   └── BluetoothModule.java         # BluetoothLeScanner avec whitelist
└── src/main/AndroidManifest.xml     # Permissions runtime requises
```

## Pré-requis de build

- Android Studio (Giraffe ou +) ou ligne de commande avec :
  - `ANDROID_HOME` configuré
  - SDK 34, Build Tools 34.x, NDK r25c+
- Godot 4.6 installé localement (récupère `godot-lib.x.x.x.release.aar`)
- Java 17

## Compiler

```bash
cd godot/android/plugin
./gradlew assembleRelease
# Sortie : build/outputs/aar/dodorassik-device-release.aar
```

Copier ensuite l'AAR + le `DodorassikDevice.gdap` dans
`godot/android/plugins/` (créer le dossier si besoin), puis dans Godot
Editor → Project → Export → Android → Plugins, cocher "DodorassikDevice".

## Côté GDScript

Une fois exporté avec le plugin actif :

```gdscript
if Engine.has_singleton("DodorassikDevice"):
    var d := Engine.get_singleton("DodorassikDevice")
    var loc := d.request_location()  # → { lat, lon, accuracy, ok }
```

`autoload/device_services.gd` détecte automatiquement le singleton et
bascule du stub dev vers la vraie implémentation.

## Sécurité / Privacy

- Permissions runtime demandées **juste avant** l'usage, jamais au lancement.
- `CameraModule` utilise `FileProvider` pour ne pas exposer le path
  filesystem dans les intents.
- `BluetoothModule.scan(allowedAddresses, timeoutMs)` filtre côté natif :
  les autres MAC ne sont jamais émises sur le bus de signaux.
- Aucun log natif n'inclut de coordonnées GPS, MAC d'appareil, ou nom de
  fichier photo. Logs : niveaux de retour seulement (`OK`, `denied`,
  `timeout`).
- Aucun network access depuis le plugin (déclaré dans l'AndroidManifest).

## Status

Squelette uniquement (phase 2). L'implémentation native est documentée
mais non compilable en l'état (manque le wiring Gradle et godot-lib).
À compléter dans la phase 2 (cf. `docs/ROADMAP.md`).
