# Revue ChatGPT — MISSION-1-STEP-4B — Conception SQLite

Date : 2026-11-23
Rapport examiné : `docs/ai-handoff/inbox/20261123_MISSION-1-STEP-4B_CODEX.txt`
Branche du rapport : `ai-handoff/report-MISSION-1-STEP-4B`

## 1. Décision

**VALIDÉ SOUS RÉSERVES — direction architecturale approuvée, implémentation partielle autorisable après cette revue.**

Le rapport est complet, distingue correctement faits et déductions, et respecte le périmètre read-only. Aucun test, build ou scénario runtime n'est présenté comme exécuté.

La centralisation via `SQLiteDatabaseCoordinator`, l'utilisation de `PRAGMA user_version`, les transactions synchrones pour les opérations SQLite multi-écritures et la séparation snapshot DB / packaging fichier constituent la bonne direction.

Cette validation ne vaut ni validation runtime, ni autorisation de modifier les règles métier ou les données utilisateur.

## 2. Arbitrages produit et techniques

### A. Templates

- Un template doit pouvoir être réutilisé par plusieurs enregistrements.
- La liste de sélection ne doit donc pas masquer un template simplement parce qu'il est déjà utilisé.
- La centralisation SQLite ne doit toutefois pas changer silencieusement le comportement actuel de `GetTemplatesAsync`.
- Toute correction de ce comportement doit être isolée, documentée et accompagnée de tests UI/consommateurs dans une mission distincte si elle dépasse le simple maintien du comportement.

### B. Changement de template d'un enregistrement

- Un changement de template ne doit jamais supprimer implicitement l'ancien record, ses valeurs ou ses instances.
- Le comportement par défaut doit être non destructif : si un record existe avec un template différent, refuser le remplacement implicite et conserver l'état précédent.
- Une opération explicite de migration/remplacement de template pourra être conçue plus tard, avec politique de mapping et confirmation dédiée.
- Valider le template cible et ses définitions avant toute mutation.

### C. Suppression de champs et de blocs

- Ne pas laisser des valeurs orphelines invisibles par suppression silencieuse.
- Pour la mission de centralisation, préserver les données existantes et ne pas ajouter de purge automatique.
- Une suppression de définition ne doit être autorisée que si son effet sur les valeurs/instances est explicite et transactionnel. À défaut de règle métier implémentée et testée, refuser l'opération plutôt que perdre les données.
- L'archivage/restauration de définitions et de valeurs pourra être étudié dans une mission ultérieure; ne pas l'inventer dans cette étape.

### D. Versionnement SQLite

- Utiliser `PRAGMA user_version` comme autorité unique de version du schéma. Ne pas ajouter une deuxième table de version sans nécessité démontrée.
- Les migrations doivent être centralisées et transactionnelles, avec vérification des préconditions et postconditions.
- Ne jamais avaler une exception SQLite générale.
- Pour une base version 0/non versionnée, inspecter le schéma réel. N'adopter automatiquement que les formes dont l'existence est prouvée. Schéma inconnu ou partiel : arrêt sans altération destructive.
- Ne pas dédupliquer automatiquement des données pour satisfaire une nouvelle contrainte d'unicité.
- Le backup pré-migration doit être vérifié et réouvrable; si cette garantie échoue, ne pas lancer la migration.

### E. Transactions et coordination

- Une seule connexion SQLite applicative, possédée par le coordinateur.
- Les services métier ne créent ni connexion, ni verrou, ni initialisation DDL dans leurs constructeurs.
- Une opération métier acquiert la coordination une seule fois; les helpers internes réutilisent la connexion déjà détenue.
- Les callbacks de transaction restent synchrones : aucun `await`, aucune réentrée dans le coordinateur et aucun appel à un service qui réacquiert le verrou.
- Chaque use case multi-écritures doit être entièrement validé avant mutation et atomique côté SQLite.

### F. Backups et restauration

- Le rapport a raison d'identifier le mélange entre données dynamiques liées à un Workspace et Clients globaux sans `WorkspaceId`.
- Ne pas implémenter un restore ZIP v1 ambigu.
- Ne jamais exécuter de DELETE global dans une restauration censée viser un Workspace.
- Pour la suite, distinguer explicitement deux contrats : sauvegarde/restauration d'un Workspace et sauvegarde/restauration complète de l'application/appareil.
- Le format d'archive devra porter un `Scope` obligatoire distinct de `FormatVersion` et de la version du schéma SQLite.
- Tant que la portée des Clients n'est pas définie, une restauration Workspace ne doit ni supprimer ni remplacer les Clients globaux.
- Toute archive doit être entièrement validée et staged avant mutation; l'ancienne sauvegarde et les anciens fichiers doivent rester récupérables en cas d'échec.
- SQLite et le système de fichiers ne sont pas atomiques ensemble : journal/reprise explicite, sans prétendre à un commit distribué.

## 3. Périmètre autorisé pour la prochaine implémentation

**Step 4C — centralisation et sûreté SQLite des services Workspace/DynamicForm uniquement.**

Autorisé :
1. Introduire le runner de migrations et brancher `PRAGMA user_version` dans le coordinateur.
2. Migrer `LocalWorkspaceContext` et `SqliteDynamicFormService` vers `ISqliteDatabase`.
3. Supprimer leurs connexions privées et initialisations asynchrones dans les constructeurs.
4. Rendre atomiques les opérations multi-écritures concernées.
5. Corriger l'ordre de `GetOrCreateRecordAsync` pour préserver l'ancien record en cas d'échec ou de template différent, selon les règles ci-dessus.
6. Ajouter les tests de migration, concurrence, rollback et conservation décrits dans le rapport.
7. Mettre à jour les documents d'architecture et le rapport de mission.

Non autorisé dans Step 4C :
- Modifier le schéma métier au-delà des migrations strictement nécessaires et prouvées.
- Modifier le modèle Workspace ou ajouter `WorkspaceId` aux Clients.
- Reconcevoir le moteur dynamique ou l'interface.
- Modifier le comportement des templates au-delà de la conservation du comportement existant.
- Implémenter ou modifier `CompleteBackupService`, ZIP v1/v2, import/export JSON ou la restauration des pièces jointes.
- Accéder à la base réelle de l'utilisateur.
- Fusionner ou pousser des changements applicatifs sur `develop` sans revue.

Le service `CompleteBackupService` reste un risque connu et non résolu. Sa connexion privée et son restore global dangereux ne doivent pas être présentés comme corrigés par Step 4C. Il fera l'objet de Step 4D, après décision et tests dédiés au contrat de sauvegarde/restauration.

## 4. Critères d'acceptation Step 4C

- Aucun `SQLiteAsyncConnection` privé ne subsiste dans `LocalWorkspaceContext` ou `SqliteDynamicFormService`.
- L'initialisation est centralisée, attendue explicitement et échoue de manière visible.
- Les migrations de base neuve, adoption d'une forme existante prouvée, schéma inconnu, échec et version future sont couvertes.
- Toute migration échouée laisse `user_version` et les données inchangées.
- Les opérations multi-écritures approuvées sont rollbackées intégralement en cas d'échec injecté.
- Un changement de template invalide ou différent ne détruit jamais l'ancien record et ses valeurs.
- Les données d'un autre Workspace restent inchangées.
- Les tests automatisés sont exécutés et leurs résultats documentés; aucun test non exécuté ne peut être déclaré PASS.
- Build Android Debug exécuté si l'environnement le permet; sinon résultat NOT TESTED avec raison précise.
- Le rapport complet figure dans la réponse finale de Codex et dans un fichier .txt unique sous `docs/ai-handoff/inbox/`, publié sur une branche dédiée ne contenant que le rapport.

## 5. Décision finale et prochaine action

Step 4B est clos avec validation sous réserves. La prochaine action est **`@execute` sur une mission Step 4C distincte**, limitée au périmètre autorisé ci-dessus. Après publication du rapport et de la branche, ChatGPT effectuera `@review`.

Aucune action n'est demandée sur la PR cloud #4; elle reste en pause.
