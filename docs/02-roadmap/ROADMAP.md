# Roadmap — UltimateManager

## Statut

Roadmap cible fondée sur l’audit architectural. Les éléments ci-dessous ne sont pas tous implémentés.

## Priorités

### 1. Sécurisation SQLite, migrations et sauvegardes

Mission associée : [Mission 1 — Sécurisation SQLite](../03-missions/01-sqlite-security/PLAN.md).

- centraliser la connexion ;
- garantir une initialisation unique ;
- sérialiser les écritures et définir la propriété des transactions ;
- versionner les migrations ;
- définir une politique de conservation avant toute suppression ;
- restaurer SQLite et les pièces jointes par une stratégie temporaire et réversible ;
- distinguer les tests passants, échouants et bloqués ;
- rendre l’infrastructure de tests exécutable sans dépendre de l’interface MAUI.

### 2. Isolation des données par Workspace

Mission associée : [Mission 2 — Isolation Workspace](../03-missions/02-workspace-isolation/README.md).

- ajouter `WorkspaceId` aux contacts ;
- filtrer toutes les lectures et écritures ;
- tester l’isolation ;
- adapter imports et exports.

### 3. Stabilisation de l’édition des contacts et templates

Mission associée : [Mission 3 — Formulaires dynamiques](../03-missions/03-dynamic-forms/README.md).

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
