# Roadmap

Liste vivante. À découper en issues GitHub au fil de l'eau.

## Phase 1 — Fondations (✅ ce commit)

- [x] Squelette Godot 4 + 5 autoloads
- [x] Trois interfaces (joueur, créateur, super-admin)
- [x] Solution .NET 8 (Api / Core / Infrastructure) avec EF Core PostgreSQL
- [x] Modèle domaine de base (User, Family, Hunt, Step, Clue, Submission, Score)
- [x] Auth JWT + hashing PBKDF2
- [x] File de soumissions hors ligne

## Phase 2 — Boucle de jeu jouable

- [ ] Premières migrations EF Core générées et committées
- [ ] Tests d'intégration `WebApplicationFactory` sur `AuthController` et `HuntsController`
- [ ] Création réelle d'une chasse depuis l'éditeur Godot (steps + clues)
- [ ] Plugin Android pour `DeviceServices` : GPS + caméra
- [ ] Build Android signé (debug) documenté
- [ ] Sélection de famille à la connexion (multi-adultes, multi-enfants)

## Phase 3 — Multi-joueur & compétition

- [ ] WebSocket / SignalR pour la liveness des chasses compétitives
- [ ] Leaderboard temps réel
- [ ] Plusieurs équipes par famille (ex: garçons vs filles)
- [ ] Anti-triche basique : cohérence vitesse de déplacement, ordre des étapes

## Phase 4 — Création avancée

- [ ] Éditeur de carte intégré (placer des points, dessiner un parcours)
- [ ] Bibliothèque d'assets/énigmes partagée
- [ ] Validation par le super-admin avant publication publique
- [ ] Marketplace de parcours (gratuit / payant)
- [ ] Internationalisation (FR/EN au minimum)

## Phase 5 — Plateforme

- [ ] Console super-admin web (séparée du jeu) — peut être un projet Blazor
- [ ] Statistiques d'usage (familles actives, parcours populaires)
- [ ] CI/CD GitHub Actions (build serveur + export Godot Android)
- [ ] Déploiement Docker / Fly.io / Azure App Service
