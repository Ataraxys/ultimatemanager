# MISSION — MAUI Developer — Stabilisation complète des données et fiches dynamiques

**Responsable unique : MAUI Developer.**  
**Type de mission : implémentation et livraison de code, pas un audit.**

## Décision de travail et autorisation

Le propriétaire du dépôt a explicitement confirmé le circuit suivant :

1. Tu travailles directement sur la branche existante `integration/reconcile-feature-branches`.
2. Tu commits tes modifications directement sur cette branche, en plusieurs commits logiques si nécessaire.
3. Une fois les corrections terminées et les validations réussies, tu intègres cette branche dans `develop` en mettant à jour la PR #5 et en la fusionnant avec un merge commit normal.
4. Tu vérifies le résultat réel sur GitHub après la fusion.

**Aucune branche d'implémentation supplémentaire ne doit être créée.** Ne crée pas `feature/pr5-data-stabilization`. Ne demande pas à un autre agent de reprendre la mission.

La base connue au lancement est le HEAD de la PR #5 :
`b59b6352bb2c473a1da84a1e62d7ca564a2f4e18`.

Actualise les références et vérifie le HEAD réel avant toute modification. Si la branche a avancé, travaille depuis son nouvel HEAD et consigne le SHA exact. Si son état ou la PR a changé de manière incompatible, arrête-toi et explique le blocage.

Le propriétaire a autorisé l'intégration finale vers `develop`, mais cette autorisation est conditionnée à une livraison réellement vérifiée : **ne fusionne pas si un problème P1 reste ouvert, si les tests essentiels échouent ou si les migrations risquent les données existantes.** Dans ce cas, laisse la PR ouverte et explique exactement ce qui bloque. Aucun push direct sur `develop` : utilise la PR #5 et un merge commit normal lorsque les critères sont satisfaits.

## 1. Documents obligatoires

Lis avant de coder :

- `README.md`
- `docs/ai-handoff/AGENT_PROTOCOL.md`
- `docs/ai-handoff/SESSION_STATE.md`
- `docs/ai-handoff/outbox/20261124_MISSION-PR5-REVUE-COMPLETE-82-FICHIERS.md`
- `docs/ai-handoff/outbox/20261124_MISSION-COPILOT-QA-REVIEW-PR5.md`
- `docs/ai-handoff/inbox/20261125-COPILOT-QA-PR5-FULL-REVIEW.txt`
- `docs/01-architecture/DECISIONS.md`, si présent.

Le rapport Copilot est un point de départ. Il n'est pas une demande de nouvel audit. Vérifie chaque constat dans le code au moment de le corriger ; si un problème est déjà corrigé, ne le réintroduis pas.

## 2. Périmètre : corriger et livrer

Tu es responsable de l'ensemble des corrections ci-dessous. Ne t'arrête pas après un diagnostic ou une liste de recommandations. Travaille par étapes, mais poursuis jusqu'à une livraison cohérente.

### P1 — Isolation Workspace des clients

Le rapport indique que `Client` n'a pas de `WorkspaceId`, que les opérations de `SqliteClientService` ne sont pas toutes filtrées par Workspace et que les sauvegardes peuvent exporter des clients hors du Workspace actif.

- Examine le modèle Workspace et `LocalWorkspaceContext`.
- Introduis une isolation cohérente des clients par Workspace.
- Filtre lecture, GetById, recherche, modification et suppression.
- Empêche l'accès ou la mutation d'un client d'un autre Workspace.
- Aligne les exports de sauvegarde sur le Workspace actif.
- Ajoute une migration idempotente et non destructive pour les clients historiques sans WorkspaceId, en les rattachant au Workspace approprié selon le modèle réel de l'application.
- Ne supprime aucune donnée existante.

### P1 — Suppression complète d'un client

Le rapport indique que la suppression ne retire que la ligne Client et peut laisser des données dynamiques orphelines.

Cartographie les relations réelles et supprime atomiquement les données propres au client : records, valeurs, instances de blocs et clones/templates privés au client, ainsi que toute autre dépendance effectivement liée.

Ne supprime jamais un template source partagé ni les données d'un autre client. Ajoute un test où deux clients utilisent le même template source et où la suppression de l'un ne touche pas l'autre.

### P1 — Connexions SQLite centralisées

Le rapport indique que `SqliteDynamicFormService`, `LocalWorkspaceContext` et `CompleteBackupService` créent des connexions indépendantes alors que `SqliteClientService` utilise le coordinateur `ISqliteDatabase`.

- Cartographie tous les accès SQLite.
- Fais passer les accès par le mécanisme de coordination existant.
- Évite les connexions indépendantes concurrentes au même fichier.
- Préserve les transactions, la cohérence des snapshots et l'injection de dépendances.
- N'introduis pas un second coordinateur parallèle.

### P1 — Changement de template pendant la création

Corrige le scénario : création d'un client → sélection du template A → aperçu/saisie → sélection du template B. Le chemin actuel passant par `SelectTemplateAsync`, `PrepareWorkingTemplateAsync`, `DynamicRecordViewModel.LoadAsync` et `GetOrCreateRecordAsync` peut rencontrer un conflit de template pour un même client.

L'utilisateur doit pouvoir changer de template avant l'enregistrement définitif, sans accumuler des clones persistants ni provoquer d'exception. Définis le devenir des valeurs saisies lorsque les champs diffèrent. Ajoute un test de régression A → B.

### P1 — Annulation de création sans orphelins

La sélection du template semble persister des clones/enregistrements/instances avant la validation finale.

Distingue l'état temporaire de l'état persistant. Après annulation, aucune donnée persistante créée uniquement par la tentative abandonnée ne doit rester. L'enregistrement final doit persister les éléments nécessaires de façon cohérente et atomique. Teste : nouveau client → template A → template B → saisie → annulation.

### P2 — Atomicité des opérations dynamiques

Examine notamment :

- `DeleteTemplateAsync`
- `DeleteBlockAsync`
- `DeleteFieldAsync`
- `MoveBlockAsync`
- `MoveFieldAsync`
- `CloneTemplateAsync`
- `EnsureClientTemplateAsync`

Rends atomiques les opérations multi-écritures lorsque nécessaire. Définis les règles de cascade/preservation et évite les `FieldValue` et `BlockInstance` orphelins. Ne masque pas les erreurs avec des catch génériques.

### P2 — Migrations

Examine les migrations et les `catch (SQLiteException)` qui pourraient masquer des erreurs autres que « colonne déjà présente ».

Rends les migrations idempotentes, distingue les duplications attendues des erreurs réelles, préserve les données et teste les migrations sur des bases temporaires représentatives. N'introduis pas un versionnement incompatible avec l'existant.

### P2 — Sauvegardes

Examine la régénération de sauvegarde complète à chaque modification de structure.

Évite les reconstructions inutiles à chaque petite modification, tout en préservant la cohérence des archives. Évite les sauvegardes concurrentes, nettoie les fichiers temporaires après échec et traite explicitement les documents référencés mais absents.

Ne réactive pas une restauration destructive et ne développe pas un nouveau système de restauration/chiffrement sans nécessité validée.

## 3. Règles produit non négociables

Préserve impérativement :

- « Modifier » modifie le contenu uniquement.
- « ✎ » permet de modifier toute la structure, les blocs, les champs et le template.
- La consultation reste en lecture seule.
- Les templates restent réutilisables même s'ils sont utilisés.
- Les templates sont optionnels et sélectionnés explicitement.
- Le nom du template n'est pas affiché par défaut sur la fiche client.
- Les adresses, numéros de téléphone, coordonnées GPS et commentaires multiples restent compatibles avec le modèle dynamique.
- En consultation, le bouton de carte ouvre Google Maps.
- En modification de structure, le bouton permet de choisir des coordonnées sur la carte.

N'altère pas ces règles pour simplifier la persistance.

## 4. Tests et validations obligatoires

Utilise exclusivement des bases SQLite temporaires. **Ne teste jamais sur la vraie base de l'utilisateur.**

Ajoute/exécute des tests couvrant au minimum :

1. Migration d'une ancienne base avec clients existants.
2. Affectation des clients historiques au Workspace approprié.
3. Isolation des clients entre deux Workspaces.
4. GetById, recherche, modification et suppression sans fuite entre Workspaces.
5. Suppression d'un client et de ses données dépendantes.
6. Préservation du template source et des données d'un second client.
7. Changement A → B pendant la création.
8. Annulation après sélection de template et saisie.
9. Absence d'orphelins après suppression/annulation.
10. Atomicité des suppressions de blocs/champs.
11. Cohérence du backup par Workspace.
12. Accès concurrents via le coordinateur SQLite.
13. Réutilisation et visibilité des templates existants.

Lance les tests disponibles, puis :

- Build Android Debug.
- Build Android Release si possible.
- `git diff --check`.
- Tests UI manuels si un émulateur/appareil est disponible.

Pour chaque vérification, indique PASS / FAIL / BLOCKED / NOT RUN, avec la commande et le résultat réel. N'invente aucun test UI.

## 5. Discipline de réalisation

- Reste sur `integration/reconcile-feature-branches`.
- Fais des commits logiques et fréquents directement sur cette branche.
- N'ouvre pas une branche d'implémentation parallèle.
- N'effectue aucun merge/rebase/cherry-pick/reset/force-push.
- Ne touche pas au worktree local protégé `feature/block-instances-fix` ni à son diff préexistant dans `UltimateManager.csproj`.
- Pas de refonte générale, de mise à jour de dépendances ou de nettoyage sans rapport avec les constats.
- Si une décision métier indispensable manque, documente précisément le point et arrête uniquement la partie concernée ; poursuis les autres corrections indépendantes.

## 6. Livraison et intégration vers develop

Quand les corrections sont terminées :

1. Vérifie que tous les commits de code sont bien présents sur GitHub dans `integration/reconcile-feature-branches`.
2. Mets à jour le corps de la PR #5 avec un résumé des corrections et les validations réelles.
3. Vérifie le diff complet de la PR #5 par rapport à `develop`.
4. Si tous les P1 sont corrigés, les tests essentiels passent et aucune migration ne menace les données, fusionne la PR #5 vers `develop` avec un **merge commit normal**. C'est l'intégration autorisée par le propriétaire.
5. Ne fais jamais de push direct sur `develop`.
6. Après fusion, vérifie le SHA de merge, le HEAD de `develop` et l'état merged de la PR.
7. Si les critères ne sont pas remplis, ne fusionne pas : laisse la PR ouverte et explique les blocages précis.

Conformément à `AGENT_PROTOCOL.md`, publie aussi un rapport final unique `.txt` dans `docs/ai-handoff/inbox/` sur une branche dédiée `ai-handoff/report-data-stabilization` basée sur le `develop` actualisé. La branche de rapport doit contenir uniquement les fichiers de handoff. Le rapport doit préciser les SHAs de départ, commits de code, SHA de merge éventuel, commandes et résultats de tests, migrations, fichiers modifiés et risques restants. Mets à jour `SESSION_STATE.md` sur la branche d'intégration avant fusion pour documenter la mission et son état, puis assure-toi que le statut final est cohérent après intégration.

**Ne prétends jamais qu'un code, un rapport ou une fusion est publié sans avoir vérifié le fichier, le commit et le SHA réels sur GitHub.**

## 7. Critère de réussite

La mission n'est pas terminée avec un audit, un plan ou un rapport.

Elle est terminée lorsque les corrections P1/P2 applicables sont implémentées, testées sur bases temporaires, compilées autant que l'environnement le permet, publiées directement sur la branche d'intégration existante et, si les critères de sécurité/validation sont satisfaits, intégrées dans `develop` via la PR #5.

**Tu es le MAUI Developer responsable de coder et de livrer. Commence par actualiser la branche, puis implémente.**
