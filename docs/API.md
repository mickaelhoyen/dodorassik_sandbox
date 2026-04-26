# API REST

Toutes les routes sont préfixées par `/api`. La sérialisation est JSON
(`Content-Type: application/json`). Les endpoints protégés exigent
`Authorization: Bearer <jwt>`.

## Auth

### `POST /api/auth/register`

Crée un compte joueur (par défaut). Le rôle est promu côté super-admin.

```json
{ "email": "alice@example.com", "password": "********", "displayName": "Alice" }
```

Réponse `200`:

```json
{
  "token": "eyJhbGciOi...",
  "user": { "id": "...", "email": "...", "displayName": "...", "role": "player" }
}
```

### `POST /api/auth/login`

Mêmes corps de réponse que `register`. `401` si identifiants invalides.

## Hunts

### `GET /api/hunts`

Liste les chasses. Filtre optionnel `?status=published`. Public.

### `GET /api/hunts/{id}`

Détail d'une chasse, incluant ses `steps` (triées par `order`).

### `POST /api/hunts` *(role: Creator | SuperAdmin)*

```json
{
  "name": "Chasse au parc Monceau",
  "description": "Pour les 6-10 ans",
  "mode": "relaxed",
  "steps": [
    {
      "title": "Trouver la grande statue",
      "type": "location",
      "params": { "lat": 48.8794, "lon": 2.3094, "radius_m": 25 },
      "points": 10
    },
    {
      "title": "Photo du lac",
      "type": "photo",
      "points": 5
    }
  ]
}
```

### `POST /api/hunts/{id}/publish` *(role: Creator | SuperAdmin)*

Bascule le statut à `published`. `204` si OK.

### `POST /api/hunts/{huntId}/steps/{stepId}/submit` *(authentifié)*

Le client envoie le résultat de validation d'une étape :

```json
{ "payload": { "lat": 48.8794, "lon": 2.3095, "distance_m": 12.4 } }
```

Réponse `200`:

```json
{ "accepted": true, "awardedPoints": 10, "message": null }
```

## Users (RGPD)

### `GET /api/users/me` *(authentifié)*
Retourne le profil de l'utilisateur courant.

### `PATCH /api/users/me` *(authentifié)*
Met à jour `displayName` et/ou `email`.

### `GET /api/users/me/export` *(authentifié)*
Portabilité : JSON complet des données concernant le compte.

### `DELETE /api/users/me?confirm=yes` *(authentifié)*
Effacement RGPD. Anonymise les soumissions et hunts du créateur, supprime
le compte. La query `confirm=yes` est obligatoire.

## Families

### `GET /api/families/me` *(authentifié)*
Retourne la famille de l'utilisateur ou `404`.

### `POST /api/families` *(authentifié)*
Crée une famille et y rattache l'utilisateur.

```json
{ "name": "Les Aventuriers" }
```

### `POST /api/families/{id}/join` *(authentifié)*
Rejoint une famille existante via son identifiant.

### `POST /api/families/leave` *(authentifié)*
Quitte la famille courante.

## Health

### `GET /api/health`

```json
{ "status": "ok", "utc": "2026-04-26T12:34:56Z" }
```

## Erreurs

Format minimal :

```json
{ "error": "invalid_credentials" }
```

| Code | Sens                             |
|------|----------------------------------|
| `invalid_input` | Champs manquants ou invalides |
| `email_taken`   | Email déjà utilisé            |
| `invalid_credentials` | Login refusé             |
