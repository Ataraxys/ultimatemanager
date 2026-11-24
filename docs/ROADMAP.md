# Roadmap — UltimateManager

## Statut

Roadmap cible fondée sur l’audit architectural. Les éléments ci-dessous ne sont pas tous implémentés.

## Priorités

### 1. Sécurisation SQLite, migrations et sauvegardes

- centraliser la connexion ;
- versionner les migrations ;
- ajouter les transactions ;
- rendre la restauration non destructive pour les autres Workspaces ;
- unifier progressivement les sauvegardes.

### 2. Isolation des données par Workspace

- ajouter `WorkspaceId` aux contacts ;
- filtrer toutes les lectures et écritures ;
- tester l’isolation ;
- adapter imports et exports.

### 3. Stabilisation de l’édition des contacts et templates

- séparer contenu et structure ;
- conserver les valeurs lors d’un changement de structure ;
- garantir Enregistrer/Annuler ;
- réserver la gestion des templates au parcours approprié.

### 4. Séparation progressive des couches

- isoler Domain, Application, Infrastructure et Presentation ;
- réduire le code métier dans les pages ;
- introduire repositories et cas d’utilisation.

### 5. Évolution du moteur d’objets dynamiques

- introduire ObjectDefinition, ObjectRecord et ObjectTemplate ;
- versionner les structures ;
- séparer configuration et valeurs.

### 6. Relations entre objets

- ajouter ObjectRelation ;
- définir les types de relation ;
- tester les règles Workspace.

### 7. Collaboration et groupes

- utilisateurs ;
- membres de Workspace ;
- groupes ;
- rôles et permissions.

### 8. Tâches, projets et agenda

- agrégats spécialisés ;
- relations avec contacts et groupes ;
- vues et workflows métier.

### 9. Synchronisation cloud

- journal de changements ;
- versions et suppressions logiques ;
- résolution des conflits ;
- serveur et abonnement après stabilisation offline.

## Règle de progression

Aucune étape de collaboration ou de synchronisation ne doit contourner les garanties de conservation, d’isolation et de migration des données.
