# Modèle de domaine — UltimateManager

## Statut

La première partie décrit les modèles effectivement observés. La seconde décrit la cible recommandée.

## Existant vérifié

Modèle métier fixe :

- `Client` dans `Models/Client.cs`.

Modèles dynamiques persistés :

- `Workspace`
- `EntityTemplate`
- `BlockDefinition`
- `FieldDefinition`
- `EntityRecord`
- `BlockInstance`
- `FieldValue`

Types de champs observés :

- texte court et long ;
- nombre ;
- date ;
- téléphone ;
- adresse ;
- coordonnées GPS ;
- email ;
- URL ;
- case à cocher ;
- liste déroulante ;
- commentaire ;
- document.

Le code contient des value objects pour adresse, téléphone, coordonnées, commentaire et document.

## Limites actuelles

- Aucun utilisateur, membre, groupe, rôle ou permission.
- Aucun objet relationnel générique.
- Aucune relation métier entre contacts.
- Aucun versionnement de définition.
- `EntityType` est une chaîne libre.
- `FieldDefinition.IsRequired` n’est pas appliqué par une validation métier centralisée.

## Modèle cible

### Collaboration

- `User`
- `Workspace`
- `WorkspaceMember`
- `Group`
- `GroupUserMember`
- `GroupContactMember`
- `Role`
- `Permission`

### Moteur dynamique

- `ObjectDefinition` : type d’objet configurable.
- `FieldDefinition` : champ d’une définition.
- `ObjectRecord` : instance d’un objet dynamique.
- `ObjectRelation` : relation entre instances.
- `ObjectTemplate` : structure initiale ou versionnée.
- `ObjectView` : configuration d’affichage.

### Objets standards

- `Contact`
- `Task`
- `Project`
- `Event`
- `Document`

## Principes de propriété

Toute entité Workspace-scoped doit porter son Workspace ou être atteignable par une relation contrôlée vers celui-ci.

Les champs personnalisés sont stockés dans des lignes séparées. Les valeurs personnelles sont séparées de la configuration de structure.

Les modifications de structure ne doivent pas détruire automatiquement les valeurs existantes.
