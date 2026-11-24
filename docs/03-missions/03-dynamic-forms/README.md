# Mission 3 — Formulaires et objets dynamiques

## Statut

**Planifiée — non démarrée.** Cette mission n’est pas implémentée par la réorganisation documentaire.

## Objectif

Séparer durablement la structure des formulaires et le contenu des fiches, tout en conservant les valeurs métier existantes et en préparant le découpage progressif du moteur dynamique.

## Périmètre prévu

- distinguer l’édition des données de l’édition de structure ;
- préserver l’ordre, les blocs, les champs, les dimensions et le style lors de l’édition du contenu ;
- rendre tous les blocs éditables avec un rendu partagé View/Edit ;
- isoler les fiches de leur template d’origine ;
- séparer `ValueJson` et `ConfigJson` ;
- conserver les valeurs lors d’une évolution de structure ;
- découper progressivement `SqliteDynamicFormService` par responsabilités ;
- tester Enregistrer/Annuler, les templates et la conservation des données.

## Prérequis

- isolation des données par Workspace ;
- modèle de persistance et transactions sécurisés ;
- décision de versionnement des structures et des migrations de valeurs.

## Critères de sortie

L’édition des données ne modifie pas la structure, les templates créés depuis une fiche ne sont ajoutés que sur demande explicite et aucune valeur existante n’est supprimée implicitement. Les résultats doivent distinguer Passant, Échouant et Bloqué.

## Références

- [Architecture](../../01-architecture/ARCHITECTURE.md)
- [Modèle de domaine](../../01-architecture/DOMAIN_MODEL.md)
- [Conception SQLite](../../01-architecture/DATABASE_DESIGN.md)
- [Roadmap](../../02-roadmap/ROADMAP.md)
