# MISSION-1-STEP-4C — Implémentation SQLite Workspace et DynamicForm

## 1. Mission

Implémenter, dans le dépôt `Ataraxys/ultimatemanager`, la centralisation SQLite de `LocalWorkspaceContext` et `SqliteDynamicFormService`, le versionnement de schéma et l'atomicité des opérations multi-écritures, conformément à la revue approuvée :

`docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4B-CHATGPT-REVIEW.md`

Lire également le rapport Step 4B :

`docs/ai-handoff/inbox/20261123_MISSION-1-STEP-4B_CODEX.txt`

## 2. Prérequis et référence

- Branche de travail applicative : créer une branche dédiée depuis `develop` à jour.
- Lire `docs/ai-handoff/SESSION_STATE.md`, la revue Step 4B et les instructions Copilot du dépôt.
- Vérifier l'état Git initial et ne jamais écraser de changements préexistants.
- Ne pas utiliser la branche `feature/add-personalisation` comme base sans vérifier son écart avec `develop`; expliquer clairement la base retenue.
- Ne pas fusionner, ne pas ouvrir de PR et ne pas pousser de code applicatif vers `develop` sans autorisation explicite.

## 3. Périmètre autorisé

### 3.1 Coordination SQLite

- Faire de `SQLiteDatabaseCoordinator` / `ISqliteDatabase` l'unique propriétaire de la connexion SQLite applicative.
- Migrer `LocalWorkspaceContext` et `SqliteDynamicFormService` vers cette abstraction.
- Supprimer leurs connexions privées, verrous indépendants et initialisations DDL lancées depuis les constructeurs.
- Garantir une initialisation explicitement attendue avant toute requête.
- Une opération métier acquiert la coordination une seule fois; les helpers internes réutilisent la connexion déjà détenue.
- Les callbacks transactionnels sont synchrones : aucun `await`, aucune réentrée dans le coordinateur, aucun appel à un service qui reprend le verrou.

### 3.2 Migrations

- Utiliser `PRAGMA user_version` comme autorité unique.
- Introduire un runner centralisé si nécessaire, sans deuxième table de version redondante.
- Base neuve : créer le schéma courant et fixer sa version.
- Base existante version 0 : inspecter les tables, colonnes et index; n'adopter que les formes prouvées par le code/historique/fixtures.
- Schéma inconnu ou partiel : échouer sans ALTER, DELETE, recréation ou déduplication automatique.
- Chaque migration vérifie préconditions et postconditions, s'exécute dans une transaction et modifie `user_version` dans la même transaction.
- Une version future est refusée explicitement.
- Supprimer les `catch(SQLiteException)` génériques qui masquent les erreurs.
- Si une sauvegarde pré-migration vérifiée ne peut pas être garantie par les APIs déjà validées, ne pas prétendre que la migration est protégée : arrêter et rapporter le blocage. Ne pas implémenter un mécanisme de backup improvisé dans cette mission.

### 3.3 Opérations DynamicForm

- Auditer toutes les méthodes multi-écritures du service et envelopper chaque use case atomiquement.
- Valider les entrées et ressources nécessaires avant la première mutation.
- `GetOrCreateRecordAsync` ne doit plus supprimer l'ancien record avant validation du nouveau template.
- Si un record existe avec un template différent, refuser le remplacement implicite et préserver intégralement l'ancien record, ses valeurs et ses instances.
- Les erreurs à chaque étape doivent provoquer un rollback complet.
- Ne pas modifier silencieusement les règles de sélection/visibilité des templates.
- Pour suppression de champs/blocs, ne pas purger silencieusement les valeurs existantes. Si la politique de conservation ne peut pas être respectée proprement dans le périmètre, refuser l'opération et documenter le blocage plutôt que perdre des données.
- Préserver les filtres Workspace existants et ne pas modifier l'isolation Workspace.

## 4. Hors périmètre strict

Ne pas modifier :
- `CompleteBackupService`, ses flux ZIP, sa restauration ou ses pièces jointes.
- `ClientBackupService`, `AutomaticBackupService`, import/export JSON ou les modèles Client.
- Le schéma Client ou ajout de `WorkspaceId` aux Clients.
- La structure métier des Workspaces, permissions, groupes ou utilisateurs.
- Le moteur dynamique au-delà des corrections transactionnelles et de conservation strictement nécessaires.
- L'interface, les ViewModels et le comportement des boutons.
- Les packages NuGet ou frameworks de test sans justification explicite dans le rapport.
- La base SQLite réelle de l'utilisateur.

Ne pas considérer `CompleteBackupService` comme centralisé ou sécurisé après cette mission. Il reste un chantier Step 4D.

## 5. Stratégie d'exécution

Découper le travail en petites étapes vérifiables :
1. Cartographier les accès et opérations concernés; identifier les risques de compatibilité.
2. Implémenter runner/versionnement et bootstrap sans changer le schéma métier non justifié.
3. Migrer `LocalWorkspaceContext`.
4. Migrer `SqliteDynamicFormService` et rendre ses use cases atomiques.
5. Ajouter/étendre les tests et le failure injection.
6. Exécuter les tests puis le build Android Debug si l'environnement le permet.
7. Relire le diff complet pour détecter les changements hors périmètre.

Si une décision non couverte par la revue est nécessaire, arrêter cette partie et demander arbitrage au lieu d'inventer une règle métier.

## 6. Tests obligatoires

Utiliser exclusivement des bases et fichiers temporaires isolés. Ne jamais ouvrir la base réelle de l'utilisateur.

Tester au minimum :
- création d'une base neuve et vérification de `user_version`;
- initialisation concurrente et répétée;
- adoption d'une forme existante prouvée;
- schéma inconnu/partiel et version future refusés sans mutation;
- migration interrompue : rollback, version inchangée, données conservées;
- template multi-blocs/champs cloné entièrement ou pas du tout;
- échec injecté après chaque écriture dans les opérations multi-tables;
- création de record et instances atomique;
- template cible invalide : ancien record/valeurs/instances inchangés;
- template différent pour un record existant : refus non destructif;
- sauvegarde de données d'un autre Workspace inchangées pendant les opérations;
- suppression de champ/bloc : aucune perte silencieuse de valeurs;
- aucune connexion privée restante dans les deux services migrés.

Rapporter chaque test PASS / FAIL / NOT TESTED, avec commande, résultat et limites. Ne jamais assimiler inspection statique à un test runtime.

## 7. Critères de sortie

- Diff limité au périmètre autorisé.
- Aucun writer privé dans `LocalWorkspaceContext` ou `SqliteDynamicFormService`.
- Initialisation centralisée, explicite et fail-closed.
- Transactions effectives et rollback prouvé par tests.
- Anciennes données préservées en cas d'erreur ou de mismatch de template.
- Aucun changement UI ou règle métier non approuvé.
- Tests et build rapportés honnêtement.
- Mise à jour des documents d'architecture/decisions uniquement si la décision est effectivement implémentée; distinguer décisions acceptées et limitations restantes.

## 8. Git et remise

- Travailler sur une branche applicative dédiée créée depuis `develop`.
- Pousser la branche applicative si possible, sans merge, sans PR et sans push vers `develop`.
- Créer une branche de rapport dédiée basée sur `develop`, contenant uniquement le rapport final .txt.
- Nom suggéré : `ai-handoff/report-MISSION-1-STEP-4C`.
- Chemin du rapport : `docs/ai-handoff/inbox/20261123_MISSION-1-STEP-4C_CODEX.txt`.
- Ne jamais inclure de code applicatif dans la branche de rapport.
- Si les contraintes Git empêchent une remise propre, ne pas forcer : expliquer exactement le blocage.

## 9. Rapport final obligatoire

La réponse finale Codex doit contenir le rapport complet en Markdown, copiable directement dans la conversation, ET le même contenu doit être sauvegardé dans le fichier .txt ci-dessus.

Le rapport doit inclure :
- statut et résumé exécutif;
- branche de base, branche de travail, commits et état Git;
- fichiers modifiés/créés/supprimés;
- architecture et décisions effectivement implémentées;
- inventaire des opérations transactionnelles;
- stratégie de migration et schémas reconnus/refusés;
- tests détaillés PASS / FAIL / NOT TESTED avec commandes et résultats;
- build Android Debug PASS / FAIL / NOT TESTED;
- risques, limites, décisions non résolues et compatibilité;
- confirmation explicite qu'aucune base réelle n'a été utilisée;
- prochaine recommandation.

Ne jamais annoncer une étape terminée si son code, ses tests ou ses preuves ne le démontrent pas.
