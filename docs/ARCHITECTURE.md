# Architecture

## Vue d'ensemble

```
+-----------------------------+
|        Téléphone famille    |
|  Godot 4 (Android / iOS)    |
|                             |
|  ┌──────────┐ ┌──────────┐  |
|  │  Joueur  │ │ Créateur │  |
|  └──────────┘ └──────────┘  |
|        ┌────────────┐       |
|        │ SuperAdmin │       |
|        └────────────┘       |
|        AppState             |
|        ApiClient ─────┐     |
|        OfflineCache   │     |
|        DeviceServices │     |
+───────────────────────│─────+
                        │ HTTPS / JWT
                        ▼
            +────────────────────+
            |   ASP.NET Core 8   |
            |   Dodorassik.Api   |
            +────────────────────+
                        │
                        ▼
            +────────────────────+
            |   PostgreSQL 16    |
            +────────────────────+
```

## Trois interfaces, un seul binaire

L'application Godot expose trois rôles. Le rôle est choisi sur l'écran
`role_selection`. Joueur entre directement (la session reste anonyme par
défaut, plusieurs personnes d'une famille peuvent partager le même téléphone).
Créateur et Super-administrateur passent par `login_screen` et reçoivent un
JWT signé par le serveur.

| Interface          | Public                | Auth requise | Écrans principaux |
|--------------------|-----------------------|--------------|-------------------|
| Joueur             | Famille en sortie     | Non          | `player_home`, `hunt_runner` |
| Créateur           | Animateur, parent     | Oui (Creator) | `creator_home`, `hunt_editor` |
| Super-administrateur | Équipe Dodorassik    | Oui (SuperAdmin) | `super_admin_home` |

## Modèle domaine

```
User ─┬─ FamilyId ──> Family ─< HuntScore >── Hunt ─┬── HuntStep ─< StepSubmission
      │                                              ├── Clue
      └─ Role { Player, Creator, SuperAdmin }        └── Status { Draft, Published, Archived }
```

- `Hunt` : un parcours, une histoire, plusieurs étapes ordonnées.
- `HuntStep` : un défi avec un `StepType` (`manual`, `location`, `photo`,
  `bluetooth`, `text_answer`, `clue_collect`). Les paramètres (rayon GPS,
  réponse attendue, MAC du beacon, etc.) sont stockés en `jsonb` dans
  `ParamsJson` — on évite les migrations à chaque nouveau type de jeu.
- `Clue` : indice physique (carte, objet) avec un code court à saisir.
- `StepSubmission` : résultat envoyé par le téléphone. Le timestamp client
  fait foi en mode compétitif (sinon les soumissions différées rejouées via
  la file offline tricheraient).
- `HuntScore` : agrégat par famille, recalculé à chaque soumission.

## Mode connecté vs hors ligne

Le client garde une dichotomie claire :

1. **En ligne** — `ApiClient` parle au serveur, lectures et écritures
   directes. C'est le mode utilisé pour les chasses géantes synchronisées
   (timing, classement, plusieurs familles).
2. **Hors ligne** — `OfflineCache` :
   - télécharge la chasse complète (`save_hunt`) avant la sortie ;
   - laisse le `hunt_runner` valider localement (GPS via `DeviceServices`,
     code écrit en dur dans le `params` côté étape) ;
   - empile les soumissions dans `pending_submissions.json` ;
   - rejoue la file dès que `flush_pending()` est appelé en ligne.

Le bouton de bascule live sur l'écran de sélection. `AppState.online` est la
seule source de vérité côté UI.

## Services natifs (`DeviceServices`)

Wrapper Godot autour des capacités plateforme :

- GPS (FusedLocationProvider sur Android, CoreLocation sur iOS)
- Caméra (intent / picker natif)
- Scan Bluetooth LE (recherche de beacons placés sur place par le créateur)

Le wrapper renvoie toujours `{ ok, data, error }` et expose des stubs
déterministes en édition pour qu'on puisse itérer sans téléphone.

## Sécurité

- Mots de passe : PBKDF2-HMAC-SHA256, 100 000 itérations, sel 16 octets,
  format `v1$iter$salt$hash`.
- Tokens : JWT HS256, durée 7 jours par défaut. Secret minimum 32
  caractères, contrôlé au démarrage.
- CORS ouvert en dev — à restreindre avant prod.
- Le client Godot envoie `Authorization: Bearer <token>` automatiquement
  quand `AppState.is_authenticated()`.

## Évolutions prévues

Voir [`ROADMAP.md`](ROADMAP.md).
