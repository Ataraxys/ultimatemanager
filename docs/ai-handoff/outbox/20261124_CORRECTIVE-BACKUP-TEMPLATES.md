# Mission corrective — restauration fail-closed et réutilisation des templates

Date : 2026-11-24
Statut : AUTORISÉE PAR L'UTILISATEUR
Branche d'implémentation autorisée : `integration/reconcile-feature-branches`
PR concernée : #5 vers `develop`
Cette mission corrective remplace toute instruction antérieure incompatible avec son périmètre. Elle ne réautorise pas la fusion vers `develop`.

## Objectif

Corriger uniquement les deux bloqueurs P1 confirmés par la revue indépendante de la PR #5 :

1. Empêcher toute restauration de sauvegarde susceptible d'effacer ou de corrompre les données.
2. Faire en sorte que les templates d'un Workspace restent visibles et réutilisables, même lorsqu'ils sont déjà associés à un ou plusieurs enregistrements.

Ajouter les tests de non-régression correspondants. Ne pas élargir le périmètre.

## Pré-vol obligatoire — ne rien modifier avant validation

1. Lire `docs/ai-handoff/AGENT_PROTOCOL.md` s'il existe, `docs/ai-handoff/README.md`, `docs/ai-handoff/SESSION_STATE.md`, cette mission et les consignes du dépôt.
2. Actualiser les références distantes et relever les SHA exacts de :
   - `develop`
   - `integration/reconcile-feature-branches`
   - `feature/block-instances-fix` (lecture seule)
3. Vérifier l'état de la PR #5 : base, head, SHA, statut et éventuelles modifications récentes.
4. Vérifier que la branche d'intégration est toujours celle attendue et que le travail peut être réalisé sans réécrire l'historique ni écraser une mise à jour concurrente.
5. Si la branche d'intégration a changé de manière inattendue, si la PR a été fusionnée/fermée, ou si les références ne permettent pas d'établir une base sûre : STOP, aucun changement, rapporter les faits et demander arbitrage.

## Protection du worktree

- Ne pas réutiliser ni changer de branche dans le worktree local préexistant sur `feature/block-instances-fix`.
- Ne pas toucher, écraser, stasher, réinitialiser ou supprimer son diff local préexistant dans `UltimateManager.csproj`.
- Utiliser un worktree séparé et propre pour `integration/reconcile-feature-branches`.
- Aucun `reset --hard`, `clean`, force-push, rebase ou cherry-pick.
- Ne jamais utiliser la vraie base SQLite de l'utilisateur : tests sur bases temporaires uniquement.

## Correction 1 — restauration de sauvegarde sécurisée

Fichier signalé lors de la revue : `Services/CompleteBackupService.cs`.

Constat à traiter : la restauration actuelle supprime des données existantes avant d'avoir validé intégralement l'archive et sans garantie transactionnelle globale. Une archive incomplète ou une erreur en cours de restauration peut donc provoquer une perte de données.

Travail autorisé :
- Identifier précisément tous les points d'entrée UI/service permettant de lancer une restauration.
- Mettre la restauration en mode fail-closed : refuser/neutraliser l'opération avant toute écriture ou suppression en base.
- Le refus doit être explicite pour l'utilisateur et ne doit pas donner l'impression qu'une restauration a réussi.
- Ne pas supprimer les fonctions d'export/backup si elles sont indépendantes et sûres.
- Ajouter un test sur base SQLite temporaire prouvant qu'une tentative de restauration refusée échoue avant toute mutation : les lignes préexistantes doivent rester strictement identiques.
- Si le code permet de tester l'absence de mutation sans appeler des chemins destructifs, le faire via une dépendance/fake ou un point d'entrée contrôlé.

Interdictions :
- Ne pas tenter une implémentation partielle de restauration transactionnelle.
- Ne pas changer le format d'archive.
- Ne pas introduire de politique Workspace/global archive.
- Ne pas effectuer de suppression préalable, même « pour nettoyer ».
- Ne pas laisser un bouton actif qui déclenche une restauration destructive. Si nécessaire, désactiver le bouton et expliquer clairement la fonctionnalité temporairement indisponible.
- Ne pas annoncer qu'une restauration sûre est implémentée : la restauration doit rester indisponible jusqu'à une mission dédiée de conception et d'implémentation complète.

Critère d'acceptation : tout chemin de restauration est refusé avant la première écriture en base ; les données de test restent intactes.

## Correction 2 — réutilisation des templates

Fichier signalé lors de la revue : `Services/SqliteDynamicFormService.cs`, méthode `GetTemplatesAsync`.

Constat à traiter : la méthode filtre les templates déjà référencés par des enregistrements, ce qui les fait disparaître de la liste de sélection après leur première utilisation.

Travail autorisé :
- Pour le Workspace courant, retourner tous les templates qui lui appartiennent, qu'ils soient utilisés ou non.
- Conserver le filtrage Workspace existant et les règles de tri pertinentes.
- Ne pas modifier le comportement d'autres services ou la structure du modèle.
- Ajouter un test de non-régression sur base temporaire : créer deux templates dans le Workspace courant, associer l'un d'eux à un enregistrement, puis vérifier que les deux templates restent retournés et que le template déjà utilisé reste sélectionnable/réutilisable.
- Ajouter si utile un cas confirmant qu'un template d'un autre Workspace n'est pas exposé.

Critère d'acceptation : l'utilisation d'un template ne le retire jamais de la liste des templates disponibles pour son Workspace.

## Hors périmètre strict

Ne pas modifier :
- le modèle de cardinalité des `BlockInstance` / `FieldValue` ni ajouter `BlockInstanceId` ;
- la centralisation globale des connexions SQLite ou le coordinateur ;
- la suppression des clients et le nettoyage des enregistrements orphelins ;
- l'isolation Workspace des clients ;
- la logique d'annulation des modifications de structure ;
- les parcours UX Modifier / ✎, sauf le strict nécessaire pour rendre la restauration indisponible ;
- les migrations, le schéma SQLite, le format de sauvegarde ou les templates au-delà du correctif demandé ;
- toute refonte générale, nettoyage opportuniste ou changement sans rapport.

Si un des sujets hors périmètre bloque réellement les deux corrections, arrêter et expliquer pourquoi au lieu d'élargir la mission.

## Tests et validations obligatoires

Sur la branche d'intégration :
1. Exécuter `dotnet build UltimateManager.csproj --configuration Debug --framework net10.0-android`.
2. Exécuter `dotnet run --project Tests/SQLiteCoordinatorHarness/SQLiteCoordinatorHarness.csproj --configuration Debug`.
3. Ajouter et exécuter les tests ciblés décrits ci-dessus.
4. Vérifier le diff complet, les statuts Git, les chemins UI de restauration et l'absence de mutation dans le test de restauration.
5. Faire un contrôle manuel des parcours concernés si un appareil/émulateur est disponible. Sinon écrire explicitement `NOT TESTED`. Une compilation ne prouve pas le comportement UI.

Pour chaque commande : rapporter la commande exacte et le résultat réel PASS / FAIL / NOT TESTED. Ne jamais reprendre les anciens résultats comme preuve d'une nouvelle exécution.

## Livraison et gouvernance Git

- Commits correctifs uniquement sur `integration/reconcile-feature-branches`.
- Push uniquement de cette branche après vérification du diff.
- Mettre à jour la PR #5 existante vers `develop`, sans la fusionner.
- Ne pas pousser sur `develop`, ne pas merger la PR, ne pas modifier `master`.
- Mettre à jour `docs/ai-handoff/SESSION_STATE.md` pour refléter précisément cette mission, les SHA réellement observés, les validations et le statut actuel de la PR. Ne pas recopier les anciens statuts comme s'ils étaient actuels.
- Publier le rapport complet dans un fichier unique sous `docs/ai-handoff/inbox/` sur une branche dédiée `ai-handoff/report-corrective-backup-templates`, basée sur le SHA actuel de `develop`, contenant uniquement le rapport et les fichiers de gouvernance strictement nécessaires. Ne pas y inclure de code applicatif.
- Inclure le rapport intégral dans la réponse finale.

## Format du rapport

Inclure :
- SHA de `develop`, de la branche d'intégration avant/après, de la branche source consultée et de la PR #5 ;
- fichiers modifiés et justification de chaque modification ;
- comportement exact avant/après pour les deux corrections ;
- tests ajoutés, commandes exactes, résultats PASS / FAIL / NOT TESTED ;
- confirmation que la restauration est refusée avant toute mutation ;
- confirmation que les templates utilisés restent visibles et réutilisables ;
- état du worktree local protégé et confirmation qu'il n'a pas été touché ;
- état de la PR #5 ;
- risques résiduels et sujets explicitement laissés hors périmètre ;
- verdict recommandé : prêt pour revue indépendante ou encore bloqué.

## Définition de terminé

Mission terminée uniquement si les deux corrections sont implémentées et couvertes par des tests exécutés, le build et le harness sont rapportés sans ambiguïté, le diff est relu, la PR #5 reste non fusionnée, et le rapport est publié sur la branche report-only dédiée.