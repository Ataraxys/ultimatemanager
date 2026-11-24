---
applyTo: "**/*.xaml,**/Views/**/*.cs,**/ViewModels/**/*.cs,**/MauiProgram.cs"
---

# Règles d’interface .NET MAUI

- Respecter MVVM et injecter les dépendances par constructeur.
- Garder le code-behind limité à la présentation et à la navigation.
- Ne pas accéder directement à SQLite depuis les Views ou ViewModels.
- Séparer l’édition des données d’une fiche de l’édition de sa structure.
- Préserver un rendu cohérent entre affichage et édition, avec persistance temporaire puis Enregistrer/Annuler lorsque le parcours le demande.
- Isoler le fournisseur cartographique derrière `IMapProvider`; privilégier OpenStreetMap comme fournisseur initial.
- Prévoir des états de chargement, d’erreur et d’annulation sans bloquer inutilement l’interface.
