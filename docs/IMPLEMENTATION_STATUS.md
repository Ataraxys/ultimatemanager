# État d’implémentation — UltimateManager

## Légende

- **Présent** : vérifié dans le code actuel.
- **Partiel** : mécanisme présent mais incomplet ou non conforme à la cible.
- **À faire** : non identifié comme implémenté dans l’audit.

## Présentation

| Fonction | État |
|---|---|
| Application .NET MAUI .NET 10 | Présent |
| MVVM avec CommunityToolkit.Mvvm | Présent |
| Navigation Shell | Présent |
| Injection DI dans MauiProgram | Présent |
| Séparation en projets Domain/Application/Infrastructure | À faire |

## Contacts

| Fonction | État |
|---|---|
| Modèle Client fixe | Présent |
| Création, modification et suppression | Présent |
| Import depuis les contacts de l’appareil | Présent |
| Champs personnalisés | Partiel |
| Relations entre contacts | À faire |
| Isolation par Workspace | À faire |
| Validation métier centralisée | À faire |

## Templates et objets dynamiques

| Fonction | État |
|---|---|
| Templates | Présent |
| Blocs et champs | Présent |
| Valeurs en JSON par champ | Présent |
| Configuration distincte en ConfigJson | À faire; le code utilise principalement OptionsJson |
| Versionnement des structures | À faire |
| Relations entre objets | À faire |
| Vues configurables | À faire |
| Création de types d’objets utilisateur | Partiel; EntityType est une chaîne libre |

## Persistance

| Fonction | État |
|---|---|
| SQLite local | Présent |
| Identifiants Guid | Présent |
| Migrations versionnées | À faire |
| Transactions multi-écritures | À faire |
| Clés étrangères explicites | À faire |
| Suppression logique | À faire |
| Métadonnées de synchronisation | À faire |
| Sauvegarde JSON legacy | Présent |
| Sauvegarde ZIP complète | Présent |
| Restauration isolée par Workspace | À faire |

## Collaboration

| Fonction | État |
|---|---|
| Utilisateurs | À faire |
| Membres de Workspace | À faire |
| Groupes | À faire |
| Rôles et permissions | À faire |
| Synchronisation cloud | À faire |

## Prochaines étapes prioritaires

1. Sécuriser SQLite, les migrations et les sauvegardes.
2. Isoler les données par Workspace.
3. Stabiliser l’édition des contacts et templates.
4. Séparer progressivement les couches.
5. Faire évoluer le moteur dynamique vers un moteur d’objets versionné.
