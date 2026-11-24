# Mission — Réconciliation des branches et intégration vers develop

Date : 2026-11-24
Statut : AUTORISÉE PAR L'UTILISATEUR — intégration demandée, publication finale sur develop autorisée après validation.

## Objectif

Produire une version cohérente d'UltimateManager en intégrant le travail applicatif de `feature/block-instances-fix` (qui contient déjà `feature/add-personalisation`) dans la base actuelle `develop`, puis préparer une PR validée vers `develop`.

Ne pas fusionner séparément `feature/add-personalisation` et `feature/block-instances-fix` : la seconde est déjà descendante de la première.

## Références vérifiées au lancement

- `develop` : `47b6dbf615cd62cdb9c5c0be018dda60310c826d`
- `feature/add-personalisation` : `2bbcd1b35c9481794e5d4f9be379e8fb486c4560`
- `feature/block-instances-fix` : `bde661d52b811ab7e06278a64faf64f467354daf`
- Ancêtre commun des branches feature et develop : `2910b5c9e89591aa84c5af451851663adc4ecf41`
- Comparaison feature : `feature/block-instances-fix` est en avance de 2 commits sur `feature/add-personalisation`, sans commit en retard. Les 2 commits supplémentaires touchent `Services/SqliteDynamicFormService.cs`, `Tests/SQLiteCoordinatorHarness/Program.cs`, `ViewModels/ClientDynamicRecordViewModel.cs`, `ViewModels/TemplateEditorViewModel.cs`, `Views/TemplateEditorPage.xaml.cs`.
- Branche d'intégration créée sur GitHub : `integration/reconcile-feature-branches`, initialement basée sur `develop` SHA ci-dessus. Cette branche est le seul espace de travail autorisé pour la réconciliation.

## Première action obligatoire

Lire intégralement :
- `docs/ai-handoff/AGENT_PROTOCOL.md` s'il existe ;
- `docs/ai-handoff/SESSION_STATE.md` ;
- `docs/ai-handoff/README.md` ;
- les instructions GitHub/Copilot applicables ;
- ce brief.

Actualiser `origin` et vérifier les SHA actuels de `develop`, `feature/add-personalisation`, `feature/block-instances-fix` et `integration/reconcile-feature-branches`.

Si une branche source a avancé depuis les SHA ci-dessus, ou si la branche d'intégration n'est pas basée sur le SHA develop attendu, arrêter et rapporter le changement. Ne jamais écraser une mise à jour concurrente.

## Sécurité du worktree

- NE PAS réutiliser le worktree actuellement sur `feature/block-instances-fix`.
- NE PAS changer de branche dans ce worktree.
- NE PAS toucher, écraser, stasher, réinitialiser ou supprimer le diff local préexistant de `UltimateManager.csproj` dans ce worktree.
- Créer un worktree séparé et propre pour `integration/reconcile-feature-branches`.
- Préserver les deux branches sources ; aucune réécriture de leur historique.
- Aucun `reset --hard`, `clean`, force-push, rebase ou cherry-pick destructif.
- Les opérations Git doivent rester sur la branche d'intégration. Aucun push direct sur `develop` pendant l'exécution.

## Travail demandé

1. Inspecter le diff complet entre `develop` et `feature/block-instances-fix`, y compris les commits, les fichiers supprimés/renommés, les changements XAML, les services, les ViewModels, la configuration projet, les tests et les documents.
2. Vérifier la relation exacte des deux branches feature ; ne pas appliquer deux fois les commits de `feature/add-personalisation`.
3. Fusionner `feature/block-instances-fix` dans `integration/reconcile-feature-branches` avec un merge commit normal. Ne pas utiliser squash/rebase.
4. Résoudre les conflits en conservant les modifications récentes de `develop` et les fonctionnalités pertinentes de la branche feature. Ne pas résoudre mécaniquement en choisissant systématiquement ours/theirs.
5. Auditer les points critiques avant de considérer la fusion terminée :
   - séparation exacte Modifier (contenu uniquement) / ✎ (structure complète) ;
   - persistance et réutilisation des templates ;
   - changement de TemplateId et conservation des records/valeurs (aucune suppression silencieuse) ;
   - BlockDefinition / BlockInstance et cardinalité réelle des blocs ;
   - SQLiteDatabaseCoordinator et services qui ouvrent encore des connexions indépendantes ;
   - isolation Workspace ;
   - sauvegardes/restaurations et risque d'effacement de données ;
   - compatibilité des modèles et de la base existante.
6. Ne pas entreprendre de refonte générale ni ajouter de fonctionnalités non demandées. Si un défaut bloque la sécurité ou le fonctionnement, corriger uniquement si la correction est locale, testée et dans le périmètre ; sinon documenter comme bloqueur.
7. Actualiser les documents de référence seulement pour refléter fidèlement ce qui est effectivement intégré. Ne pas déclarer comme terminées les fonctionnalités non testées.

## Tests obligatoires

Utiliser exclusivement des bases SQLite temporaires/jetables, jamais la base réelle de l'utilisateur.

Exécuter et rapporter :
- `dotnet build UltimateManager.csproj --configuration Debug --framework net10.0-android`
- `dotnet run --project Tests/SQLiteCoordinatorHarness/SQLiteCoordinatorHarness.csproj --configuration Debug`
- tests supplémentaires ciblés sur les conflits/corrections et sur la conservation des données ;
- vérifier le diff final et le statut Git.

Faire un contrôle manuel si l'environnement le permet pour les parcours MAUI critiques ; sinon indiquer explicitement NOT TESTED. Une compilation ne prouve pas le comportement UX.

Tout test non exécuté doit être noté NOT TESTED. Ne jamais inventer de résultat.

## Livraison

- Commit de merge et éventuels commits de correction uniquement sur `integration/reconcile-feature-branches`.
- Push de cette branche d'intégration sur GitHub.
- Créer une pull request vers `develop`, de préférence en brouillon tant que les validations ne sont pas terminées.
- Ne pas merger la PR vers `develop` avant que les tests et l'audit soient terminés.
- L'utilisateur a autorisé l'objectif final de fusion vers `develop`, mais cette mission ne doit pas publier un résultat non validé. Après validation, présenter SHA, résultats et risques ; la fusion finale dans `develop` pourra alors être faite.
- Produire un rapport complet et factuel dans `docs/ai-handoff/inbox/` sur une branche dédiée `ai-handoff/report-integration-reconciliation` basée sur le SHA actuel de `develop`, sans code applicatif. Inclure le rapport intégral dans la réponse finale.
- Le rapport doit distinguer faits, déductions et hypothèses ; indiquer fichiers modifiés, conflits, commandes, PASS/FAIL/NOT TESTED, SHA de base et SHA final, état de la PR, risques et décision recommandée.

## Critères d'acceptation

La mission est terminée uniquement si :
- la branche d'intégration contient develop + les changements réconciliés de feature ;
- aucun travail local préexistant n'a été perdu ou modifié ;
- build Android Debug et harness passent, ou les échecs/blocages sont explicitement documentés ;
- les risques de perte de données et les comportements destructifs sont identifiés et traités ou bloquants ;
- une PR vers develop existe avec un diff relisible ;
- le rapport complet est disponible ;
- aucune modification directe de develop n'a été faite pendant cette mission.
