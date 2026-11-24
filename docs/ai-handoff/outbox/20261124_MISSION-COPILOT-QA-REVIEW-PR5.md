# MISSION — COPILOT QA REVIEWER — AUDIT COMPLET PR #5

Date : 2026-11-24
Agent à sélectionner dans GitHub Copilot : `@qa-reviewer`
Statut : AUTORISÉE — REVUE STRICTEMENT READ-ONLY
Dépôt : `Ataraxys/ultimatemanager`
PR : https://github.com/Ataraxys/ultimatemanager/pull/5

## 1. Objectif et document de référence

Tu reprends la mission d'audit complet précédemment confiée à Codex, désormais exécutée avec GitHub Copilot faute de quota Codex.

Lis intégralement et applique la mission existante :
`docs/ai-handoff/outbox/20261124_MISSION-PR5-REVUE-COMPLETE-82-FICHIERS.md`

Elle reste la référence détaillée du périmètre, des contrôles, des risques connus, du format des constats et du verdict. Le nombre de fichiers doit toujours être déterminé depuis GitHub au moment de l'audit, et non supposé à partir du nom de fichier.

Lis également, avant tout travail :
- `docs/ai-handoff/AGENT_PROTOCOL.md`
- `README.md`
- `docs/ai-handoff/SESSION_STATE.md`
- les consignes Copilot et les profils d'agents concernés sous `.github/`
- le rapport précédent `docs/ai-handoff/inbox/20261124-CODEX-pr5-corrective-review.txt`

## 2. Pré-vol obligatoire

Rafraîchis les références distantes et vérifie :
- état actuel de la PR #5, base, head, SHA exacts, nombre de fichiers et mergeability ;
- liste complète des fichiers modifiés ;
- SHA actuel de `develop`, de `integration/reconcile-feature-branches` et de `feature/block-instances-fix` (cette dernière en lecture seule).

Compare exactement la base et la tête analysées. Si la PR a changé depuis la mission initiale, audite le diff actuel et consigne les SHA retenus. Si les références ou le diff complet ne sont pas accessibles, arrête-toi avec un verdict BLOCKED et explique précisément pourquoi.

## 3. Travail demandé

Effectue une revue indépendante exhaustive de tous les fichiers du diff PR #5 :
- architecture, responsabilités, dépendances et maintenabilité ;
- modèles dynamiques, Templates, Blocks, Fields, BlockInstances, EntityRecords et FieldValues ;
- SQLite, transactions, concurrence, migrations, connexions et intégrité des données ;
- isolation Workspace pour chaque lecture, écriture, recherche, suppression et export ;
- cycle de vie des templates, réutilisation entre clients, annulation et données orphelines ;
- suppression des clients et cohérence des données associées ;
- sauvegarde et export ; vérifier que toute restauration reste refusée avant toute mutation ;
- parcours MAUI et règles UX, notamment « Modifier » = contenu uniquement et « ✎ » = structure/template complet ;
- tests, couverture réelle, scénarios manquants et limites de validation.

Revalide tous les points de vigilance de la mission de revue complète. Pour chaque défaut, donne une preuve localisable, un scénario concret, l'impact, la gravité P0–P3, le niveau de certitude et la correction minimale recommandée. Sépare clairement les bugs confirmés, risques, dette technique et décisions produit.

Produis une matrice couvrant chaque fichier modifié, y compris ceux pour lesquels aucun problème n'est identifié. Ne qualifie pas la revue d'exhaustive si un fichier n'a pas été lu.

## 4. Vérifications

Exécute les tests/builds pertinents seulement si l'environnement le permet, sans modifier le code ni la configuration. Indique les commandes exactes et les résultats PASS / FAIL / BLOCKED / NOT RUN. Une compilation ne vaut pas validation UX.

N'utilise jamais la vraie base SQLite de l'utilisateur : uniquement des bases temporaires et fixtures isolées. Ne prétends pas avoir exécuté les commandes d'un autre agent.

## 5. Interdictions strictes

Cette mission est un audit read-only :
- ne modifie aucun code, test, configuration ou documentation produit ;
- ne fais aucune correction et aucun commit applicatif ;
- ne modifie pas la PR, ne l'approuve pas et ne la fusionne pas ;
- aucun merge, rebase, cherry-pick, reset, force-push ou suppression de branche ;
- ne touche pas au worktree protégé `feature/block-instances-fix` ni à ses changements locaux ;
- ne modifie ni `develop` ni la branche d'intégration ;
- ne réactive pas la restauration de sauvegarde et ne teste aucune restauration réelle.

Tu peux uniquement publier le rapport obligatoire dans `docs/ai-handoff/inbox/` sur une branche report-only.

## 6. Rapport obligatoire

Crée un rapport unique au format `.txt`, par exemple :
`docs/ai-handoff/inbox/20261124-COPILOT-QA-pr5-full-review.txt`
Si le nom existe déjà, choisis un nom unique sans écraser un rapport précédent.

Publie-le sur une branche dédiée :
`ai-handoff/report-copilot-pr5-full-review`
Cette branche doit être créée depuis le SHA de `develop` rafraîchi au pré-vol et ne contenir que les fichiers de handoff/rapport. Si elle existe déjà, ne réécris pas son historique : choisis un nom suffixé unique.

Le rapport doit inclure :
- identité de l'agent, date et identifiant de mission ;
- SHA exacts de `develop`, head PR et branche source observée ;
- nombre réel de fichiers et matrice exhaustive ;
- résumé exécutif et constats détaillés P0–P3 ;
- résultats par domaine et décisions produit/architecture à arbitrer ;
- commandes exactes et résultats de vérification ;
- limites et éléments non testés ;
- confirmation qu'aucun code ni worktree protégé n'a été modifié ;
- état final de la PR ;
- un verdict unique : APPROVE, REQUEST CHANGES ou BLOCKED.

Une revue partielle ne peut jamais conclure APPROVE. Inclus le rapport intégral dans ta réponse finale et indique la branche, le commit et le statut réel de publication. Si GitHub refuse la publication, dis-le explicitement : ne prétends jamais que le rapport est publié.

## 7. Coordination

Tu es l'agent principal de cette mission. Ne délègue pas implicitement et ne suppose pas qu'un autre agent a fait une vérification. Si tu demandes ensuite une analyse spécialisée à `@project-architect`, `@backend-specialist` ou `@maui-specialist`, chaque agent doit produire son propre rapport distinct, et tu dois consolider leurs preuves sans les présenter comme tes propres tests.

Aucune approbation de merge n'est accordée par cette mission.
