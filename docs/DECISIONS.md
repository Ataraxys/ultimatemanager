# Décisions et questions ouvertes — UltimateManager

## Décisions déjà établies

- La cible est .NET MAUI .NET 10.
- L’application doit rester local-first et fonctionner offline.
- Les identifiants métier sont des Guid.
- Les données doivent être isolées par Workspace.
- Les valeurs de champs personnalisés sont stockées séparément de la configuration de structure.
- Les templates et les fiches doivent être indépendants.
- L’édition des données et l’édition de la structure sont deux responsabilités distinctes.
- OpenStreetMap est le fournisseur cartographique de départ préféré, derrière une abstraction remplaçable.
- Les objets métier standards ne doivent pas tous être transformés en records dynamiques.

## État observé à ne pas confondre avec une décision cible

- Le code actuel utilise `Client` comme modèle SQLite et métier.
- Le code actuel utilise des templates, blocs, champs et records dynamiques.
- Le code actuel utilise Google comme fournisseur cartographique.
- Le code actuel ne possède pas encore les utilisateurs, groupes, permissions ou synchronisation cloud cibles.

## Décisions en attente

### Identité

Quel fournisseur d’identité sera utilisé pour les comptes et la collaboration ?

### Synchronisation

Quel sera le serveur de référence ? Comment seront résolus les conflits offline ?

### Templates

Un template est-il personnel, partagé par Workspace ou partagé par organisation ?

### Versionnement

Une modification de structure crée-t-elle toujours une nouvelle version ? Comment migrer les anciennes valeurs ?

### Permissions

Quels droits sont nécessaires pour lire, modifier les données, modifier la structure, gérer les membres et restaurer une sauvegarde ?

### Objets standards

Quels objets doivent rester spécialisés et quels types doivent être configurables par l’utilisateur ?

### Commercialisation

Quel périmètre est inclus dans chaque niveau d’abonnement, et quelles fonctions nécessitent un service cloud ?
