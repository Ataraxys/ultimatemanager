# MISSION-1-INTEGRATION-AUDIT-PR3 — Audit d'intégration read-only

## 1. Objectif

Auditer intégralement la PR #3 « Feature/add personalisation » et sa branche `feature/add-personalisation` par rapport à `develop`, afin de déterminer comment intégrer proprement le travail existant sans perdre les évolutions de `develop`, introduire de régression ou contourner les problèmes déjà identifiés dans SQLite.

Cette mission remplace temporairement Step 4C. Elle est strictement read-only.

## 2. Contexte et documents obligatoires

Lire :
- `docs/ai-handoff/SESSION_STATE.md`;
- `docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4B-CHATGPT-REVIEW.md`;
- `docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4C-CHATGPT-REVIEW.md`;
- `docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4C-IMPLEMENTATION.md`;
- `docs/ai-handoff/inbox/20261123_MISSION-1-STEP-4C_CODEX.txt`;
- les instructions générales du dépôt et le template de rapport.

Références :
- dépôt : `Ataraxys/ultimatemanager`;
- base cible actuelle : `develop`;
- branche auditée : `feature/add-personalisation`;
- PR : #3.

Rafraîchir les refs distantes et enregistrer les SHA exacts au début et à la fin de l'audit. Ne pas supposer que les SHA du brief sont toujours les plus récents.

## 3. Interdictions strictes

- Aucune modification de code, documentation, workflow ou configuration.
- Aucun merge, rebase, cherry-pick, reset, force-push ou changement de branche dans le checkout utilisateur.
- Ne pas mettre à jour la branche PR #3.
- Ne pas créer de PR et ne pas fusionner.
- Ne pas accéder à la base SQLite réelle de l'utilisateur.
- Ne pas présenter des analyses statiques comme des tests runtime.
- Ne pas importer ou copier des fichiers depuis une branche vers une autre.
- Ne pas considérer que le simple fait qu'une fonctionnalité soit présente signifie qu'elle est validée.

Créer uniquement un rapport dans un worktree isolé basé sur `develop`, sur une branche rapport dédiée.

## 4. Travaux d'audit obligatoires

### A. État Git et divergence

- Établir les SHA de `develop`, `feature/add-personalisation`, merge-base et PR #3.
- Établir commits ahead/behind, commits propres à chaque branche et chronologie.
- Lister les fichiers changés, ajoutés, supprimés et renommés, regroupés par domaines.
- Examiner les changements de `develop` absents de la branche feature.
- Identifier conflits probables, dépendances et ordre d'intégration.
- Vérifier les fichiers de gouvernance (.github, ai-handoff) et les risques de régression des instructions existantes.

### B. Fonctionnalités et architecture

Cartographier les fonctionnalités ajoutées ou modifiées :
- formulaires dynamiques et templates;
- modèle de données et objets métier;
- navigation et pages clients;
- Workspace;
- sauvegardes/restaurations;
- services SQLite et DI;
- tests/harness;
- documentation et décisions d'architecture.

Pour chaque domaine, préciser :
- fichiers concernés;
- dépendances internes;
- état : cohérent / à corriger / non vérifié;
- preuves statiques et limites.

### C. Audit SQLite prioritaire

Réexaminer dans le code de la branche feature :
- `SQLiteDatabaseCoordinator` / `ISqliteDatabase`;
- `SqliteClientService`;
- `LocalWorkspaceContext`;
- `SqliteDynamicFormService`;
- `CompleteBackupService`;
- initialisation, migrations et DI;
- transactions multi-écritures;
- concurrence et connexions privées;
- suppression de définitions et conservation des valeurs;
- `GetOrCreateRecordAsync`;
- isolation Workspace;
- formats et flux backup/restore.

Vérifier si les constats Step 4A/4B sont toujours exacts sur le HEAD audité. Identifier les corrections éventuellement déjà présentes, sans présumer de leur validité runtime.

### D. Audit fonctionnel UI/UX

Vérifier statiquement, par lecture des ViewModels, vues et navigation, les exigences produit déjà actées :
- « Modifier » ne modifie que le contenu;
- « ✎ » permet de modifier la structure/template comme lors de la création;
- templates réutilisables et sélection explicite;
- champs dynamiques et blocs;
- plusieurs adresses, numéros de téléphone, coordonnées GPS et commentaires;
- cohérence affichage/édition;
- import de contacts et avertissements connus.

Distinguer clairement ce qui est démontré par le code, ce qui est partiel et ce qui nécessite une validation manuelle sur appareil.

### E. Tests et qualité

- Inventorier les tests/harness et les scénarios réellement couverts.
- Vérifier les commandes et scripts documentés, sans utiliser de données utilisateur.
- Si des tests automatisés peuvent être lancés sans modification et dans un environnement isolé, les exécuter uniquement si cela ne change pas le checkout utilisateur; sinon NOT TESTED avec raison.
- Ne jamais affirmer qu'un build ou test est passé sur la seule base d'un rapport antérieur.
- Relever les dépendances de build, warnings et plateformes non vérifiées.

### F. Plan d'intégration proposé

Proposer un plan précis et prudent parmi :
- intégrer PR #3 telle quelle après corrections;
- découper PR #3 en plusieurs PR cohérentes;
- créer une branche d'intégration depuis develop et appliquer des lots contrôlés;
- abandonner/réécrire certaines parties si leur risque est excessif.

Pour chaque lot :
- périmètre fichiers/fonctionnalités;
- dépendances;
- risques;
- tests et preuves requis;
- ordre recommandé;
- critères de sortie.

Ne pas choisir ou exécuter l'intégration. C'est une décision ChatGPT après lecture du rapport.

## 5. Livrables

Un rapport structuré et factuel contenant :
1. Résumé exécutif et décision recommandée.
2. SHA, merge-base, divergence et état Git.
3. Tableau des commits propres à chaque branche.
4. Inventaire du diff par domaine.
5. Audit SQLite détaillé et état des constats antérieurs.
6. Audit des exigences UI/UX.
7. Audit des tests et limites.
8. Conflits et risques d'intégration.
9. Plan d'intégration par lots et critères de sortie.
10. Points nécessitant un arbitrage ChatGPT.
11. Liste des fichiers réellement modifiés par PR #3 (pas par cette mission).
12. Confirmation explicite qu'aucun fichier applicatif n'a été modifié pendant l'audit.

## 6. Rapport et publication

- Créer une branche dédiée depuis le SHA actuel de `develop` dans un worktree isolé.
- Nom : `ai-handoff/report-MISSION-1-INTEGRATION-AUDIT-PR3`.
- Fichier : `docs/ai-handoff/inbox/20261123_MISSION-1-INTEGRATION-AUDIT-PR3_CODEX.txt`.
- Cette branche doit contenir uniquement ce rapport.
- Ne pas pousser de code applicatif.
- Ne pas ouvrir de PR.
- Ne pas merger.
- Si publication impossible, conserver le rapport complet dans la réponse finale et expliquer le blocage.

## 7. Réponse finale obligatoire

La réponse finale Codex doit inclure le rapport complet en Markdown, directement copiable-collable dans la conversation. Le même rapport doit être sauvegardé dans le fichier .txt et publié sur la branche dédiée.

Sections obligatoires : statut, résumé, SHA/état Git, commits, périmètre, faits et preuves, fichiers PR3 par domaine, audit SQLite, audit UI/UX, tests PASS/FAIL/NOT TESTED, risques, conflits, plan d'intégration, arbitrages requis, prochaines étapes.

Aucun résultat ne doit être inventé. Séparer faits observés, déductions, recommandations et points non vérifiés.
