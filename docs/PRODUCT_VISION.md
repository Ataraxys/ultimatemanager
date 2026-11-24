# Vision produit — UltimateManager

## Statut du document

Ce document décrit la vision cible. Les fonctionnalités mentionnées comme cibles ne sont pas nécessairement implémentées.

## Vision

UltimateManager est une application .NET MAUI .NET 10, local-first, destinée à la gestion de données métier personnalisables.

Elle doit pouvoir être utilisée :

- par une personne seule dans un espace personnel ;
- par plusieurs utilisateurs dans une organisation ;
- avec plusieurs Workspaces indépendants ;
- avec des groupes réunissant utilisateurs et contacts externes.

## Capacités cibles

- Contacts et clients.
- Tâches, projets et agenda.
- Documents.
- Objets métier standards personnalisables.
- Création de types d’objets personnalisés.
- Templates et vues configurables.
- Workflows métier.
- Fonctionnement offline.
- Synchronisation cloud future.
- Commercialisation par abonnement.

## Principes produit

- Les données sont isolées par Workspace.
- La modification des données d’une fiche ne modifie pas sa structure.
- La structure est éditée séparément via les templates.
- Les templates créés depuis une fiche ne sont ajoutés à la liste que sur demande explicite.
- L’utilisateur peut choisir un template lors de la création et visualiser un aperçu.
- La cartographie doit commencer avec OpenStreetMap derrière une abstraction remplaçable.

## État actuel vérifié

Le code actuel contient un modèle `Client`, un Workspace local, un moteur de templates/blocs/champs et des sauvegardes locales. Les utilisateurs, groupes, permissions, projets, tâches, événements, documents métier, workflows et synchronisation cloud ne sont pas encore implémentés comme sous-systèmes cibles.
