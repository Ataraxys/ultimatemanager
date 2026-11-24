# MISSION — REVUE INDÉPENDANTE COMPLÈTE DE LA PR #5 (83 FICHIERS)

Date : 2026-11-24  
Statut : AUTORISÉE — REVUE STRICTEMENT READ-ONLY  
Dépôt : `Ataraxys/ultimatemanager`  
PR : [#5 — Intégration réconciliée des formulaires dynamiques et blocs SQLite](https://github.com/Ataraxys/ultimatemanager/pull/5)

## 1. Objectif

Réaliser une revue technique, fonctionnelle, architecturale et orientée intégrité des données de **l'intégralité des 83 fichiers modifiés par la PR #5**, depuis sa base `develop` jusqu'à sa tête actuelle.

La précédente revue indépendante portait uniquement sur le correctif ciblé de restauration fail-closed et de visibilité des templates. Elle n'approuvait pas la PR entière. Cette mission doit donc examiner le périmètre global, identifier les défauts concrets et les décisions produit/architecture encore nécessaires, puis recommander explicitement si la PR peut être approuvée, doit être corrigée ou reste bloquée.

**Cette mission n'autorise aucune modification du code, aucune correction, aucun commit applicatif et aucune fusion.** C'est un audit indépendant en lecture seule.

## 2. Références connues — à rafraîchir obligatoirement

Dernier état GitHub observé le 2026-11-24 :

- PR : `#5`, ouverte, draft, non fusionnée.
- Base : `develop`, SHA `47b6dbf615cd62cdb9c5c0be018dda60310c826d`.
- Head : `integration/reconcile-feature-branches`, SHA `9c76f7f52ed47b1bb0c9f46741c73b9be3374bfb`.
- Volume annoncé : 83 fichiers, +9 744 / -186.
- Mergeability retournée lors de la dernière lecture : `true`. Ce statut technique ne constitue jamais une approbation.

Le nom du fichier conserve le nombre initial annoncé dans le rapport précédent ; le périmètre réel à auditer est le nombre complet de fichiers renvoyé par GitHub. Après publication de cette mission, le total observé est passé à 83 car la mission elle-même est incluse dans le diff de la PR. Ces SHA sont des points de départ, pas une permission d'ignorer l'état actuel.

## 3. Pré-vol obligatoire — STOP si incohérence

Avant toute analyse :

1. Lire `docs/ai-handoff/AGENT_PROTOCOL.md`, `README.md`, `SESSION_STATE.md`, cette mission, les consignes du dépôt et les rapports précédents pertinents.
2. Rafraîchir les références Git distantes et relever les SHA exacts de `develop`, de `integration/reconcile-feature-branches` et de la branche source `feature/block-instances-fix` (cette dernière en lecture seule).
3. Relire les métadonnées de la PR #5 : état, draft, base, head, SHA, nombre de fichiers, commits et mergeability.
4. Récupérer la liste paginée complète des fichiers modifiés par la PR et confirmer le nombre réel. Ne pas se contenter du nombre annoncé ci-dessus.
5. Vérifier que le diff analysé correspond exactement au couple base/head actuel. Si la PR a évolué, analyser son état actuel et documenter précisément les SHA retenus.
6. Si la PR est fusionnée/fermée, si les références ne peuvent pas être établies ou si le diff n'est pas récupérable intégralement, arrêter l'audit et publier un rapport BLOCKED avec les faits. Ne pas improviser une autre base.

Aucune modification de branche ou de fichier applicatif n'est autorisée pendant le pré-vol.

## 4. Périmètre exhaustif

### 4.1 Couverture des fichiers

- Examiner **chacun des 83 fichiers** (ou le nombre réel constaté).
- Pour chaque fichier, déterminer son rôle, la nature du changement, les dépendances et les effets possibles.
- Lire le contenu et le diff pertinents, pas uniquement les noms de fichiers ou les résumés de commits.
- Suivre les appels entre UI, ViewModels, services, modèles, persistance SQLite et tests.
- Produire dans le rapport une matrice exhaustive des fichiers : chemin, examiné oui/non, catégorie, constat ou « aucun problème identifié », et éventuelle limite d'analyse.
- Si une limite technique empêche de lire un fichier ou un patch, le signaler explicitement. Ne jamais qualifier la revue d'exhaustive si un fichier n'a pas été examiné.

### 4.2 Architecture et maintenabilité

Évaluer notamment :
- séparation UI / ViewModels / services / modèles / persistance ;
- responsabilités et cohésion des classes et interfaces ;
- dépendances et injection, durée de vie des services, concurrence et synchronisation ;
- cohérence du modèle objet fortement typé avec le moteur de formulaires dynamiques ;
- identité et cycle de vie des Templates, Blocks, Fields, BlockInstances, EntityRecords et FieldValues ;
- extensibilité future sans sur-ingénierie ni duplication inutile ;
- gestion des erreurs, annulation, états de chargement et cohérence asynchrone.

Ne pas proposer une refonte générale sans défaut concret et démontré.

### 4.3 Intégrité SQLite et cycle de vie des données

Vérifier :
- atomicité des opérations multi-tables et transactions ;
- gestion des connexions SQLite, concurrence et verrouillages ;
- cohérence des clés, identifiants, relations et contraintes ;
- créations partielles, suppressions, annulations et données orphelines ;
- migrations, compatibilité des schémas et conservation des données existantes ;
- isolation par Workspace à chaque lecture, écriture, recherche, export et suppression ;
- sauvegardes, restauration, import/export et résistance aux archives invalides ou incomplètes ;
- absence de chemins pouvant effacer ou mélanger des données d'un autre Workspace.

La restauration doit rester refusée tant qu'une implémentation sûre n'a pas été conçue et validée. Ne pas réactiver ni tenter de tester une restauration réelle.

### 4.4 Fonctionnalités et UX

Vérifier les parcours de bout en bout, notamment :
- création, consultation, modification et suppression d'un client ;
- différence exacte entre « Modifier » (contenu uniquement) et « ✎ » (structure/template complet) ;
- création, sélection, réutilisation, clonage et annulation des Templates ;
- ajout, édition, suppression et répétition de blocs et de champs ;
- plusieurs adresses, téléphones, coordonnées GPS et commentaires ;
- comportement de la carte : sélection de coordonnées uniquement en mode édition de structure, ouverture de Google Maps en consultation ;
- navigation, validation, erreurs et préservation des valeurs saisies.

Signaler les écarts entre le comportement demandé et le code réellement présent. Ne pas considérer une simple compilation comme une validation UX.

### 4.5 Tests et preuves

- Examiner tous les tests ajoutés/modifiés et les harness concernés.
- Évaluer si les tests couvrent réellement les scénarios, et non seulement les méthodes isolées.
- Identifier les faux positifs, scénarios manquants, assertions insuffisantes, tests non exécutables et dépendances à l'environnement.
- Exécuter les tests/builds pertinents si l'environnement le permet, sans modifier le code ni les fichiers de configuration.
- Rapporter les commandes exactes et distinguer PASS, FAIL, BLOCKED et NOT RUN.
- Ne jamais présenter un résultat rapporté par un autre agent comme une exécution personnelle.
- Aucun test ne doit utiliser la vraie base SQLite de l'utilisateur : uniquement des bases temporaires et fixtures isolées.

## 5. Points de vigilance déjà identifiés — à revalider, pas à présumer

Les points suivants viennent de revues antérieures et doivent être réexaminés sur le diff exact de la PR actuelle. Ils ne sont ni automatiquement confirmés ni automatiquement corrigés :

1. **Restauration de sauvegarde** : vérifier que tous les points d'entrée refusent l'opération avant toute mutation et que l'UI l'indique clairement.
2. **Réutilisation des templates** : vérifier le filtrage par Workspace et la visibilité des templates déjà utilisés.
3. **Cycle de vie des templates provisoires** : vérifier si l'ouverture d'une fiche vierge ou le changement de sélection crée des clones/EntityRecords persistés inutiles, et si annuler laisse des objets orphelins.
4. **Réutilisation par plusieurs clients** : vérifier qu'au moins deux clients distincts peuvent utiliser le même template source sans partager leurs valeurs ni modifier la source. Le test historique 40 avait été jugé insuffisant car il réutilisait le même ExternalEntityId ; vérifier si ce manque a été corrigé.
5. **Cardinalité BlockInstance / FieldValue** : vérifier si plusieurs occurrences indépendantes d'une même définition de bloc sont correctement identifiées et restituées, ou si des instances sont fusionnées/collapsées.
6. **Centralisation SQLite** : vérifier quels services utilisent réellement le coordinateur/contrat de connexion et lesquels ouvrent leurs propres connexions.
7. **Suppression d'un client** : vérifier le traitement transactionnel des records, instances et valeurs dynamiques associés.
8. **Isolation Workspace des clients** : vérifier listes, recherches, lectures, écritures, suppressions et exports ; rechercher les accès globaux involontaires.
9. **Annulation de l'édition de structure** : vérifier si les modifications déjà persistées sont réellement annulées ou si l'annulation ne ferme que l'interface.
10. **Portée des sauvegardes** : vérifier que clients et données dynamiques exportés appartiennent au même périmètre Workspace/application.
11. **Validation réelle** : distinguer les tests de services/harness des parcours UI MAUI réellement exécutés.

Pour chaque point : fournir le chemin, la méthode ou le flux concerné, une preuve précise et un scénario reproductible ou expliquer pourquoi le risque n'est pas confirmé.

## 6. Décisions produit / architecture à faire ressortir

Ne pas prendre silencieusement de décision produit à la place de l'utilisateur. Lister séparément les arbitrages nécessaires, avec :
- question précise ;
- options réalistes ;
- conséquences sur données, UX, architecture et évolutivité ;
- recommandation argumentée ;
- urgence : bloquante avant merge ou différable.

En particulier, clarifier si les blocs répétables doivent être supportés comme plusieurs occurrences indépendantes, et quel est le comportement attendu lors d'un changement de template sur une fiche qui possède déjà des données.

## 7. Classification des constats

Chaque constat doit avoir :
- identifiant stable (F-001, F-002…) ;
- gravité : P0 critique, P1 élevée, P2 moyenne, P3 faible ;
- certitude : confirmé, probable, à vérifier ;
- fichiers et lignes/méthodes concernés ;
- preuve issue du code ou d'un test ;
- scénario concret et impact utilisateur/données ;
- recommandation minimale ;
- statut : bloque l'approbation / correction souhaitée / amélioration différable.

Ne pas gonfler la gravité. Distinguer bug confirmé, risque architectural, dette technique et préférence de conception.

## 8. Interdictions et gouvernance Git

- Mission strictement read-only sur le code applicatif.
- Ne modifier aucun fichier source, test, configuration ou documentation du produit, hormis le rapport obligatoire dans `inbox/`.
- Ne créer aucun commit sur `integration/reconcile-feature-branches`, `develop` ou `feature/*`.
- Aucun merge, rebase, cherry-pick, reset, force-push, suppression de branche ou réécriture d'historique.
- Ne pas modifier la PR #5, son titre, sa description, ses reviewers ou son statut.
- Ne pas approuver la PR et ne pas la fusionner.
- Ne pas toucher au worktree protégé `feature/block-instances-fix), ni à sa modification locale préexistante de `UltimateManager.csproj). Ne pas le réutiliser pour l'audit.
- Ne jamais ouvrir, modifier ou tester la vraie base SQLite de l'utilisateur.
- Le rapport ne doit contenir aucun secret, token ou donnée personnelle.

## 9. Publication du rapport obligatoire

Conformément à `AGENT_PROTOCOL.md` :

1. Créer un rapport unique `docs/ai-handoff/inbox/20261124-CODEX-pr5-full-review.txt` (si ce nom est déjà utilisé, choisir un nom unique et le signaler).
2. Créer une branche dédiée `ai-handoff/report-pr5-full-review` basée exactement sur le SHA de `develop` rafraîchi au pré-vol. Si cette branche existe déjà, ne pas écraser son historique ni son rapport : choisir un nom de branche suffixé unique.
3. La branche de rapport ne doit contenir que les fichiers de handoff/rapport nécessaires. Aucun code applicatif.
4. Inclure le rapport intégral dans la réponse finale.
5. Si publication impossible, indiquer clairement BLOCKED et fournir le rapport complet sans prétendre qu'il est publié.

## 10. Format et verdict final

Le rapport doit inclure :

- identité de l'agent, date, PR et identifiant de mission ;
- SHA exacts de develop, head PR et branche source observée ;
- nombre exact de fichiers et matrice des fichiers effectivement examinés ;
- résumé exécutif ;
- constats P0 à P3 détaillés ;
- résultats par domaine : architecture, SQLite/données, templates/blocs, Workspace, UX, sauvegardes, tests ;
- décisions produit/architecture encore requises ;
- commandes exactes exécutées et résultats ;
- limites de l'audit et éléments non testés ;
- confirmation explicite qu'aucun code ni worktree protégé n'a été modifié ;
- état final de la PR (toujours ouverte/draft/non fusionnée ou changement observé) ;
- recommandation de suites, sans exécuter les corrections.

Choisir un verdict unique :

- **APPROVE** : aucun bloqueur confirmé, couverture exhaustive et preuves suffisantes. Cela reste une recommandation d'agent, pas une autorisation de merge.
- **REQUEST CHANGES** : défauts confirmés nécessitant des missions correctives avant approbation.
- **BLOCKED** : impossible de réaliser une revue suffisamment complète ou base incohérente.

Une revue partielle ne peut jamais recevoir le verdict APPROVE.

## 11. Définition de terminé

La mission est terminée uniquement lorsque :
- tous les fichiers modifiés de la PR ont été examinés, ou les limites sont explicitement documentées avec verdict BLOCKED ;
- les constats sont étayés par des preuves concrètes ;
- les décisions produit/architecture sont clairement séparées des bugs ;
- les vérifications sont rapportées honnêtement ;
- le rapport unique est publié dans `inbox/` sur une branche report-only basée sur le SHA courant de `develop` ;
- aucun code n'a été modifié et la PR #5 reste non fusionnée.

**Aucune correction et aucune fusion ne sont autorisées par cette mission.**
