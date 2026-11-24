# Architecture — UltimateManager

## Statut

Ce document distingue l’architecture actuellement observée de l’architecture recommandée. Il ne décrit pas une migration déjà réalisée.

## Existant vérifié

La solution contient un seul projet MAUI : `UltimateManager.csproj`.

Dossiers principaux :

- `Models/` : modèles annotés SQLite et modèles de formulaires dynamiques.
- `Services/` : SQLite, sauvegardes, import, cartographie et sérialisation.
- `ViewModels/` : état et orchestration de l’interface.
- `Views/` : pages MAUI, navigation et code-behind.
- `Platforms/` : code spécifique aux plateformes.

La composition DI est centralisée dans `MauiProgram.cs`, mais plusieurs pages résolvent directement des services via `App.Services`.

Les ViewModels ne créent pas directement de connexion SQLite. Les services SQLite construisent toutefois plusieurs connexions vers la même base.

## Problèmes structurants

- Pas de projets Domain, Application, Infrastructure et Presentation séparés.
- `Client` est à la fois modèle métier, modèle SQLite et modèle utilisé par l’interface.
- `Client` ne contient pas de `WorkspaceId`.
- Le moteur dynamique mélange initialisation SQLite, templates, records, blocs, champs et valeurs dans `SqliteDynamicFormService`.
- Les migrations ne sont pas versionnées.
- Les opérations multi-écritures ne sont pas transactionnelles.
- La restauration complète effectue des suppressions globales.
- L’édition de structure apparaît encore dans `ClientEditPage` alors que l’édition de contenu doit en être séparée.

## Architecture cible recommandée

Projets progressifs :

- `UltimateManager.Domain` : agrégats, règles et contrats métier.
- `UltimateManager.Application` : cas d’utilisation, ports et résultats.
- `UltimateManager.Infrastructure` : SQLite, repositories, migrations, sauvegardes et fournisseurs externes.
- `UltimateManager.Presentation` : Views, ViewModels et navigation.
- `UltimateManager.App` : composition MAUI et plateformes.

La séparation peut commencer à l’intérieur du projet actuel avant extraction en projets distincts.

## Règles de dépendance cible

- Domain ne dépend ni de MAUI ni de SQLite.
- Application dépend du Domain et de ports abstraits.
- Infrastructure implémente les ports.
- Presentation dépend de l’Application.
- La composition DI reste dans l’application MAUI.
- Les modules métier ne dépendent pas directement les uns des autres.

## Objets métier

`Contact`, `Task`, `Project`, `Event` et `Document` restent des agrégats spécialisés. `ObjectRecord` est réservé aux types réellement dynamiques ou créés par l’utilisateur.

## Références

- [Modèle de domaine](DOMAIN_MODEL.md)
- [Conception SQLite](DATABASE_DESIGN.md)
- [Décisions](DECISIONS.md)
- [Mission 1 — Sécurisation SQLite](../03-missions/01-sqlite-security/PLAN.md)
