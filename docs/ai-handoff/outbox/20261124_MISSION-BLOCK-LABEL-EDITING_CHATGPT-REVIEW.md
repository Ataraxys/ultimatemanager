# Revue indépendante — Mission BLOCK-LABEL-EDITING

Date : 2026-11-24  
Reviewer : ChatGPT  
Verdict : **REQUEST CHANGES — correction techniquement prometteuse, livraison non conforme et validation UI incomplète**

## 1. Éléments examinés

- Rapport : `docs/ai-handoff/inbox/20260101-0100_MISSION-BLOCK-LABEL-EDITING_STEP-1_MAUI-SPECIALIST.txt`
- Commit applicatif déclaré : `1ae541ba86a77eaebac581114f06f008c8e7ea8d`
- Branche de rapport : `ai-handoff/report-block-label-editing`
- Branche applicative de référence au départ : `feature/add-personalisation`, SHA `2bbcd1b35c9481794e5d4f9be379e8fb486c4560`
- Fichiers examinés : `Services/SqliteDynamicFormService.cs`, `ViewModels/ClientDynamicRecordViewModel.cs`, `ViewModels/ClientEditViewModel.cs`, `Views/ClientEditPage.xaml`, `Views/ClientEditPage.xaml.cs`, `Models/DynamicForms/DynamicFormModels.cs`, tests du harness et rapport agent.
- La revue est basée sur le rapport et l'inspection GitHub du diff/code. Les tests annoncés n'ont pas été réexécutés indépendamment et l'application n'a pas été exécutée sur appareil.

## 2. Points positifs

### P1 — Blocs ajoutés après création

Le diagnostic est cohérent : les définitions de blocs ajoutées au template après la création du record n'avaient pas d'instance correspondante. Le chargement de la fiche pouvait donc ne pas restituer correctement les valeurs de ces nouveaux blocs.

La méthode `EnsureBlockInstancesAsync` appelée depuis `GetRecordAsync` répond à ce scénario et paraît idempotente dans un usage séquentiel. Le test 30 couvre bien le cas central et le test 31 couvre le clonage de template.

### P2 — Labels vides et dupliqués

Le code confirme que les identifiants des champs sont des Guid indépendants du label. Le service accepte les labels vides et les labels dupliqués. Dans le parcours UI de la fiche, `ClientDynamicRecordViewModel.AddFieldAsync` substitue toutefois un label vide par un libellé par défaut lié au type. Pour un bloc, le nom vide devient « Nouveau bloc ». C'est un fallback explicite, pas une collision d'identité.

Le rapport distingue correctement le comportement du service et celui de l'interface. Le cas « Annuler » (`null`) reste distinct du cas « Ajouter » avec texte vide.

### P3 — Renommage des labels

Le XAML de `ClientEditPage` contient déjà un `Entry` lié à `DynamicRecordFieldItem.Label`, visible en mode structure. Le mode contenu affiche un `Label` non éditable. Le changement de `SaveTemplateChangesAsync` relit les définitions courantes et ne modifie que le label, ce qui réduit le risque d'écraser des propriétés avec un objet ViewModel périmé.

## 3. Réserves et demandes de correction

### R1 — Violation du protocole de branches (bloquant)

Le commit applicatif `1ae541ba86a77eaebac581114f06f008c8e7ea8d` est déclaré comme étant sur `ai-handoff/report-block-label-editing`.

La comparaison GitHub confirme que cette branche contient des modifications applicatives (`Services/SqliteDynamicFormService.cs`, ViewModel, tests et UI/template) ainsi que des modifications d'instructions d'agents. Elle est à 4 commits devant `feature/add-personalisation` à `2bbcd1b...`. Elle n'est donc pas une branche de rapport uniquement.

Cela contrevient à `docs/ai-handoff/README.md` et `AGENT_PROTOCOL.md` : une branche `ai-handoff/report-*` ne doit contenir que les fichiers de passation et ne doit jamais porter de code produit.

**Action requise :**
- Ne pas fusionner ni utiliser cette branche comme base d'implémentation.
- Replacer/reproduire les modifications applicatives dans une branche d'implémentation autorisée, basée sur le SHA applicatif explicitement retenu.
- Publier le rapport seul sur une branche de rapport dédiée basée sur le `develop` actuel.
- Ne pas effectuer de cherry-pick, rebase ou transfert destructif sans autorisation explicite.
- Séparer les modifications d'intégration des agents du correctif de fiche : elles ne doivent pas être embarquées dans la livraison du correctif fonctionnel.

### R2 — Réconciliation lors d'une lecture non transactionnelle (à corriger ou justifier)

`EnsureBlockInstancesAsync` effectue successivement :
1. lecture des définitions ;
2. lecture des instances existantes ;
3. insertion des instances manquantes.

Cette séquence n'est pas atomique et aucune unicité composite sur `(EntityRecordId, BlockDefinitionId)` n'est visible dans le modèle. Deux chargements simultanés du même record peuvent donc tous deux constater l'absence d'une instance puis insérer deux lignes pour la même définition.

**Action requise :**
- Sérialiser/transactionnaliser la réconciliation selon l'abstraction SQLite réellement utilisée par la branche cible.
- Garantir l'idempotence en cas d'appels concurrents, et pas seulement séquentiels.
- Ajouter un test de concurrence ou une preuve équivalente.
- Si une contrainte unique/migration est proposée, vérifier les données existantes et définir une stratégie sûre avant de l'ajouter.

### R3 — Validation réelle de l'interface manquante (bloquant pour clôturer)

Le rapport indique explicitement que les tests MAUI/UI n'ont pas été exécutés. Les tests du harness valident le service SQLite, pas le binding XAML, les commandes des boutons, ni les transitions d'écran.

**Action requise :**
- Exécuter manuellement sur Android ou Windows :
  1. ouvrir une fiche existante ;
  2. ajouter un bloc en mode `✎` ;
  3. ajouter plusieurs champs, dont un avec label vide ;
  4. enregistrer la structure ;
  5. ouvrir « Modifier », renseigner les champs du bloc récent ;
  6. enregistrer, quitter, rouvrir la fiche ;
  7. vérifier la persistance de toutes les valeurs ;
  8. revenir en mode `✎`, renommer un label, enregistrer et vérifier que les valeurs restent liées au même champ ;
  9. vérifier que le mode « Modifier » ne permet pas de modifier les labels ni la structure ;
  10. vérifier que la consultation reste en lecture seule.
- Joindre les résultats précis au rapport.

### R4 — Preuve de build insuffisamment précise

Le rapport indique `run_build UltimateManager.csproj` réussi, sans fournir la commande shell exacte ni les sorties pertinentes.

**Action requise :** fournir la commande exacte, la configuration/framework ciblé, le résultat et les warnings éventuels.

## 4. Décision sur les trois demandes produit

| Demande | Évaluation |
|---|---|
| Les blocs récents ne sont pas modifiables / leurs valeurs ne sont pas relues | Cause identifiée et correctif service plausible ; validation UI encore nécessaire |
| L'ajout sans label échoue parfois | Pas de défaut de persistance reproduit ; l'UI utilise un label par défaut si l'utilisateur valide un champ vide. « Annuler » abandonne bien l'ajout |
| Modifier le label en mode ✎ | Le contrôle UI existait déjà ; le correctif fiabilise la sauvegarde du label, mais la validation visuelle et le rechargement restent à faire |

## 5. Décision finale

**Ne pas clôturer la mission et ne pas intégrer la branche actuelle.**

Le correctif de réconciliation et le renommage sont techniquement prometteurs, et les tests 30 à 33 annoncés sont pertinents. Mais la livraison viole la séparation entre branche applicative et branche de rapport, la réconciliation doit être protégée contre les appels concurrents, et le parcours UI réel n'a pas été validé.

La prochaine intervention doit être une correction ciblée par le MAUI Specialist, avec appui du Backend Specialist pour la garantie transactionnelle/concurrente, puis revue indépendante du QA Reviewer. Aucun merge n'est autorisé par cette revue.
