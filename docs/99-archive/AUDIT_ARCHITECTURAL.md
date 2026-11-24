RAPPORT D’AUDIT ARCHITECTURAL — ULTIMATEMANAGER

Périmètre vérifié

Audit réalisé en lecture seule sur le projet UltimateManager.csproj.
Aucune modification, suppression ou création de code n’a été effectuée.


A. ÉTAT ACTUEL

A.1. Structure de la solution

La solution contient actuellement un seul projet : UltimateManager.csproj.

Le projet est une application .NET MAUI ciblant .NET 10, avec les dépendances principales suivantes :

- Microsoft.Maui.Controls
- Microsoft.Maui.Controls.Maps
- CommunityToolkit.Mvvm
- sqlite-net-pcl
- Microsoft.Extensions.Logging.Debug

Les responsabilités sont regroupées dans le même projet :

- Models/
- Services/
- ViewModels/
- Views/
- Platforms/
- Converters/

Il n’existe pas actuellement de projets séparés pour Domain, Application, Infrastructure et Presentation.

A.2. Responsabilités actuelles

Models : modèles SQLite et objets de transfert pour les formulaires dynamiques.
Services : accès SQLite, logique métier, sauvegardes, import, cartographie et sérialisation.
ViewModels : état de l’interface, orchestration de certains cas d’utilisation et édition.
Views : pages MAUI, navigation et logique code-behind.
Platforms : initialisation spécifique Android, iOS, MacCatalyst et Windows.
MauiProgram.cs : composition et injection des dépendances.

La séparation est donc essentiellement conventionnelle par dossier, et non contractuelle par couche.


B. AUDIT DES MODÈLES MÉTIER

B.1. Client

Fichier : Models/Client.cs

Le modèle Client représente actuellement le contact métier principal.

Il contient notamment :

- FirstName
- LastName
- PhoneNumber
- Email
- Address
- PostalCode
- City
- Country
- Latitude
- Longitude
- Comment
- CreatedAt
- UpdatedAt

Le modèle est directement annoté pour SQLite avec [Table("Clients")].

Constats :

- Le modèle est à la fois modèle métier, modèle de persistance SQLite et structure utilisée directement par les ViewModels.
- Il ne possède aucun WorkspaceId.
- Il ne contient aucun identifiant de synchronisation : CreatedBy, UpdatedBy, Version, DeletedAt ou SyncState.
- Les données personnelles fixes et les données dynamiques sont stockées dans deux systèmes différents.

Risques :

- Les contacts ne sont pas réellement isolés par Workspace.
- Une synchronisation future devra gérer deux agrégats indépendants.
- Le modèle est difficile à faire évoluer sans modifier directement le schéma SQLite.
- La distinction entre Contact, Client et futur objet métier n’est pas encore définie.

B.2. Modèles dynamiques existants

Fichier : Models/DynamicForms/DynamicFormModels.cs

Les structures existantes sont :

- Workspace
- EntityTemplate
- BlockDefinition
- FieldDefinition
- EntityRecord
- BlockInstance
- FieldValue

Correspondance actuelle :

- Workspace : espace local courant.
- EntityTemplate : template d’une fiche.
- BlockDefinition : bloc structurel d’un template.
- FieldDefinition : champ d’un bloc.
- EntityRecord : fiche dynamique.
- BlockInstance : copie d’un bloc dans une fiche.
- FieldValue : valeur d’un champ.

Le moteur est déjà partiellement relationnel, ce qui est un point positif.

Toutefois, il s’agit davantage d’un moteur de formulaires clonés que d’un moteur d’objets génériques.

B.3. Écart avec la cible

La cible prévoit :

- ObjectDefinition
- FieldDefinition
- ObjectRecord
- ObjectRelation
- ObjectTemplate
- ObjectView

Le code actuel ne possède pas :

- de définition générique d’objet ;
- de relation entre objets ;
- de vue configurable ;
- de version de définition ;
- de distinction claire entre type d’objet et template ;
- de référence explicite à un objet métier standard.

La propriété EntityTemplate.EntityType est actuellement une chaîne libre, par défaut « Client ». Cela ne fournit pas un système fortement typé pour les types d’objets.


C. AUDIT DE LA GESTION DES CONTACTS

C.1. Création et modification

Services concernés :

- Services/IClientService.cs
- Services/SqliteClientService.cs

La création, mise à jour et suppression sont effectuées directement dans SqliteClientService.

Le service :

- construit sa propre SQLiteAsyncConnection ;
- crée directement les tables ;
- écrit directement le modèle Client ;
- régénère une sauvegarde JSON après chaque modification.

Problèmes :

1. Il n’existe pas de service applicatif distinct pour les cas d’utilisation : créer, modifier, importer ou supprimer un contact, ou changer de Workspace.

2. SqliteClientService combine :
   - accès aux données ;
   - initialisation de schéma ;
   - sauvegarde legacy ;
   - orchestration métier.

3. Les écritures ne sont pas transactionnelles.

C.2. Séparation contenu / structure

L’intention fonctionnelle existe partiellement :

- ClientDetailsPage permet l’édition des données.
- TemplateEditorPage sert à éditer la structure.
- ClientEditPage contient toutefois encore des handlers de modification de structure.

Fichiers concernés :

- Views/ClientDetailsPage.xaml
- Views/ClientDetailsPage.xaml.cs
- Views/ClientEditPage.xaml
- Views/ClientEditPage.xaml.cs
- ViewModels/ClientDetailsViewModel.cs
- ViewModels/ClientEditViewModel.cs

Incohérence importante :

Dans ClientEditViewModel, IsStructureEditing retourne toujours false. Pourtant ClientEditPage.xaml.cs contient des opérations pour ajouter, supprimer et déplacer des blocs et des champs.

Cela montre qu’une ancienne ou intermédiaire implémentation de l’édition de structure est encore présente dans le parcours d’édition du client.

Risque :

Le mode d’édition du contenu peut rester couplé à la structure, contrairement à la règle projet : l’édition d’un client doit modifier les données, tandis que la structure doit être éditée séparément.

C.3. Templates

Le service dynamique expose notamment :

- CreateTemplateAsync
- CloneTemplateAsync
- SetDefaultTemplateAsync
- EnsureClientTemplateAsync
- GetTemplatesAsync

La stratégie actuelle est :

1. créer un template ;
2. le cloner pour un client ;
3. créer un EntityRecord ;
4. créer des BlockInstance ;
5. enregistrer les valeurs dans FieldValue.

Cette stratégie respecte partiellement l’isolation structurelle, mais elle comporte un risque critique.

Dans GetOrCreateRecordAsync, lorsqu’un autre record existe pour le même contact, l’ancien record est supprimé avant la création du nouveau.

Risques :

- perte de données lors d’un changement de template ;
- absence d’historique ;
- absence de migration des valeurs compatibles ;
- absence de transaction entre suppression et recréation ;
- échec intermédiaire pouvant laisser une fiche incomplète.

C.4. Champs personnalisés

Les champs personnalisés sont stockés dans FieldDefinition et FieldValue.

Les options de champ sont sérialisées dans FieldDefinition.OptionsJson.
Les valeurs sont sérialisées dans FieldValue.ValueJson.

Cette approche est compatible avec la vision hybride du projet, mais elle doit être renforcée par :

- une validation typée ;
- une version du type de champ ;
- une stratégie de migration de valeur ;
- une contrainte d’unicité sur RecordId et FieldDefinitionId ;
- une distinction claire entre ValueJson et ConfigJson.

C.5. Relations entre contacts

Aucune relation métier entre contacts n’a été identifiée.

Il n’existe pas de structure pour :

- parent/enfant ;
- société/contact ;
- client/fournisseur ;
- contact lié à un projet ;
- contact lié à un événement ;
- membre d’un groupe.


D. AUDIT DU MOTEUR D’OBJETS DYNAMIQUES

D.1. Capacités existantes

Le moteur permet déjà :

- de créer un template ;
- de créer des blocs ;
- de créer des champs ;
- de modifier les champs ;
- de déplacer les blocs et les champs ;
- de créer une fiche depuis un template ;
- d’enregistrer des valeurs JSON ;
- de définir un template client par défaut ;
- de cloner une structure.

D.2. Limites

Il ne permet pas encore de gérer correctement :

- plusieurs types d’objets métier standard ;
- les relations entre objets ;
- les vues personnalisées ;
- les droits d’accès par objet ;
- le versionnement des définitions ;
- les évolutions compatibles d’une structure ;
- la suppression logique ;
- l’historique ;
- les objets appartenant explicitement à un Workspace pour les contacts ;
- la synchronisation des modifications.

D.3. Complexité excessive

SqliteDynamicFormService est responsable de nombreuses catégories d’opérations :

- création et clonage des templates ;
- gestion du template par défaut ;
- gestion des blocs ;
- gestion des champs ;
- gestion des records ;
- gestion des valeurs ;
- chargement d’agrégats ;
- initialisation SQLite ;
- contrôle d’isolation Workspace.

Ce service risque de devenir un point monolithique si les fonctionnalités de collaboration, relations, vues et synchronisation y sont ajoutées.


E. AUDIT SQLITE

E.1. Tables identifiées

- Clients
- Workspaces
- EntityTemplates
- BlockDefinitions
- FieldDefinitions
- EntityRecords
- BlockInstances
- FieldValues

E.2. Initialisation

Les tables sont créées dans plusieurs services :

- SqliteClientService
- SqliteDynamicFormService
- LocalWorkspaceContext
- CompleteBackupService

Chaque service construit sa propre connexion SQLite vers ultimate-manager.db3.

Problèmes :

- initialisation dispersée ;
- risque de concurrence entre initialisations ;
- absence de service central de base de données ;
- absence de version globale du schéma ;
- absence de migration contrôlée.

E.3. Migrations

Les migrations actuelles sont des tentatives ponctuelles avec ALTER TABLE, notamment pour DefaultTemplateId et ExternalEntityId.

Les exceptions SQLite sont ignorées.

Risques :

- une erreur autre que « colonne déjà existante » peut être masquée ;
- l’ordre des migrations n’est pas enregistré ;
- impossible de connaître de manière fiable la version réelle du schéma ;
- les installations intermédiaires sont difficiles à supporter.

E.4. Relations SQLite

Aucune clé étrangère n’est déclarée dans les modèles.

Les relations sont uniquement reconstruites par convention avec TemplateId, BlockDefinitionId, EntityRecordId et FieldDefinitionId.

Risques :

- données orphelines ;
- suppressions partielles ;
- incohérences entre templates et records ;
- absence de garantie d’intégrité.

E.5. Contraintes et index

Les identifiants utilisent bien Guid, ce qui est conforme à la cible.

Cependant, il manque notamment :

- des index composites suffisants ;
- une contrainte d’unicité sur les valeurs de champs ;
- une contrainte d’unicité sur les records par objet ;
- un contrôle d’appartenance Workspace sur toutes les entités ;
- un mécanisme de suppression logique.

E.6. Sauvegardes

Deux systèmes coexistent :

1. ClientBackupService : sauvegarde JSON des clients uniquement.
2. CompleteBackupService : archive ZIP contenant workspace, clients, templates, blocs, champs, records, valeurs et pièces jointes.

Cette coexistence est problématique car les deux formats représentent des visions différentes des données.

La restauration de CompleteBackupService exécute des suppressions globales des tables avant réinsertion.

Risques critiques :

- la restauration d’un Workspace peut supprimer les données des autres Workspaces ;
- la restauration n’est pas transactionnelle ;
- une interruption peut produire une base partiellement vidée ;
- les clients ne sont pas filtrés par Workspace ;
- les références aux fichiers de documents doivent rester valides après restauration.


F. AUDIT DES VIEWMODELS ET DE LA PRÉSENTATION

F.1. Points positifs

Les ViewModels utilisent principalement des abstractions :

- IClientService
- IDynamicFormService
- IMapProvider
- IAutomaticBackupService

Aucun ViewModel ne crée directement une connexion SQLite.

F.2. Couplage code-behind / conteneur DI

Plusieurs pages récupèrent directement des services depuis App.Services :

- ClientEditPage
- ClientDetailsPage
- ClientsPage
- ContactImportPage
- TemplatesPage

Les pages contiennent notamment de la logique de suppression, d’import, de résolution de services et d’orchestration de persistance.

Risque :

La couche Presentation connaît directement le conteneur DI et certains services applicatifs, ce qui rend les tests et la centralisation des règles plus difficiles.

F.3. Validation

La validation est très limitée :

- FirstName est obligatoire dans ClientEditViewModel ;
- FieldDefinition.IsRequired existe mais n’est pas réellement appliqué par le moteur de sauvegarde ;
- les types email, téléphone, nombre, date et URL ne semblent pas validés par un service métier central.

Il manque une validation de domaine, une validation des champs dynamiques et une stratégie de validation avant transaction.

F.4. Cartographie

Le projet possède déjà les abstractions IMapProvider et IPlaceSearchProvider.

Cependant l’implémentation est fortement orientée Google :

- GoogleMapProvider
- GooglePlaceSearchProvider

La consigne du dépôt recommande de commencer avec OpenStreetMap et d’isoler le fournisseur.

L’abstraction existe, mais le fournisseur actuellement utilisé n’est pas OpenStreetMap. GoogleMapProvider crée directement une page MAUI, ce qui rapproche le service d’infrastructure de la présentation.


G. PROBLÈMES IDENTIFIÉS ET PRIORITÉS

Priorité CRITIQUE

- Models/Client.cs — Client : aucun WorkspaceId. Risque de fuite de données entre espaces.
- SqliteDynamicFormService.GetOrCreateRecordAsync : suppression de l’ancien record lors d’un changement de template. Risque de perte de données.
- CompleteBackupService : restauration par suppressions globales. Risque de destruction de tous les Workspaces.
- Tous les services SQLite : pas de migrations versionnées. Évolution de schéma non contrôlée.
- SqliteDynamicFormService : opérations multi-écritures sans transaction. Données partielles ou orphelines.

Priorité HAUTE

- SqliteClientService : Client global alors que le dynamique est Workspace-scoped. Modèle de sécurité incohérent.
- ClientBackupService / CompleteBackupService : deux systèmes de sauvegarde concurrents. Comportements divergents.
- DynamicFormModels.cs : modèles SQLite directement utilisés comme modèles métier. Couplage persistance/domaine.
- SqliteDynamicFormService : service regroupant trop de responsabilités. Classe difficile à maintenir et tester.
- ClientEditPage.xaml.cs : modification de structure présente dans le parcours d’édition client. Violation de la séparation contenu/structure.
- ClientEditViewModel : IsStructureEditing toujours à false malgré les handlers structurels. Fonctionnalité incohérente ou morte.

Priorité MOYENNE

- CompleteBackupService : export des clients non filtré par Workspace.
- FieldValue : pas de contrainte d’unicité record/champ. Doublons possibles.
- FieldDefinition.OptionsJson : configuration peu générique.
- ContactImporter : import basé sur le nom affiché et un seul téléphone/email. Doublons et perte d’informations.
- MauiProgram.cs : composition centralisée mais services d’infrastructure directement exposés.
- GoogleMapProvider : service d’abstraction créant directement une page MAUI.

Priorité BASSE

- UltimateManager.csproj : fichiers temporaires .tmp visibles dans le projet. Risque de confusion ou d’inclusion accidentelle.


H. ARCHITECTURE CIBLE RECOMMANDÉE

Une migration progressive est recommandée.

Projets cibles :

- UltimateManager.Domain
- UltimateManager.Application
- UltimateManager.Infrastructure
- UltimateManager.Presentation
- UltimateManager.App

H.1. Domain

Le Domain contiendrait les modèles et règles métier :

- Workspace
- User
- WorkspaceMember
- Group
- GroupUserMember
- GroupContactMember
- Role
- Permission
- Contact
- Task
- Project
- Event
- Document
- ObjectDefinition
- FieldDefinition
- ObjectRecord
- ObjectRelation
- ObjectTemplate
- ObjectView

Le Domain ne doit dépendre ni de MAUI ni de SQLite.

H.2. Application

L’Application contiendrait les cas d’utilisation et interfaces :

- contacts ;
- Workspaces ;
- templates ;
- objets ;
- collaboration ;
- sauvegardes ;
- repositories ;
- synchronisation.

H.3. Infrastructure

L’Infrastructure contiendrait :

- SQLiteConnectionFactory ;
- SchemaMigrator ;
- repositories ;
- sauvegarde Workspace ;
- fournisseurs cartographiques ;
- synchronisation.

H.4. Presentation

La Presentation contiendrait les Views, ViewModels, navigation et converters.

Les ViewModels dépendraient des interfaces applicatives et non de App.Services.

H.5. Objets métier standards

Les objets standards ne doivent pas tous devenir des ObjectRecord.

Recommandation :

- Contact, Task, Project, Event et Document restent des agrégats spécialisés.
- Les champs personnalisés sont associés à leur objet via un mécanisme commun.
- ObjectRecord est réservé aux types créés par l’utilisateur ou aux objets réellement dynamiques.


I. MODÈLE SQLITE RECOMMANDÉ

I.1. Collaboration

Tables recommandées :

- Users
- Workspaces
- WorkspaceMembers
- Groups
- GroupUserMembers
- GroupContactMembers
- Roles
- Permissions
- RolePermissions

Les membres d’un Workspace doivent avoir une contrainte d’unicité sur WorkspaceId et UserId.

I.2. Contacts

Tables recommandées :

- Contacts
- ContactAddresses
- ContactPhoneNumbers

Contacts doit contenir au minimum :

- Id
- WorkspaceId
- données principales
- CreatedAt
- UpdatedAt
- CreatedBy
- UpdatedBy
- Version
- DeletedAt

I.3. Moteur d’objets

Tables recommandées :

- ObjectDefinitions
- FieldDefinitions
- ObjectRecords
- ObjectFieldValues
- ObjectRelations

ObjectFieldValues doit avoir une contrainte d’unicité sur RecordId et FieldDefinitionId.

I.4. Templates et vues

Tables recommandées :

- ObjectTemplates
- ObjectTemplateFields
- ObjectViews

I.5. Synchronisation

Les tables synchronisables doivent disposer de :

- CreatedAt
- UpdatedAt
- CreatedBy
- UpdatedBy
- Version
- DeletedAt

Une table SyncChanges peut journaliser les opérations de synchronisation.


J. PLAN DE MIGRATION SANS PERTE

1. Geler le schéma et sauvegarder l’existant.
   - Copier le fichier SQLite.
   - Exporter les données.
   - Inventorier les incohérences.

2. Introduire un gestionnaire de schéma versionné.
   - Ajouter SchemaVersion.
   - Journaliser chaque migration.
   - Exécuter les migrations dans des transactions lorsque possible.

3. Créer le Workspace personnel explicite.
   - Associer tous les clients existants au Workspace local.
   - Conserver les Guid existants.

4. Stabiliser les contacts.
   - Migrer Clients vers Contacts.
   - Extraire adresses et téléphones.
   - Conserver temporairement une couche de compatibilité.

5. Séparer template et record.
   - Ne plus supprimer l’ancien record.
   - Créer une nouvelle version de template.
   - Copier uniquement les champs compatibles.
   - Effectuer l’opération dans une transaction.

6. Remplacer progressivement les concepts Entity*.
   - EntityTemplate vers ObjectTemplate.
   - EntityRecord vers ObjectRecord.
   - EntityType vers ObjectDefinitionId.
   - OptionsJson vers ConfigJson.
   - FieldValue vers ObjectFieldValue.

7. Introduire les relations.
   - Créer ObjectRelations.
   - Ne pas inventer automatiquement les relations absentes.

8. Unifier les sauvegardes.
   - Définir un format versionné unique.
   - Conserver l’ancien JSON uniquement comme import legacy.

9. Ajouter les métadonnées de synchronisation.
   - Version.
   - Suppression logique.
   - Auteur.
   - Dates UTC.

10. Introduire la synchronisation cloud uniquement après stabilisation des étapes précédentes.


K. PLAN DE DÉVELOPPEMENT

Mission 1 — Sécuriser SQLite, les migrations et les sauvegardes

- centraliser l’accès SQLite ;
- ajouter une version de schéma ;
- ajouter des transactions ;
- empêcher la restauration globale accidentelle ;
- produire une sauvegarde avant migration.

Mission 2 — Isoler les données par Workspace

- ajouter WorkspaceId aux contacts ;
- filtrer toutes les lectures et écritures ;
- adapter imports et sauvegardes ;
- tester l’isolation entre deux Workspaces.

Mission 3 — Stabiliser l’édition des fiches

- séparer édition des données et édition de structure ;
- retirer les opérations structurelles de ClientEditPage ;
- garantir Enregistrer/Annuler ;
- conserver les valeurs lors d’un changement de structure.

Mission 4 — Préparer la migration du moteur dynamique

- introduire ObjectDefinition, ObjectRecord et ObjectTemplate ;
- conserver une compatibilité avec les tables existantes ;
- créer une couche de traduction.

Mission 5 — Ajouter les relations entre objets

- créer ObjectRelation ;
- ajouter les relations Contact/Group/Project/Task/Event ;
- tester l’isolation Workspace ;
- définir les suppressions logiques.

Mission 6 — Séparer les couches

- extraire Domain ;
- extraire Application ;
- déplacer SQLite vers Infrastructure ;
- garder MAUI dans Presentation/App ;
- supprimer progressivement les accès App.Services depuis les pages.

Mission 7 — Standardiser la validation

- validation métier centralisée ;
- validation des champs dynamiques ;
- validation des valeurs JSON par type ;
- erreurs structurées.

Mission 8 — Ajouter la collaboration

- utilisateurs ;
- membres de Workspace ;
- groupes ;
- rôles ;
- permissions.

Mission 9 — Préparer la synchronisation offline/cloud

- versions ;
- journal de changements ;
- suppressions logiques ;
- résolution de conflits.

Mission 10 — Remplacer ou compléter la cartographie

- introduire un fournisseur OpenStreetMap ;
- conserver IMapProvider ;
- permettre le choix du fournisseur.


L. DÉCISIONS ARCHITECTURALES NÉCESSAIRES

Les choix suivants ne peuvent pas être déduits uniquement du code :

- identité utilisateur : compte local, Microsoft Entra ID ou autre système ;
- stratégie de synchronisation et résolution des conflits ;
- portée des templates : personnelle, Workspace ou organisation ;
- comportement lors d’un changement de structure ;
- rôle exact des objets métier standards ;
- permissions minimales ;
- fournisseur cartographique par défaut.


M. CINQ PREMIÈRES MISSIONS RECOMMANDÉES

1. Sécuriser SQLite, les migrations et les sauvegardes.

2. Ajouter l’isolation Workspace aux contacts.

3. Corriger la séparation entre édition du contenu et édition de la structure.

4. Introduire une couche Application et des repositories.

5. Transformer le moteur de formulaires en moteur d’objets versionné.


CONCLUSION

L’application possède déjà une base fonctionnelle intéressante :

- identifiants Guid ;
- fonctionnement local/offline ;
- abstraction partielle de cartographie ;
- moteur relationnel de blocs, champs et valeurs ;
- sauvegarde complète incluant les pièces jointes ;
- injection de dépendances centralisée.

Cependant, elle n’est pas encore prête pour la cible collaborative et multi-Workspace.

Les risques prioritaires sont :

1. contacts non isolés par Workspace ;
2. restauration destructive globale ;
3. absence de migrations versionnées ;
4. absence de transactions ;
5. suppression de données lors d’un changement de template ;
6. coexistence de deux systèmes de sauvegarde ;
7. couplage entre modèle SQLite, domaine et services ;
8. absence de collaboration, utilisateurs, groupes, rôles et permissions ;
9. moteur actuel limité à des fiches dynamiques clonées ;
10. incohérence entre édition du contenu et édition de la structure.

La priorité doit être la sécurisation et la conservation des données avant l’ajout de nouveaux types d’objets ou de fonctionnalités collaboratives.
