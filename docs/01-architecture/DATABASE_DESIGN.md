# Conception SQLite — UltimateManager

## Statut

La conception actuelle est inventoriée à partir du code. Le schéma cible est une proposition de migration et n’est pas encore implémenté.

## Schéma actuel vérifié

Tables créées par `sqlite-net-pcl` :

- `Clients`
- `Workspaces`
- `EntityTemplates`
- `BlockDefinitions`
- `FieldDefinitions`
- `EntityRecords`
- `BlockInstances`
- `FieldValues`

Les relations sont exprimées par des Guid, sans clés étrangères déclarées.

Les colonnes dynamiques utilisent :

- `FieldDefinition.OptionsJson` pour certaines configurations ;
- `FieldValue.ValueJson` pour les valeurs.

## Risques actuels

- Initialisation SQLite répartie entre plusieurs services.
- Migrations limitées à des `ALTER TABLE` ad hoc et exceptions ignorées.
- Pas de version de schéma centralisée.
- Pas de transaction pour les opérations multi-écritures.
- Pas de contrainte d’unicité sur une valeur par record et champ.
- `Client` n’est pas isolé par Workspace.
- Restauration complète destructive et non transactionnelle.
- Deux formats de sauvegarde concurrents : clients JSON et archive ZIP complète.
- Une transaction SQLite ne suffit pas à rendre atomiques les modifications SQLite et les fichiers physiques.
- Les suppressions métier ne doivent pas être autorisées uniquement parce qu’elles sont transactionnelles.

## Schéma cible recommandé

Tables de collaboration :

- `Users`
- `Workspaces`
- `WorkspaceMembers`
- `Groups`
- `GroupUserMembers`
- `GroupContactMembers`
- `Roles`
- `Permissions`
- tables d’association de rôles et permissions.

Tables métier :

- `Contacts`
- `ContactAddresses`
- `ContactPhoneNumbers`
- tables spécialisées pour tâches, projets, événements et documents si nécessaire.

Tables dynamiques :

- `ObjectDefinitions`
- `FieldDefinitions`
- `ObjectRecords`
- `ObjectFieldValues`
- `ObjectRelations`
- `ObjectTemplates`
- `ObjectTemplateFields`
- `ObjectViews`

## Règles de persistance cible

- Identifiants Guid.
- `WorkspaceId` sur les entités Workspace-scoped.
- `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `Version` et `DeletedAt` pour les entités synchronisables.
- Unicité de `(RecordId, FieldDefinitionId)`.
- Migrations numérotées et journalisées.
- Transactions pour les opérations atomiques multi-tables.
- Une politique de conservation doit être définie avant toute suppression métier.
- Valeurs en `ValueJson`, configuration en `ConfigJson`.
- Pas de colonne par champ personnalisé ni de fiche complète dans un JSON unique.
- Les restaurations avec pièces jointes utilisent un staging temporaire, un journal de reprise et un nettoyage différé.

## Migration sans perte

1. Sauvegarder et copier la base.
2. Inventorier et vérifier les données orphelines.
3. Ajouter un gestionnaire de versions de schéma.
4. Centraliser la propriété des connexions et sérialiser les écritures.
5. Définir les règles de conservation avant les transactions destructives.
6. Mettre en place une restauration en deux phases pour SQLite et fichiers physiques.
7. Ajouter le Workspace aux contacts dans la mission dédiée.
8. Migrer progressivement `Clients` vers `Contacts` dans une mission ultérieure.
9. Conserver les anciens records lors des changements de template.
10. Introduire les nouvelles tables avec une couche de compatibilité.
11. Unifier les sauvegardes après validation des migrations.

## Références

- [Architecture](ARCHITECTURE.md)
- [Mission 1 — Plan](../03-missions/01-sqlite-security/PLAN.md)
- [Mission 1 — Tests](../03-missions/01-sqlite-security/TEST_PLAN.md)
