# État d’implémentation — UltimateManager

## Légende

- **Présent** : vérifié dans le code actuel.
- **Partiel** : mécanisme présent mais incomplet ou non conforme à la cible.
- **Planifié** : décrit dans la documentation, mais pas implémenté.
- **Bloqué** : nécessite une décision ou un prérequis non disponible.

## Présentation

| Fonction | État |
|---|---|
| Application .NET MAUI .NET 10 | Présent |
| MVVM avec CommunityToolkit.Mvvm | Présent |
| Navigation Shell | Présent |
| Injection DI dans MauiProgram | Présent |
| Séparation en projets Domain/Application/Infrastructure | Planifié |
| Projet de tests automatisés dédié | Bloqué |

## Contacts

| Fonction | État |
|---|---|
| Modèle Client fixe | Présent |
| Création, modification et suppression | Présent |
| Import depuis les contacts de l’appareil | Présent |
| Champs personnalisés | Partiel |
| Relations entre contacts | Planifié |
| Isolation par Workspace | Planifié |
| Validation métier centralisée | Planifié |

## Templates et objets dynamiques

| Fonction | État |
|---|---|
| Templates | Présent |
| Blocs et champs | Présent |
| Valeurs en JSON par champ | Présent |
| Configuration distincte en ConfigJson | Planifié; le code utilise principalement OptionsJson |
| Versionnement des structures | Planifié |
| Relations entre objets | Planifié |
| Vues configurables | Planifié |
| Création de types d’objets utilisateur | Partiel; EntityType est une chaîne libre |
| Conservation des valeurs lors d’un changement de structure | Bloqué; règle métier non définie |

## Persistance

| Fonction | État |
|---|---|
| SQLite local | Présent |
| Identifiants Guid | Présent |
| Initialisation SQLite unique | Planifié |
| Propriété centralisée des connexions | Planifié |
| Sérialisation des écritures | Planifié |
| Migrations versionnées | Planifié |
| Transactions multi-écritures | Planifié |
| Clés étrangères explicites | Planifié |
| Suppression logique | Planifié |
| Politique de conservation avant suppression | Planifié |
| Métadonnées de synchronisation | Planifié |
| Sauvegarde JSON legacy | Présent |
| Sauvegarde ZIP complète | Présent |
| Restauration réversible avec staging des pièces jointes | Planifié |
| Restauration isolée par Workspace | Bloqué pour les contacts tant que Client ne possède pas WorkspaceId |

## Tests et validation

| Fonction | État |
|---|---|
| Tests unitaires exécutables | Bloqué; framework et projet de test absents |
| Tests d’intégration SQLite | Bloqué; chemin de base non injectable |
| Tests de restauration | Bloqué; services couplés à FileSystem et APIs MAUI |
| Tests d’isolation dynamique | Planifié |
| Tests d’isolation des contacts | Bloqué jusqu’à WorkspaceId sur Client |
| Classification passant/échouant/bloqué | Planifié |

## Collaboration

| Fonction | État |
|---|---|
| Utilisateurs | Planifié |
| Membres de Workspace | Planifié |
| Groupes | Planifié |
| Rôles et permissions | Planifié |
| Synchronisation cloud | Planifié |

## Prochaines étapes prioritaires

1. Mission 1 : sécuriser SQLite, les migrations, les transactions, les sauvegardes et les restaurations.
2. Mission 2 : isoler les données par Workspace, notamment les contacts.
3. Mission 3 : stabiliser l’édition des contacts et templates.
4. Séparer progressivement les couches.
5. Faire évoluer le moteur dynamique vers un moteur d’objets versionné.

Aucune fonctionnalité marquée Planifié ou Bloqué ne doit être présentée comme disponible.
