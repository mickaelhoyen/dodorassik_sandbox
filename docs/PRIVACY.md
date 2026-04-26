# Vie privée — Dodorassik

> Public cible incluant **des enfants**. Cadre légal : RGPD (UE) avec
> prudence renforcée (art. 8 RGPD, article 6.1 sur l'âge du consentement
> numérique — 15 ans en France). Application à valeur de minor-protective
> par défaut.

## 1. Ce qu'on collecte

| Donnée | Sujet | Usage | Conservation |
|--------|-------|-------|--------------|
| Email | Adulte créateur/super-admin | Authentification, communications service | Tant que le compte existe |
| Display name | Adulte | Affichage dans l'UI | Idem |
| Mot de passe (hash) | Adulte | Authentification | Idem |
| Family name | Adulte | Regroupement, scoring | Idem |
| Hunts créées | Adulte | Contenu de la plateforme | Indéfini si publié, supprimé sur demande |
| Step submissions | Adulte connecté | Scoring, classement | 13 mois après dernière activité, puis anonymisation |
| Adresse IP (hashée) | Adulte | Anti-abus, rate limit | 7 jours |

## 2. Ce qu'on **ne collecte pas**

- Aucune donnée d'enfant : pas de nom, pas de date de naissance, pas de
  visage, pas d'école, pas de coordonnées personnelles.
- Pas de carnet d'adresses, pas de contacts du téléphone.
- Pas de trace GPS continue. Le GPS est lu **uniquement** lors de la
  validation explicite d'une étape de type `location`, et la coordonnée
  envoyée au serveur est *celle du joueur au moment du check*, jamais un
  flux temps réel.
- Pas d'identifiant publicitaire.
- Pas de cookies de tracking.

## 3. Photos

- Toutes les photos prises pendant une chasse sont **stockées localement**
  par défaut (espace privé du téléphone, non sauvegardées dans la galerie
  publique).
- L'envoi au serveur est **opt-in explicite** par photo. Sans validation
  visuelle de l'adulte, rien ne quitte le téléphone.
- Si envoyée : photo stockée chiffrée au repos, accessible uniquement au
  créateur du hunt et à l'équipe support, supprimée 90 jours après upload.
- Recommandation forte affichée à l'adulte avant prise : "n'inclure aucun
  visage d'enfant identifiable". Validation manuelle obligatoire avant
  upload.

## 4. Bluetooth

- Le scan recherche **uniquement** les adresses MAC déclarées dans le hunt
  par le créateur (whitelist).
- Aucun appareil tiers n'est journalisé ni envoyé.
- Permissions `BLUETOOTH_SCAN` demandées juste avant un step de type
  `bluetooth`, jamais en arrière-plan.

## 5. Mode hors ligne

- Le téléchargement d'un hunt copie les données *publiques* de ce hunt
  uniquement. Pas de données utilisateur.
- Les soumissions en attente sont stockées dans `user://pending_submissions.json`
  (espace privé Godot, sandbox OS).
- Au flush, les soumissions sont envoyées telles quelles : la fenêtre
  temporelle d'origine (`ClientCreatedAtUtc`) est préservée pour le
  scoring, sans nouvelles données ajoutées.

## 6. Droits des personnes

Endpoints REST exposés (à implémenter en phase 2) :

| Droit | Endpoint | Auth | Effet |
|-------|----------|------|-------|
| Accès / portabilité | `GET /api/users/me/export` | Bearer | JSON complet des données du compte |
| Rectification | `PATCH /api/users/me` | Bearer | Modifier displayName, email |
| Effacement | `DELETE /api/users/me` | Bearer + confirmation | Purge compte + anonymisation des soumissions |
| Limitation | `POST /api/users/me/freeze` | Bearer | Désactive sans supprimer |

## 7. Sous-traitants prévus

| Sous-traitant | Donnée | Usage | Pays |
|---------------|--------|-------|------|
| (à définir) Hébergeur cloud | Toutes | Hébergement DB + API | UE |
| (à définir) Email transactionnel | Email | Confirmation, support | UE |

Aucun sous-traitant analytics ou publicitaire.

## 8. Conservation et purge

- Comptes inactifs 24 mois : email d'alerte → suppression à 26 mois.
- Soumissions : 13 mois puis anonymisation (FK `SubmittedById` → `null`,
  `Family` → bucket "anonyme").
- Logs applicatifs : 30 jours.
- Backups : 35 jours chiffrés.

## 9. Mineurs (art. 8 RGPD)

L'application étant pensée pour qu'**aucun mineur ne crée de compte**,
nous évitons l'écueil du consentement parental requis pour les < 15 ans.
Le contrôle est architectural :

- Pas d'écran d'inscription enfant.
- Le rôle `Player` ne nécessite pas de compte.
- Les données du joueur anonyme ne quittent jamais le téléphone (mode
  detached + cache local).

Si nous devions un jour proposer des comptes enfants (ex: classements
nominatifs école), un module de consentement parental vérifié (double
opt-in adulte) serait obligatoire et tracé.

## 10. Notifications de violation

En cas de fuite, notification CNIL sous 72h conformément à l'art. 33 RGPD,
et notification aux personnes concernées si risque élevé. Procédure
détaillée à formaliser avant la mise en production.

## 11. Changements de politique

Toute modification de cette politique fait l'objet :

1. D'une entrée dans `docs/CLAUDE-LOG.md` motivée.
2. D'une bannière in-app pour les utilisateurs existants.
3. De la mise à jour du présent document avec date et version.

**Version actuelle** : 0.1 — 2026-04-26
