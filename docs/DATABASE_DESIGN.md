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
- Valeurs en `ValueJson`, configuration en `ConfigJson`.
- Pas de colonne par champ personnalisé ni de fiche complète dans un JSON unique.

## Migration sans perte

1. Sauvegarder et copier la base.
2. Inventorier et vérifier les données orphelines.
3. Ajouter un gestionnaire de versions de schéma.
4. Ajouter le Workspace aux contacts existants.
5. Migrer progressivement `Clients` vers `Contacts`.
6. Conserver les anciens records lors des changements de template.
7. Introduire les nouvelles tables avec une couche de compatibilité.
8. Unifier les sauvegardes après validation des migrations.
