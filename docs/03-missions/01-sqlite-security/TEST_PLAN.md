# Mission 1 — Plan de tests SQLite, sauvegarde et restauration

## Statut

**Planifié — tests non exécutés dans cette réorganisation documentaire.** Aucun résultat ne doit être interprété comme une implémentation.

## Préconditions d’exécution

- projet de tests .NET 10 et framework/adaptateur sélectionnés ;
- chemins temporaires injectables pour SQLite, staging et pièces jointes ;
- horloge et identifiants d’opération contrôlables ;
- possibilité de simuler une erreur ou une interruption aux frontières de restauration ;
- jeu de données métier comprenant plusieurs Workspaces, records, valeurs et pièces jointes.

Si une précondition manque, le test est **Bloqué**, pas Passant.

## Convention de résultat

Chaque scénario doit être reporté avec : identifiant, date, commande exacte, environnement, résultat observé, preuve, statut et anomalie associée.

- **Passant** : comportement attendu observé et preuve conservée.
- **Échouant** : comportement attendu non observé, avec reproduction et erreur.
- **Bloqué** : impossible à exécuter faute de prérequis ; la cause est documentée.

## Tests d’initialisation et de migrations

| ID | Scénario | Attendu |
|---|---|---|
| DB-001 | Appeler l’initialisation deux fois séquentiellement | Une seule préparation effective, schéma cohérent et appel idempotent |
| DB-002 | Appeler l’initialisation simultanément depuis plusieurs tâches | Une seule initialisation protégée, aucune table partielle ni exception parasite |
| DB-003 | Ouvrir une base existante au dernier niveau de schéma | Aucune migration inutile, données conservées |
| DB-004 | Appliquer une migration intermédiaire sur une base de version antérieure | Chaque version est appliquée dans l’ordre et journalisée |
| DB-005 | Faire échouer une migration | Erreur explicite, état cohérent et reprise documentée, sans exception ignorée |
| DB-006 | Relancer après une migration interrompue | Reprise ou rollback sûr, sans doublons ni perte de données |

## Tests de connexions concurrentes et transactions

| ID | Scénario | Attendu |
|---|---|---|
| TX-001 | Exécuter plusieurs écritures simultanées sur la même base | Politique de sérialisation respectée, pas de corruption ni perte silencieuse |
| TX-002 | Écrire un parent et plusieurs enfants dans une transaction | Commit atomique de toutes les lignes |
| TX-003 | Provoquer une erreur après une première écriture | Rollback complet des écritures de la transaction |
| TX-004 | Lire pendant une écriture | Comportement documenté, aucune connexion utilisée après fermeture |
| TX-005 | Lancer deux opérations incompatibles sur le même Workspace | Une opération est ordonnée ou refusée explicitement, données cohérentes |
| TX-006 | Vérifier les connexions après succès et après erreur | Toutes les ressources sont libérées et réutilisables |

## Tests de conservation des données métier

| ID | Scénario | Attendu |
|---|---|---|
| DATA-001 | Restaurer une sauvegarde dans une base contenant d’autres données | Les données hors périmètre sont conservées |
| DATA-002 | Restaurer des records et valeurs existants | Les valeurs sont conservées ou remplacées uniquement selon une règle explicite |
| DATA-003 | Échec de validation avant commit | Aucune suppression métier ni modification partielle |
| DATA-004 | Erreur au milieu d’une opération multi-écritures | Rollback ou reprise sans perte |
| DATA-005 | Vérifier les données orphelines après migration | Les orphelins sont signalés et traités selon une décision, jamais supprimés implicitement |

## Tests de restauration réversible des pièces jointes

| ID | Scénario | Attendu |
|---|---|---|
| FILE-001 | Restaurer une archive valide dans un staging vide | Tous les fichiers sont préparés, empreintes et manifeste concordants |
| FILE-002 | Archive contenant une pièce jointe invalide ou absente | Restauration non finalisée, ancienne donnée conservée, erreur explicite |
| FILE-003 | Crash après staging mais avant commit SQLite | Reprise ou annulation, staging nettoyable, aucune suppression de l’actif |
| FILE-004 | Crash après commit SQLite mais avant finalisation des fichiers | Journal détecté, finalisation reprenable, anciennes pièces conservées |
| FILE-005 | Crash pendant la finalisation d’un lot | Reprise idempotente fichier par fichier, aucune pièce jointe perdue |
| FILE-006 | Restauration réussie | Nouveaux fichiers accessibles, anciennes copies conservées jusqu’à validation puis nettoyage contrôlé |
| FILE-007 | Deux restaurations concurrentes | Une politique explicite empêche l’entrelacement ou isole les opérations |
| FILE-008 | Rejouer une opération déjà terminée | Aucun doublon ni dommage, opération reconnue comme terminée |

## Tests de sauvegarde

| ID | Scénario | Attendu |
|---|---|---|
| BAK-001 | Exporter les données et pièces jointes | Manifeste complet et archive lisible |
| BAK-002 | Vérifier une archive sans la restaurer | Validation sans modification de la base active |
| BAK-003 | Interrompre un export | Fichier temporaire non présenté comme sauvegarde valide |
| BAK-004 | Restaurer puis réexporter | Les identifiants, valeurs et pièces jointes attendus sont conservés |

## Matrice de reporting

À compléter uniquement après exécution :

| Groupe | Passants | Échouants | Bloqués | Preuve / lien |
|---|---:|---:|---:|---|
| Initialisation / migrations | — | — | — | — |
| Concurrence / transactions | — | — | — | — |
| Conservation métier | — | — | — | — |
| Pièces jointes réversibles | — | — | — | — |
| Sauvegarde | — | — | — | — |

## Références

- [Plan de Mission 1](PLAN.md)
- [Rapport d’implémentation](IMPLEMENTATION_REPORT.md)
- [Conception SQLite](../../01-architecture/DATABASE_DESIGN.md)
- [État d’implémentation](../../02-roadmap/IMPLEMENTATION_STATUS.md)
