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

## Pré-requis

| Outil | Version minimale |
|-------|-----------------|
| Android Studio | Giraffe (2022.3) ou supérieur |
| Android SDK | API 34 |
| Android Build Tools | 34.x |
| NDK | r25c |
| Java | 17 |
| Godot | 4.6 stable |

Variable d'environnement requise : `ANDROID_HOME` pointant vers le SDK.

## Étape 1 — Récupérer godot-lib

Le plugin s'appuie sur `godot-lib.4.6.stable.release.aar` fourni par Godot.

```bash
# Télécharger depuis le miroir officiel ou depuis votre installation Godot :
# Linux / macOS :
find ~/.local/share/godot -name "godot-lib.*.release.aar" 2>/dev/null | head -1
# Windows :
# %APPDATA%\Godot\export_templates\4.6.stable\android\...

# Copier dans le dossier libs/ du plugin :
cp godot-lib.4.6.stable.release.aar godot/android/plugin/libs/
```

Si vous n'avez pas les export templates installés, téléchargez-les depuis
`Editor → Export Templates → Download` dans l'éditeur Godot 4.6.

## Étape 2 — Configurer Gradle

Créez `godot/android/plugin/build.gradle` (s'il n'existe pas) :

```groovy
plugins {
    id 'com.android.library'
}

android {
    compileSdk 34
    defaultConfig {
        minSdk 24
        targetSdk 34
    }
    namespace 'com.dodorassik.device'
}

dependencies {
    compileOnly fileTree(dir: 'libs', include: ['godot-lib.*.release.aar'])
    implementation 'com.google.android.gms:play-services-location:21.2.0'
}
```

Et `godot/android/plugin/settings.gradle` :

```groovy
rootProject.name = 'DodorassikDevice'
```

## Étape 3 — Compiler l'AAR (debug)

```bash
cd godot/android/plugin
./gradlew assembleDebug
# Sortie : build/outputs/aar/dodorassik-device-debug.aar
```

Pour la release :

```bash
./gradlew assembleRelease
# Sortie : build/outputs/aar/dodorassik-device-release.aar
```

## Étape 4 — Intégrer dans le projet Godot

```bash
# Créer le dossier d'accueil des plugins si besoin
mkdir -p godot/android/plugins

# Copier l'AAR compilé + le manifeste .gdap
cp build/outputs/aar/dodorassik-device-debug.aar ../plugins/dodorassik-device.aar
cp DodorassikDevice.gdap ../plugins/
```

Dans Godot Editor :  
**Project → Export → Android → Plugins** → cocher **DodorassikDevice**.

## Étape 5 — Keystore de debug et build APK signé (debug)

Godot 4.6 nécessite un keystore même pour les builds debug. Utilisez celui
généré automatiquement par Android Studio ou créez-en un :

```bash
# Générer un keystore debug dédié à Dodorassik (valable 10 ans)
keytool -genkey -v \
  -keystore ~/.android/dodorassik-debug.keystore \
  -alias dodorassik-debug \
  -keyalg RSA \
  -keysize 2048 \
  -validity 3650 \
  -storepass android \
  -keypass android \
  -dname "CN=Dodorassik Debug, O=Dodorassik, C=FR"
```

Dans Godot Editor, configurez le preset d'export Android :

| Champ | Valeur |
|-------|--------|
| **Keystore (debug)** | `~/.android/dodorassik-debug.keystore` |
| **Keystore password** | `android` |
| **Key alias** | `dodorassik-debug` |
| **Key password** | `android` |
| **Min SDK** | 24 (Android 7.0) |
| **Target SDK** | 34 (Android 14) |

**⚠ Ne commitez jamais le fichier `.keystore` de production dans le repo.**  
Le keystore debug ci-dessus est public par convention ; un keystore de
production doit être stocké dans un coffre (GitHub Secrets, Azure Key Vault…).

Lancer le build depuis Godot : **Project → Export → Android → Export Project**  
ou en ligne de commande :

```bash
godot --headless --export-debug "Android" bin/dodorassik-debug.apk
```

## Étape 6 — Installer sur appareil

```bash
# Vérifier l'appareil connecté (USB debugging activé)
adb devices

# Installer
adb install bin/dodorassik-debug.apk

# Voir les logs filtés
adb logcat -s Godot:V DodorassikDevice:V
```

## Côté GDScript

Une fois exporté avec le plugin actif :

```gdscript
if Engine.has_singleton("DodorassikDevice"):
    var d := Engine.get_singleton("DodorassikDevice")
    var loc: Dictionary = d.request_location()  # → { lat, lon, accuracy, ok }
    var photo: Dictionary = d.capture_photo()    # → { path, ok }
    var bt: Dictionary = d.scan_bluetooth(["AA:BB:CC:DD:EE:FF"], 5000)  # → { address, rssi, ok }
```

`autoload/device_services.gd` détecte automatiquement le singleton et
bascule du stub de développement vers la vraie implémentation.

## Sécurité / Privacy

- Permissions runtime demandées **juste avant** l'usage, jamais au lancement.
- `CameraModule` utilise `FileProvider` pour ne pas exposer le path
  filesystem dans les intents.
- `BluetoothModule.scan(allowedAddresses, timeoutMs)` filtre côté natif :
  les autres MAC ne sont jamais émises sur le bus de signaux.
- Aucun log natif n'inclut de coordonnées GPS, MAC d'appareil, ou chemin de
  fichier photo. Les logs ne contiennent que des niveaux de retour (`OK`,
  `denied`, `timeout`).
- Aucun accès réseau depuis le plugin (déclaré dans l'AndroidManifest).

## Statut

Squelette fonctionnel (phase 2) — l'implémentation Java appelle les bonnes
APIs Android. Le build natif nécessite un poste développeur avec Android
Studio et un appareil physique pour tester GPS et Bluetooth.  
Voir `docs/ROADMAP.md` phase 2 pour le suivi.
