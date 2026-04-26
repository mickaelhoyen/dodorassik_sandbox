# Sécurité — Dodorassik

> Document vivant. Toute modification du modèle de menaces ou des
> contre-mesures doit être consignée ici avant d'être implémentée.

## 1. Modèle de menaces (STRIDE)

| Menace | Acteur | Surface | Impact | Contre-mesure |
|--------|--------|---------|--------|---------------|
| **S**poofing — usurpation d'identité créateur | Externe | `POST /api/auth/login` | Publication de hunts malveillantes | JWT signé HS256, password PBKDF2, rate limit login |
| **T**ampering — altération de hunt en transit | MitM | API HTTPS | Indices truqués / classement faussé | TLS obligatoire en prod, JWT non rejouable (exp courte), idempotency key sur soumission |
| **R**epudiation — déni d'une soumission | Joueur compétitif | `POST /steps/{id}/submit` | Litige de classement | `ClientCreatedAtUtc` côté client + `ServerReceivedAtUtc` côté serveur, journal applicatif horodaté |
| **I**nformation Disclosure — fuite d'email/PII | Externe | DB, logs, exports | RGPD violation | Logs sans PII, pas d'email dans les URLs, `Vary: Authorization` sur cache |
| **D**enial of Service | Externe | Endpoints publics, upload | Indisponibilité | Rate limiting, taille max body, timeouts EF, image size cap |
| **E**levation of Privilege — joueur → créateur | Compte joueur | API mutating | Création de contenu non modéré | `[Authorize(Roles=...)]` sur tout endpoint mutating, claim `role` validé serveur |
| **Géo** — traque d'un enfant via la chasse | Adulte malveillant | Trace GPS | Sécurité physique | Pas de stockage de trace joueur, GPS lu uniquement à la demande |

## 2. Authentification & autorisation

- **Algorithme** : JWT HS256, secret ≥ 32 octets, rotation manuelle pour le moment.
- **Durée** : 7 jours (access). Refresh tokens à introduire en phase 3.
- **Claims** : `sub` (Guid), `email`, `display_name`, `role`. Pas de claim
  custom contenant des PII enfant.
- **Hashing mot de passe** : PBKDF2-HMAC-SHA256, 100 000 itérations, sel 16 octets,
  format versionné (`v1$iter$salt$hash`). Migration vers Argon2id prévue
  dès qu'on ajoute la dépendance native.
- **Vérification temps constant** via `CryptographicOperations.FixedTimeEquals`.
- **Validation token** : Issuer, Audience, Lifetime, Signing key, Clock skew
  ≤ 1 minute.

## 3. Transport

- **Production** : HTTPS only, HSTS, `RequireHttpsMetadata = true`,
  redirect 80 → 443 au reverse proxy.
- **Dev** : HTTP toléré sur localhost uniquement.
- **CORS** : whitelist explicite par environnement (`Cors:AllowedOrigins`
  dans la config). `AllowAnyOrigin` est interdit hors `Development`.

## 4. Limites de taux (rate limiting)

Implémentation .NET 8 `AddRateLimiter` :

| Endpoint | Limite |
|----------|--------|
| `POST /api/auth/login` | 5 / minute / IP, fenêtre fixe |
| `POST /api/auth/register` | 3 / heure / IP |
| `POST /api/hunts/*/submit` | 30 / minute / utilisateur |
| Catch-all | 60 / minute / IP |

Réponse `429 Too Many Requests` avec header `Retry-After`.

## 5. Validation d'entrée

Tous les DTO entrants valident :

- Longueur des chaînes (max 256 par défaut).
- Format email (`MailAddress`).
- Mot de passe ≥ 8 caractères, ≤ 128.
- Taille du JSON `Params` / `Payload` ≤ 8 KiB.
- Nombre de steps par hunt ≤ 100 (anti DoS).

## 6. Stockage

- **DB** : PostgreSQL avec utilisateur applicatif distinct du super-utilisateur,
  privilèges minimums (`CONNECT`, `USAGE`, `SELECT/INSERT/UPDATE/DELETE` sur
  les tables — pas de `CREATE` runtime).
- **Migrations** : appliquées via pipeline déploiement, pas via le service
  applicatif au runtime en prod.
- **Backups** : chiffrés au repos, rétention RGPD documentée dans `PRIVACY.md`.

## 7. Logs

- Format structuré JSON.
- Champs interdits : `password`, `passwordHash`, `email`, `token`, `payload`
  brut, coordonnées GPS individuelles.
- Champs autorisés : `userId` (Guid), `huntId`, `stepId`, `correlationId`,
  `httpStatus`, `latencyMs`, `clientIpHashed` (HMAC-SHA256 avec sel rotatif).

## 8. Dépendances

- Audit régulier : `dotnet list package --vulnerable --include-transitive`
  côté .NET, surveillance Godot via mainline officielle.
- Politique : pas de dépendance avec CVE non patchée > 30 jours en prod.

## 9. Divulgation responsable

Pour signaler une vulnérabilité : `security@dodorassik.example` (à mettre
en place lors du déploiement). Réponse sous 72h.

## 10. Changements à venir (suivi)

- [ ] Argon2id pour les nouveaux hashs (compatibilité descendante via prefix `v2$`).
- [ ] Refresh tokens + révocation.
- [ ] WebAuthn / passkeys pour l'interface créateur.
- [ ] Signature des paquets de hunt téléchargés en offline (anti-tamper local).
