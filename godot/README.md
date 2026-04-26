# Client Godot — Dodorassik

Projet Godot 4 (GDScript). Cible principale : Android (téléphone).

## Lancer en local

1. Installer Godot 4.3+
2. Ouvrir ce dossier dans Godot
3. F5 (lance `scenes/common/main.tscn`)

## Organisation

```
godot/
├── project.godot                 # Config Godot, autoloads, écran principal
├── scenes/common/main.tscn       # Scène racine — héberge le ScreenHost
└── scripts/
    ├── autoload/
    │   ├── app_state.gd          # Session, rôle, mode online/offline
    │   ├── api_client.gd         # HTTP REST avec JWT auto
    │   ├── offline_cache.gd      # Persistance des chasses & file pending
    │   ├── router.gd             # Navigation entre écrans
    │   └── device_services.gd    # GPS / caméra / Bluetooth (stubs en éditeur)
    └── ui/
        ├── main.gd               # Entry point, branche le Router
        ├── base_screen.gd        # Helpers (titre, boutons, status)
        ├── role_selection_screen.gd
        ├── login_screen.gd
        ├── super_admin_home.gd
        ├── creator_home.gd
        ├── hunt_editor.gd
        ├── player_home.gd
        └── hunt_runner.gd
```

## Configuration de l'URL serveur

Par défaut, `ApiClient` pointe sur `http://localhost:5080`. Pour changer :

```gdscript
ApiClient.set_base_url("https://api.dodorassik.example.com")
```

La valeur est persistée dans `user://config.json`.

## Export Android

Le squelette ne fournit pas encore de presets d'export — voir
[`docs/ROADMAP.md`](../docs/ROADMAP.md) phase 2. Penser aux permissions
`ACCESS_FINE_LOCATION`, `CAMERA`, `BLUETOOTH_SCAN`, `BLUETOOTH_CONNECT`.
