# Mission 2 — Isolation des données par Workspace

## Statut

**Planifiée — non démarrée.** Cette mission est distincte de la sécurisation SQLite et n’est pas implémentée par la réorganisation documentaire.

## Objectif

Garantir qu’un utilisateur ne puisse lire, modifier, exporter ou supprimer les contacts et données d’un autre Workspace.

## Périmètre prévu

- ajouter et renseigner `WorkspaceId` sur les contacts ;
- filtrer toutes les lectures, recherches, écritures, suppressions, imports et exports ;
- vérifier les contrôles côté service et non seulement dans l’interface ;
- tester les scénarios positifs, négatifs et les données héritées ;
- définir le comportement de migration des contacts existants.

## Prérequis

- garanties de persistance de la [Mission 1](../01-sqlite-security/PLAN.md) ;
- décision de rattachement des contacts existants ;
- stratégie de tests SQLite isolés.

## Critères de sortie

Aucune lecture ou écriture de contact ne peut contourner le Workspace actif, et les exports/restaurations respectent le périmètre autorisé. Les résultats doivent distinguer Passant, Échouant et Bloqué.

## Références

- [Modèle de domaine](../../01-architecture/DOMAIN_MODEL.md)
- [Conception SQLite](../../01-architecture/DATABASE_DESIGN.md)
- [Roadmap](../../02-roadmap/ROADMAP.md)
