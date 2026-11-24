MISSION 1 — PLAN DE SÉCURISATION SQLITE

Statut : analyse et plan uniquement.
Aucun code métier, aucune base SQLite, aucune dépendance et aucune configuration applicative ne doivent être modifiés pendant cette phase.


1. OBJECTIF

Sécuriser la persistance locale avant l’introduction de l’isolation complète par Workspace, de la collaboration, des nouveaux objets métier et de la synchronisation cloud.

La mission doit garantir :

- une initialisation SQLite déterministe ;
- des migrations versionnées ;
- des opérations multi-écritures atomiques ;
- des sauvegardes cohérentes ;
- une restauration vérifiée et non destructive en cas d’échec ;
- l’absence de perte des données existantes.


2. ÉTAT ACTUEL VÉRIFIÉ

2.1. Connexions SQLite multiples

Les services suivants créent chacun leur propre SQLiteAsyncConnection vers ultimate-manager.db3 :

- Services/SqliteClientService.cs
- Services/SqliteDynamicFormService.cs
- Services/LocalWorkspaceContext.cs
- Services/CompleteBackupService.cs

Cela crée plusieurs points d’initialisation et plusieurs propriétaires de la base.

2.2. Initialisation dispersée

Les tables sont créées directement dans plusieurs services avec CreateTableAsync.

Les colonnes sont ajoutées avec des migrations ponctuelles :

- Workspaces.DefaultTemplateId
- EntityRecords.ExternalEntityId

Les exceptions SQLiteException sont ignorées, ce qui empêche de distinguer une colonne déjà présente d’une erreur de schéma, de corruption ou d’accès.

2.3. Absence de version de schéma

Il n’existe pas actuellement de table ou information centralisée permettant de connaître la version du schéma SQLite.

Conséquences :

- migrations non ordonnées ;
- impossibilité de diagnostiquer une base intermédiaire ;
- difficulté à restaurer ou migrer des versions anciennes ;
- risque d’appliquer des évolutions dans le mauvais ordre.

2.4. Opérations non transactionnelles

Plusieurs opérations écrivent plusieurs lignes liées sans transaction explicite :

- clonage de template ;
- création d’un record et de ses blocs ;
- suppression d’un template, de ses blocs et de ses champs ;
- suppression puis recréation d’un record ;
- restauration complète.

Une interruption peut laisser un template, record, bloc ou valeur dans un état partiel.

2.5. Restauration destructive

CompleteBackupService supprime globalement plusieurs tables avant réinsertion :

- FieldValues
- BlockInstances
- EntityRecords
- FieldDefinitions
- BlockDefinitions
- EntityTemplates
- Clients

Cette opération n’est pas protégée par une restauration complète réversible.

Une erreur après les suppressions peut laisser la base vide ou partiellement reconstruite.

2.6. Limite Workspace

La table Clients ne contient pas encore WorkspaceId.

Il est donc impossible de garantir une restauration par Workspace pour les clients dans cette mission sans anticiper la mission 2.

La mission 1 doit sécuriser la restauration complète actuelle et empêcher les suppressions irréversibles en cas d’erreur. L’isolation complète des contacts sera traitée dans la mission suivante.


3. PÉRIMÈTRE

Inclus :

- centralisation de l’accès SQLite ;
- initialisation unique et déterministe ;
- versionnement du schéma ;
- migrations idempotentes et journalisées ;
- transactions pour les opérations multi-écritures ;
- sauvegarde atomique ;
- validation complète d’une archive avant restauration ;
- restauration vérifiée et réversible en cas d’échec ;
- tests de migration, transaction et restauration ;
- compatibilité temporaire avec les données actuelles.

Exclus :

- ajout de WorkspaceId à Client ;
- migration Clients vers Contacts ;
- utilisateurs, groupes et permissions ;
- remplacement du moteur Entity* par Object* ;
- synchronisation cloud ;
- extraction immédiate en plusieurs projets ;
- modification de l’interface ;
- mise à jour des dépendances NuGet ;
- suppression immédiate du système de sauvegarde legacy.


4. FICHIERS À EXAMINER

- Services/SqliteClientService.cs
- Services/SqliteDynamicFormService.cs
- Services/LocalWorkspaceContext.cs
- Services/CompleteBackupService.cs
- Services/ClientBackupService.cs
- MauiProgram.cs
- Models/Client.cs
- Models/DynamicForms/DynamicFormModels.cs

Les composants suivants sont des propositions et ne sont pas encore présents :

- fournisseur ou fabrique de connexion SQLite ;
- gestionnaire de schéma ;
- registre de migrations ;
- service transactionnel ;
- validateur d’archive ;
- stratégie de restauration sûre.


5. PLAN D’IMPLÉMENTATION PROPOSÉ

Étape 1 — Capturer l’état initial

Avant toute migration :

1. Copier ultimate-manager.db3.
2. Créer une sauvegarde complète avec le format actuel.
3. Exporter les tables existantes.
4. Relever les tables, colonnes, volumes, Guid et relations orphelines.
5. Identifier les records sans template et les valeurs sans champ.
6. Conserver cet état comme référence.

Critère : les comptages et identifiants doivent être comparables avant et après chaque migration.

Étape 2 — Définir une version de schéma

Introduire un mécanisme centralisé, par exemple une table SchemaInfo contenant :

- Id ;
- Version ;
- AppliedAt.

Règles :

- une migration possède un numéro unique ;
- une migration appliquée n’est pas rejouée ;
- une migration échouée n’est pas enregistrée comme terminée ;
- les erreurs complètes sont journalisées ;
- une version inconnue ou supérieure est refusée si elle ne peut pas être gérée.

Critère : une base neuve et une base existante atteignent la même version finale.

Étape 3 — Centraliser l’initialisation

Déplacer progressivement la création des tables hors de SqliteClientService, SqliteDynamicFormService, LocalWorkspaceContext et CompleteBackupService.

Les services doivent utiliser le même composant de connexion et attendre une initialisation commune.

Objectifs :

- un seul chemin d’initialisation ;
- une seule stratégie de migration ;
- une seule gestion des erreurs ;
- suppression des ALTER TABLE silencieux ;
- limitation des initialisations concurrentes.

Vérifier les capacités transactionnelles de sqlite-net-pcl 1.11.285 avant de choisir l’API utilisée. Ne pas mettre à jour le package dans cette mission.

Étape 4 — Transformer les évolutions existantes en migrations

Convertir les ALTER TABLE existants en migrations nommées :

- création initiale des tables ;
- ajout de Workspaces.DefaultTemplateId ;
- ajout de EntityRecords.ExternalEntityId.

Chaque migration doit :

1. vérifier l’état attendu ;
2. appliquer l’évolution ;
3. vérifier le résultat ;
4. enregistrer sa version uniquement après succès.

Ne plus utiliser de catch SQLiteException vide.

Étape 5 — Ajouter les transactions critiques

Rendre atomiques :

- création d’un record et de ses BlockInstances ;
- clonage d’un template, de ses blocs et champs ;
- suppression d’un template, de ses champs et blocs ;
- suppression/recréation de record ;
- opérations de restauration.

Une erreur volontaire au milieu de chaque opération doit laisser la base dans son état initial.

La stratégie fonctionnelle de conservation des anciennes valeurs lors d’un changement de template sera traitée dans la mission d’édition des templates.

Étape 6 — Sécuriser les sauvegardes

Vérifier et renforcer :

- fermeture des flux avant remplacement ;
- cohérence des données exportées ;
- présence des références ;
- gestion des pièces jointes manquantes ;
- annulation par CancellationToken ;
- remplacement atomique ;
- conservation de la dernière sauvegarde valide si la nouvelle échoue.

Le manifeste devrait contenir :

- version du format ;
- date UTC ;
- Workspace concerné lorsque possible ;
- nombre de lignes par type ;
- liste ou hash des fichiers ;
- version des données.

L’export des clients est actuellement global alors que les données dynamiques sont filtrées par Workspace. Cette limite doit être documentée jusqu’à la mission 2.

Étape 7 — Sécuriser la restauration

Avant toute écriture :

1. vérifier l’archive ;
2. vérifier le manifeste ;
3. vérifier la version du format ;
4. désérialiser tous les JSON ;
5. vérifier les Guid et références ;
6. détecter les doublons ;
7. vérifier les pièces jointes ;
8. vérifier la cohérence templates/blocs/champs/records/valeurs.

Options :

Option court terme :

- valider l’archive ;
- ouvrir une transaction ;
- écrire uniquement après validation ;
- restaurer l’état initial si une écriture échoue.

Option recommandée à moyen terme :

- restaurer dans une base temporaire ;
- appliquer les migrations ;
- valider la base temporaire ;
- remplacer la base active uniquement après succès ;
- conserver l’ancienne base comme copie de sécurité.

Cette seconde option doit être testée sur Android, iOS, MacCatalyst et Windows.

Étape 8 — Encadrer la sauvegarde legacy

Conserver ClientBackupService temporairement pour ne pas casser les imports existants, mais le documenter comme format legacy limité aux clients.

Il ne doit pas être considéré comme équivalent à la sauvegarde complète.

Étape 9 — Ajouter les tests

Migrations :

- base vide ;
- base existante ;
- colonnes déjà présentes ;
- migration interrompue ;
- reprise après échec ;
- version inconnue.

Transactions :

- échec pendant création de record ;
- échec pendant clonage ;
- échec pendant suppression ;
- annulation ;
- absence de données partielles.

Sauvegardes :

- archive valide ;
- archive incomplète ;
- pièce jointe manquante ;
- écriture interrompue ;
- conservation de la sauvegarde précédente ;
- format inconnu.

Restaurations :

- archive valide ;
- archive invalide sans modification de la base ;
- erreur d’écriture avec retour à l’état initial ;
- Guid dupliqué ;
- référence orpheline ;
- base lisible après redémarrage.

Les tests doivent respecter .github/instructions/testing.instructions.md.


6. CRITÈRES D’ACCEPTATION

La mission sera terminée lorsque :

1. Une base neuve est initialisée avec une version connue.
2. Une base existante est migrée de façon déterministe.
3. Une migration déjà appliquée n’est pas rejouée.
4. Les erreurs de migration ne sont plus ignorées silencieusement.
5. Les opérations multi-tables critiques sont atomiques.
6. Une erreur ne laisse pas de données partielles.
7. Une archive invalide ne modifie pas la base.
8. Une restauration échouée conserve l’état précédent.
9. La dernière sauvegarde valide reste disponible si la nouvelle échoue.
10. Les identifiants et volumes sont comparables avant et après migration.
11. Les tests couvrent migrations, transactions, sauvegardes et restaurations.
12. La limite actuelle de Client sans WorkspaceId est documentée pour la mission 2.


7. ORDRE RECOMMANDÉ DES COMMITS

1. audit: capture sqlite schema and data invariants
2. persistence: introduce schema version metadata
3. persistence: centralize database initialization
4. persistence: migrate existing ad hoc schema changes
5. persistence: add transactions to critical writes
6. backup: validate archives before restore
7. backup: make restore failure-safe
8. tests: cover migration transaction and restore failures
9. docs: record sqlite hardening status

Chaque commit doit rester compilable lorsque cela est possible et être accompagné de tests ciblés.


8. DÉCISIONS À PRENDRE AVANT IMPLÉMENTATION

- Choisir l’API transactionnelle compatible avec sqlite-net-pcl 1.11.285.
- Choisir entre transaction sur la base active et base temporaire puis remplacement du fichier.
- Décider si un échec de sauvegarde annule l’écriture utilisateur ou est signalé séparément.
- Définir la durée de support de ClientBackupService comme format legacy.


9. CONCLUSION

L’ordre recommandé est :

1. observer et sauvegarder ;
2. versionner ;
3. centraliser ;
4. transactionnaliser ;
5. valider les archives ;
6. rendre la restauration réversible ;
7. tester les scénarios d’échec ;
8. passer ensuite à l’isolation des contacts par Workspace.

Cette mission doit sécuriser les données existantes sans anticiper les migrations métier de la mission 2.
