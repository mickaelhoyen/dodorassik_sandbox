# CLAUDE.md — Règles de travail pour Claude sur Dodorassik

Ce fichier est lu **avant chaque action** par Claude. Il pose les invariants
non négociables du projet. Toute modification de code doit pouvoir être
justifiée au regard de ces règles.

## 1. Public et contexte

Dodorassik est une application **familiale jouée par des enfants** assistés
d'adultes. Cela impose une responsabilité particulière :

- Les enfants ne créent **jamais** de compte. Ils ne sont pas tracés. Aucune
  donnée personnelle d'enfant n'entre dans la base.
- L'adulte connecté reste l'unique sujet de données (RGPD : seul "responsable").
- Le mode joueur fonctionne sans authentification : un téléphone partagé
  par toute la famille reste anonyme côté serveur.

## 2. Security by Design — invariants

À respecter pour **tout** ajout ou modification de code :

1. **Pas de secret en clair dans le repo.** Les `appsettings.json` ne contiennent
   que des valeurs de dev clairement marquées (`REPLACE_ME_...`). Tout
   secret réel passe par variables d'environnement / user-secrets / coffre.
2. **Defense in depth.** Une vérification côté client n'est jamais une
   garantie côté serveur. Toute mutation passe par un check d'autorisation
   serveur, même si l'UI le cache déjà.
3. **Authent centralisée.** Aucun controller ne se réinvente l'auth :
   `[Authorize]` + `[Authorize(Roles=...)]` ou rien. Pas de bypass
   conditionnel "si DEBUG".
4. **Hashing fort.** Les mots de passe utilisent PBKDF2-SHA256 ≥ 100 000
   itérations OU Argon2id (à privilégier dès qu'on ajoute la dépendance).
   Jamais de SHA1/MD5.
5. **JWT court par défaut.** 7 jours max. Si on doit prolonger, utiliser
   refresh tokens, pas allonger l'access token.
6. **CORS strict en prod.** `AllowAnyOrigin` est interdit en `Production`.
   Whitelist explicite des origines.
7. **Rate limiting** sur les endpoints d'authentification et toute écriture
   coûteuse (création de hunt, soumission).
8. **Validation stricte des entrées.** Tout DTO entrant valide ses champs
   (longueur, format, plages). Refus en 400 si invalide.
9. **Pas d'injection.** Toutes les requêtes DB passent par EF Core (pas de
   `FromSqlRaw` avec interpolation).
10. **Logs sans PII.** Email, token, payload utilisateur ne doivent jamais
    apparaître dans les logs en clair. Structurer `{ userId }` et c'est tout.
11. **HTTPS obligatoire en prod.** `RequireHttpsMetadata = true` hors dev.
12. **Dépendances à jour.** Auditer (`dotnet list package --vulnerable`)
    avant chaque release.

## 3. Privacy by Design — invariants

1. **Data minimization.** Stocker le minimum strict :
   - User : id, email (login), displayName (affichage), passwordHash, role,
     createdAt, lastLoginAt, familyId.
   - Family : id, name (libre, peut être un pseudo), createdAt.
   - Hunt/Step : pas de PII.
   - Submission : payload utile au scoring uniquement, pas la photo brute
     en base par défaut (on stocke un hash + un blob URL si upload validé).
2. **Pas de traçage GPS continu.** Les coordonnées sont lues à la demande
   pour valider une étape, jamais en arrière-plan.
3. **Photos par défaut locales.** Une photo prise pendant une chasse reste
   dans le stockage privé du téléphone. L'envoi au serveur n'a lieu que sur
   action explicite de l'utilisateur (et n'est pas requis pour scorer).
4. **Pas de tiers analytics.** Pas de Firebase, GA, Sentry SaaS sans
   anonymisation et opt-in clair.
5. **Droits RGPD effectifs.**
   - `GET /api/users/me/export` retourne toutes les données concernant le
     compte (portabilité).
   - `DELETE /api/users/me` purge le compte et anonymise les soumissions
     associées (effacement).
   - Email de confirmation avant la purge.
6. **Géo anonyme côté hunt.** Les hunts publiées peuvent contenir des
   coordonnées GPS *des étapes* (lieux), mais jamais une trace de joueur.
7. **Cookies / stockage navigateur** : aucun pour l'API (stateless JWT).
8. **Hébergement UE** par défaut quand on déploiera (mention RGPD).

## 4. Process pour Claude — checklist avant chaque commit

Avant d'écrire un commit, Claude doit cocher mentalement :

- [ ] Les invariants Security ci-dessus restent vrais.
- [ ] Les invariants Privacy ci-dessus restent vrais.
- [ ] Aucun secret n'est commité (grep `password|secret|api_key|token`).
- [ ] Tout nouvel endpoint a un check `[Authorize]` ou est explicitement
      documenté comme public.
- [ ] Tout nouveau champ stocké est nécessaire ; sinon, ne pas l'ajouter.
- [ ] Les logs ajoutés ne contiennent pas de PII.
- [ ] La requête utilisateur originale est consignée dans
      `docs/CLAUDE-LOG.md` avec analyse.
- [ ] Le `ROADMAP.md` est mis à jour si une phase change d'état.

## 5. Process pour Claude — journalisation

Pour chaque nouvelle requête utilisateur substantielle :

1. Ajouter une entrée dans `docs/CLAUDE-LOG.md` au format défini en haut
   de ce fichier.
2. Citer la requête utilisateur (verbatim ou résumé fidèle).
3. Décrire l'analyse, les choix, les alternatives rejetées.
4. Lister les fichiers modifiés (chemin + nature du changement).
5. Documenter explicitement la section **Security/Privacy review** :
   "ce changement n'introduit pas de nouvelle PII / élargit le scope X /
   etc."

## 6. Process pour Claude — refus

Claude refuse, sans demander, de :

- Stocker des données identifiantes d'enfant (nom, photo de visage, date
  de naissance, école…).
- Désactiver l'authentification "temporairement pour debug".
- Logger un mot de passe ou un token complet.
- Élargir CORS à `*` en prod.
- Introduire un tiers analytics sans demande explicite ET opt-in.
- Forcer `git push` ou réécrire l'historique sans demande explicite.

## 7. Conventions de code

- **C#** : .NET 8, `Nullable enable`, `ImplicitUsings enable`, `record` pour
  les DTO, `async`/`await` jusqu'au bout, pas de `.Result`.
- **GDScript** : Godot 4.6, typage statique partout (`var x: int`), pas de
  `Variant` quand un type est connu, `class_name` réservé aux ressources
  partagées.
- **Commits** : impératif présent ("Add", "Fix"), corps qui explique le
  *pourquoi*. Footer `Refs:` si lié à une issue.
- **Branches** : `feat/...`, `fix/...`, `docs/...`, ou la branche Claude
  par défaut.
