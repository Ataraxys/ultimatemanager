---
applyTo: "**/Domain/**/*.cs,**/Models/**/*.cs,**/Application/**/*.cs"
---

# Règles Domain et Application

- Séparer les invariants métier des détails SQLite, MAUI et réseau.
- Utiliser des `Guid` et des dates UTC pour les entités persistées.
- Garder les objets métier standards spécialisés; réserver les records dynamiques aux types réellement configurables.
- Isoler toutes les données par `WorkspaceId`.
- Exprimer les cas d’utilisation via des interfaces applicatives et des résultats testables.
- Ne pas introduire de dépendance directe vers une View, SQLite ou un fournisseur cartographique.
- Préserver la compatibilité des données existantes lors d’une évolution de structure.
