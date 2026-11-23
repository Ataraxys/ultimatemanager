---
applyTo: "**/Infrastructure/Persistence/**/*,**/Services/Sqlite*.cs,**/Services/*Backup*.cs,**/Services/*Workspace*.cs"
---

# Règles de persistance

- Centraliser la connexion SQLite et les migrations versionnées.
- Exécuter les opérations multi-écritures dans une transaction.
- Utiliser des clés `Guid`, des clés étrangères ou des contrôles d’intégrité explicites et des index adaptés.
- Filtrer chaque lecture et écriture par Workspace lorsque l’entité est Workspace-scoped.
- Stocker les valeurs personnalisées en `ValueJson` et leur configuration en `ConfigJson`; ne pas stocker une fiche entière dans un JSON unique.
- Ne jamais supprimer des données existantes lors d’un changement de template sans migration explicite et sauvegarde vérifiable.
- Rendre les sauvegardes versionnées, restaurables et non destructives pour les autres Workspaces.
