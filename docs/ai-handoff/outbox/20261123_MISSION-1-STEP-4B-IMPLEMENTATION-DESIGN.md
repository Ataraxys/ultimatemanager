# Mission 1 — Step 4B : conception détaillée de la migration SQLite

**Identifiant :** MISSION-1-STEP-4B  
**Priorité :** Haute  
**Statut :** READY  
**Type :** Analyse et conception strictement en lecture seule  
**Branche de référence :** `feature/add-personalisation`  
**Dépendance :** MISSION-1-STEP-4A — audit architectural reçu et revu par ChatGPT

## 1. Objectif

Transformer les constats de Step 4A en un plan d'implémentation précis, vérifiable et sûr pour centraliser les accès SQLite et sécuriser les opérations multi-tables.

Cette mission ne donne aucune autorisation de modifier le code. Elle doit produire une proposition suffisamment détaillée pour permettre à ChatGPT de décider ensuite si l'implémentation peut être autorisée.

## 2. Contexte et constats déjà vérifiés

L'audit Step 4A et la revue indépendante ont confirmé :

- `SqliteDynamicFormService`, `LocalWorkspaceContext` et `CompleteBackupService` construisent chacun une `SQLiteAsyncConnection` vers le même fichier SQLite.
- Les initialisations sont démarrées depuis les constructeurs, ce qui peut provoquer des opérations DDL concurrentes sur des connexions distinctes.
- `SqliteDynamicFormService.GetOrCreateRecordAsync` supprime le record précédent avant d'avoir validé et chargé le template demandé.
- Plusieurs opérations multi-écritures ne sont pas transactionnelles.
- `DeleteFieldAsync` et `DeleteBlockAsync` suppriment les définitions sans supprimer explicitement les valeurs associées.
- Les migrations utilisent des `ALTER TABLE` entourés de `catch (SQLiteException)` trop larges.
- `CompleteBackupService.PickAndRestoreAsync` exécute des suppressions globales sur plusieurs tables avant réinsertion, sans transaction globale visible.
- `CompleteBackupService.CreateAsync` lit des données clients globalement et filtre certaines autres données en mémoire.
- `GetTemplatesAsync` exclut les templates déjà utilisés par des records. Il faut vérifier le comportement fonctionnel attendu et ses consommateurs avant de proposer un changement.

Ces éléments sont des points de départ à revalider dans le code actuel, pas des instructions de reproduire aveuglément une solution.

## 3. Périmètre de l'analyse

Examiner conjointement :

1. `ISqliteDatabase` et `SQLiteDatabaseCoordinator`.
2. `LocalWorkspaceContext` et `IWorkspaceContext`.
3. `SqliteDynamicFormService` et `IDynamicFormService`.
4. `CompleteBackupService` et `ICompleteBackupService`.
5. `SqliteClientService`, `ClientBackupService`, `AutomaticBackupService`.
6. `MauiProgram.cs` et tous les chemins d'injection / initialisation concernés.
7. Modèles SQLite, relations logiques, index et contraintes.
8. Tests et harness existants.

Ne pas étendre la mission à une refonte du moteur dynamique, à une nouvelle isolation Workspace ou à une modification UI.

## 4. Livrables attendus

### A. Architecture cible

Décrire précisément :

- Quel composant possède la connexion SQLite.
- Comment chaque service reçoit et utilise `ISqliteDatabase`.
- Comment l'initialisation de la base et des tables est orchestrée.
- Comment empêcher les accès SQLite concurrents hors coordinateur.
- Comment éviter les dépendances circulaires et les appels imbriqués au coordinateur.
- Comment gérer le cycle de vie et les erreurs d'initialisation.

Fournir un diagramme de dépendances avant/après en Mermaid ou ASCII.

### B. Plan de migration

Proposer des étapes atomiques et ordonnées pour migrer :

- `LocalWorkspaceContext`.
- `SqliteDynamicFormService`.
- `CompleteBackupService`.

Préciser les fichiers concernés, les interfaces à adapter, l'ordre des changements et les critères d'acceptation de chaque étape.

Évaluer si ces trois services doivent être migrés dans une même livraison ou en plusieurs étapes compatibles. Justifier la décision.

### C. Migrations de schéma

Proposer un mécanisme versionné et centralisé :

- Utilisation de `PRAGMA user_version` ou mécanisme équivalent.
- Version initiale et transitions depuis les schémas existants.
- Idempotence.
- Transactions de migration.
- Gestion explicite des erreurs.
- Compatibilité avec les bases déjà installées.
- Stratégie de sauvegarde avant migration si pertinente.

Ne pas inventer l'historique des schémas : identifier les versions réellement documentées et les lacunes de preuve.

### D. Atomicité et intégrité des données

Pour chaque opération multi-écritures identifiée, établir un tableau :

| Opération | Tables touchées | Risque actuel | Atomicité requise | Solution proposée | Test de défaillance |
|---|---|---|---|---|---|

Inclure au minimum :

- Création et clonage de templates.
- Création et suppression de blocs/champs.
- Réordonnancement.
- Création de records et instances de blocs.
- `GetOrCreateRecordAsync`.
- `SaveFieldValueAsync`.
- Suppression des définitions et traitement des valeurs orphelines.
- Sauvegarde complète.
- Restauration complète.

Pour `GetOrCreateRecordAsync`, proposer une stratégie qui valide d'abord le template et ne détruit jamais les données existantes si la création du nouveau record échoue.

Pour les suppressions de champs/blocs, définir explicitement la politique métier : suppression des valeurs, conservation historique ou autre. Ne pas choisir arbitrairement.

### E. Sauvegarde et restauration

Traiter ce sujet comme une zone critique.

- Définir le périmètre exact d'une sauvegarde : Workspace courant ou sauvegarde globale.
- Définir le périmètre exact d'une restauration.
- Empêcher qu'une restauration d'un Workspace supprime les données des autres Workspaces.
- Définir la validation complète de l'archive avant toute écriture.
- Proposer une restauration transactionnelle ou une stratégie de remplacement atomique.
- Prévoir rollback en cas d'échec au milieu de la restauration.
- Prévoir le traitement des pièces jointes et le nettoyage des fichiers copiés si la restauration échoue.
- Définir le comportement de la sauvegarde legacy JSON.

Ne pas modifier le code ni les sauvegardes réelles.

### F. Stratégie de tests

Définir les tests automatisés à ajouter, sans les implémenter :

- Base temporaire neuve.
- Migration depuis chaque schéma historique réellement connu.
- Initialisation concurrente de plusieurs services.
- Accès concurrent lecture/écriture.
- Échec injecté à chaque étape d'une opération transactionnelle.
- Vérification qu'un ancien record reste intact si le nouveau template est invalide ou si une insertion échoue.
- Vérification des valeurs lors de suppression de champs/blocs.
- Restauration d'un Workspace en présence d'autres Workspaces.
- Archive corrompue ou pièce jointe absente.
- Échec de restauration et vérification du rollback.
- Vérification que la sauvegarde ne mélange pas les données entre Workspaces.

Pour chaque test, préciser le scénario, les données initiales, l'action, le résultat attendu et la preuve de réussite.

## 5. Contraintes impératives

- Lecture seule : aucune modification de code, de tests ou de documentation applicative.
- Ne pas exécuter de migration sur une base réelle.
- Ne jamais utiliser la base SQLite personnelle de l'utilisateur.
- Ne pas modifier les données utilisateur.
- Ne pas lancer de build ou de tests nécessitant une écriture dans l'environnement applicatif sans nécessité ; signaler simplement les validations non exécutées.
- Ne pas fusionner, pousser ou modifier les branches applicatives.
- Ne pas transformer cette mission en implémentation.
- Ne pas modifier le comportement Workspace ni les écrans UI.

## 6. Agents

Mobiliser si disponibles :

- Project Architect : architecture cible, DI, migration.
- Backend Specialist : transactions, SQLite, backups/restores.
- QA Reviewer : scénarios de tests et failure injection.

Codex principal consolide et vérifie les conclusions.

## 7. Rapport obligatoire

Produire un rapport complet directement dans la réponse finale ET dans un fichier unique sous `docs/ai-handoff/inbox/`.

Le rapport doit inclure :

1. Statut : COMPLETED / PARTIALLY COMPLETED / BLOCKED.
2. Identifiant et périmètre.
3. État Git initial et final.
4. Agents mobilisés.
5. Faits observés, hypothèses et points non vérifiés.
6. Architecture cible proposée.
7. Plan de migration détaillé et séquencé.
8. Tableau des opérations multi-tables et garanties d'atomicité.
9. Stratégie de migrations de schéma.
10. Stratégie backup/restore et isolation Workspace.
11. Plan de tests avec résultats NOT TESTED lorsque non exécutés.
12. Fichiers consultés ; confirmer qu'aucun fichier applicatif n'a été modifié.
13. Risques, arbitrages à demander à ChatGPT et prochaine étape recommandée.

Ne jamais présenter une recommandation comme une décision déjà approuvée.

## 8. Publication du rapport

Publier le rapport uniquement dans une branche dédiée :

`ai-handoff/report-MISSION-1-STEP-4B`

Base : dernier `origin/develop`.

Créer un worktree isolé et vérifier que le commit ne contient que le nouveau rapport. Ne pas publier sur `develop`, ne pas fusionner la branche, ne pas créer de PR et ne pas pousser de code applicatif.

Si la publication est impossible, inclure tout le rapport dans la réponse finale et expliquer précisément le blocage.

## 9. Critère de fin

La mission est terminée uniquement lorsque ChatGPT dispose d'un plan suffisamment précis pour prendre une décision éclairée sur l'autorisation d'implémenter la centralisation SQLite, les migrations et la sécurisation backup/restore.

**Aucune implémentation n'est autorisée par cette mission.**
