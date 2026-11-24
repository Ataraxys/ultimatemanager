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
- Aucune suppression automatique de donnée métier ne doit être introduite sans règle de conservation explicite.
- La restauration SQLite et la restauration des fichiers physiques doivent être coordonnées par une stratégie temporaire, réversible et journalisée ; elles ne peuvent pas être rendues atomiques par une transaction SQLite seule.

## État observé à ne pas confondre avec une décision cible

- Le code actuel utilise `Client` comme modèle SQLite et métier.
- Le code actuel utilise des templates, blocs, champs et records dynamiques.
- Le code actuel utilise Google comme fournisseur cartographique.
- Le code actuel ne possède pas encore les utilisateurs, groupes, permissions ou synchronisation cloud cibles.
- Les services SQLite créent plusieurs connexions vers le même fichier.
- Aucun projet de test ni framework de test n’a été identifié.

## Décisions en attente

### Tests

Quel framework de test et quel adaptateur seront utilisés avec .NET 10 ?

Comment injecter un chemin SQLite temporaire, un stockage de fichiers temporaire et des erreurs simulées sans dépendre de MAUI ?

### Connexions SQLite

La base utilisera-t-elle une connexion singleton sérialisée ou des connexions par opération protégées par un verrou partagé ?

Quel composant possédera la connexion, les migrations et les transactions ?

### Restauration et fichiers

La restauration utilisera-t-elle une base temporaire complète ou une transaction SQLite avec staging de fichiers ?

Comment reprendre une opération après un crash entre le commit SQLite et la finalisation des pièces jointes ?

### Conservation

Les champs et blocs utilisés seront-ils archivés, versionnés ou migrés avant toute suppression ?

Les records existants doivent-ils toujours être conservés lors d’un changement de template ?

### Identité

Quel fournisseur d’identité sera utilisé pour les comptes et la collaboration ?

### Synchronisation

Quel sera le serveur de référence ? Comment seront résolus les conflits offline ?

### Templates

Un template est-il personnel, partagé par Workspace ou partagé par organisation ?

### Permissions

Quels droits sont nécessaires pour lire, modifier les données, modifier la structure, gérer les membres et restaurer une sauvegarde ?

### Objets standards

Quels objets doivent rester spécialisés et quels types doivent être configurables par l’utilisateur ?

### Commercialisation

Quel périmètre est inclus dans chaque niveau d’abonnement, et quelles fonctions nécessitent un service cloud ?
