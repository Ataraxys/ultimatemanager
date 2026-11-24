PLAN DE TESTS — ISOLATION WORKSPACES, CONSERVATION DES DONNÉES ET RESTAURATIONS

Statut : plan de validation uniquement.
Aucun test, code de production, fichier SQLite ou dépendance n’a été modifié.


1. ÉTAT DE DÉPART VÉRIFIÉ

Aucun projet ou fichier de tests n’a été trouvé dans le projet actuel.

Les services concernés sont notamment :

- Services/SqliteClientService.cs
- Services/SqliteDynamicFormService.cs
- Services/CompleteBackupService.cs
- Services/ClientBackupService.cs
- Services/LocalWorkspaceContext.cs

Les modèles dynamiques actuels contiennent WorkspaceId pour :

- EntityTemplate
- EntityRecord

En revanche, Client ne contient pas encore WorkspaceId.

Conséquence :

- l’isolation des templates et records dynamiques peut être testée dès maintenant ;
- l’isolation des contacts ne peut pas être déclarée conforme avant l’ajout et la migration de Client.WorkspaceId ;
- les tests contacts doivent être préparés mais marqués bloqués ou contractuels jusqu’à la mission d’isolation des contacts.

Les règles de test de référence sont définies dans :

.github/instructions/testing.instructions.md


2. OBJECTIFS DE QUALITÉ

Les tests doivent démontrer que :

1. Un Workspace ne peut pas lire les templates, records ou valeurs d’un autre Workspace.
2. Une tentative d’accès croisé est refusée ou retourne une absence contrôlée.
3. Une opération multi-écritures échouée ne laisse pas de données partielles.
4. Un changement de template ne supprime pas silencieusement les données existantes.
5. Une archive invalide ne modifie jamais la base courante.
6. Une restauration échouée conserve l’état précédent.
7. Une sauvegarde restaurée conserve les identifiants, relations et valeurs attendus.
8. Les pièces jointes sont restaurées avec des références valides.
9. Les migrations peuvent être rejouées sans doubler ni détruire les données.
10. Les annulations et erreurs ne laissent pas la persistance dans un état incohérent.

La compilation seule ne constitue pas une preuve de conformité.


3. STRATÉGIE DE TEST

3.1. Tests unitaires purs

À utiliser pour :

- calcul des ordres de blocs et champs ;
- validation des références ;
- validation des manifests ;
- sérialisation et désérialisation des valeurs ;
- détection des doublons ;
- règles de migration sans accès réel à MAUI.

3.2. Tests d’intégration SQLite

À utiliser pour :

- requêtes réelles ;
- filtrage Workspace ;
- transactions ;
- migrations ;
- contraintes d’intégrité ;
- sauvegarde et restauration sur une base temporaire.

Chaque test doit utiliser une base temporaire isolée et être indépendant des données de développement de l’application.

3.3. Tests de scénario

À utiliser pour simuler :

- deux Workspaces actifs ;
- des templates similaires dans deux Workspaces ;
- des records liés au même ExternalEntityId mais dans des Workspaces différents ;
- une restauration interrompue ;
- une migration à partir de données existantes.


4. FIXTURES DE TEST RECOMMANDÉES

4.1. Workspaces

Créer au minimum :

- Workspace A : Guid fixe de test.
- Workspace B : Guid fixe différent.

Ne jamais réutiliser le Workspace personnel implicite de l’application dans les tests.

4.2. Templates

Créer :

- Template A dans Workspace A, nommé « Client ».
- Template B dans Workspace B, nommé « Client ».
- Template privé A2 dans Workspace A.
- Un bloc par template.
- Plusieurs champs par bloc : texte, email, téléphone, adresse, nombre et document.

Les templates A et B doivent avoir au moins un nom et un champ identiques afin de détecter les confusions d’identifiants et non seulement les confusions de libellés.

4.3. Records

Créer :

- Record A dans Workspace A.
- Record B dans Workspace B.
- Même ExternalEntityId utilisé dans les deux Workspaces.
- Valeurs différentes pour chaque record.

Exemple :

- Workspace A : prénom « Alice ».
- Workspace B : prénom « Bob ».

4.4. Valeurs

Ajouter au moins :

- une valeur texte ;
- une valeur structurée AddressValue ;
- une valeur PhoneNumberValue ;
- une valeur GeoPointValue ;
- une valeur DocumentValue ;
- une valeur vide ;
- une valeur JSON invalide pour test d’erreur.

4.5. Pièces jointes

Préparer :

- un fichier existant ;
- un fichier manquant ;
- un nom contenant des caractères à normaliser ;
- deux documents avec le même nom mais des contenus différents.


5. TESTS D’ISOLATION WORKSPACE

5.1. Lecture de template

Scénario :

1. Créer le Template A dans Workspace A.
2. Créer le Template B dans Workspace B.
3. Exécuter GetTemplateAsync(TemplateA.Id) avec le contexte A.
4. Exécuter GetTemplateAsync(TemplateA.Id) avec le contexte B.

Résultats attendus :

- contexte A : Template A est retourné ;
- contexte B : résultat nul ou erreur d’accès contrôlée ;
- aucune donnée du Template A n’est exposée au contexte B.

Priorité : critique.

5.2. Liste des templates

Scénario :

1. Créer plusieurs templates dans A et B.
2. Appeler GetTemplatesAsync dans A.
3. Appeler GetTemplatesAsync dans B.

Résultats attendus :

- A ne retourne que les templates autorisés de A ;
- B ne retourne que les templates autorisés de B ;
- aucun filtrage ne repose uniquement sur le nom du template ;
- les templates de même nom restent distingués par leur Guid.

Priorité : critique.

5.3. Lecture de record

Scénario :

1. Créer Record A dans A et Record B dans B.
2. Utiliser le même ExternalEntityId.
3. Charger Record A avec le contexte A.
4. Charger Record A avec le contexte B.
5. Appeler GetRecordForExternalEntityAsync dans chaque contexte.

Résultats attendus :

- A ne lit que Record A ;
- B ne lit que Record B ;
- le même ExternalEntityId ne provoque aucune fuite croisée ;
- le résultat retourné correspond au Workspace courant.

Priorité : critique.

5.4. Écriture de valeur sur un record étranger

Scénario :

1. Créer Record A et un champ A.
2. Utiliser le contexte B.
3. Appeler SaveFieldValueAsync(RecordA.Id, FieldA.Id, ...).

Résultat attendu :

- l’opération échoue avec une erreur contrôlée ;
- aucune FieldValue n’est créée ou modifiée ;
- UpdatedAt du record A reste inchangé.

Priorité : critique.

5.5. Modification d’un template étranger

Tester avec le contexte B :

- UpdateBlockAsync sur un bloc de A ;
- UpdateFieldAsync sur un champ de A ;
- DeleteBlockAsync sur un bloc de A ;
- DeleteFieldAsync sur un champ de A ;
- MoveBlockAsync sur un template de A ;
- MoveFieldAsync sur un bloc de A ;
- SetDefaultTemplateAsync avec un template de A.

Résultats attendus :

- toutes les opérations sont refusées ;
- les données de A restent inchangées ;
- aucune modification partielle n’est observée.

Priorité : critique.

5.6. Création dans le Workspace courant

Vérifier que :

- CreateTemplateAsync utilise le Workspace courant ;
- CreateRecordAsync utilise le Workspace courant ;
- GetOrCreateRecordAsync utilise le Workspace courant ;
- EnsureClientTemplateAsync ne crée jamais de données dans un autre Workspace.

Priorité : haute.

5.7. Changement de contexte

Scénario :

1. Charger le service avec le contexte A.
2. Charger les données A.
3. Changer vers le contexte B dans un nouveau scope de test.
4. Recharger les mêmes identifiants.

Résultat attendu :

- aucune donnée précédemment chargée ne doit être réutilisée par erreur ;
- les collections et agrégats doivent correspondre au nouveau contexte ;
- les caches éventuels doivent être invalidés ou explicitement scoped.

Priorité : haute.


6. TESTS CONTACTS ET WORKSPACEID

6.1. État actuel

Ces tests ne peuvent pas être déclarés passants avec le modèle actuel, car Client ne contient pas WorkspaceId et SqliteClientService ne filtre pas par Workspace.

6.2. Tests à préparer pour la mission suivante

Après ajout de Client.WorkspaceId, ajouter :

- GetAllAsync ne retourne que les contacts du Workspace courant ;
- GetByIdAsync refuse un contact d’un autre Workspace ;
- SearchAsync ne recherche jamais dans un autre Workspace ;
- CreateAsync affecte le Workspace courant ;
- UpdateAsync refuse un contact étranger ;
- DeleteAsync refuse un contact étranger ;
- import legacy n’insère que dans le Workspace courant ;
- restauration d’un Workspace ne touche pas les contacts d’un autre Workspace.

Priorité : critique pour la mission d’isolation des contacts.


7. TESTS D’ABSENCE DE PERTE DE DONNÉES

7.1. Snapshot avant/après

Pour chaque scénario destructif :

1. Créer un jeu de données complet.
2. Capturer un snapshot logique :
   - identifiants ;
   - nombre de lignes ;
   - contenu des champs ;
   - UpdatedAt ;
   - relations entre tables.
3. Exécuter l’opération.
4. Comparer les éléments qui doivent rester inchangés.

Le snapshot doit comparer le contenu et pas uniquement le nombre de lignes.

7.2. Clonage de template

Scénario nominal :

1. Créer un template avec plusieurs blocs et champs.
2. Cloner le template.
3. Comparer la structure source et cible.

Résultats attendus :

- le clone possède un nouvel identifiant ;
- les champs et blocs sont présents ;
- les ordres sont conservés ;
- IsRequired est conservé ;
- OptionsJson est conservé ;
- aucune valeur du record source n’est copiée par erreur ;
- le template source reste inchangé.

7.3. Échec pendant clonage

Simuler une erreur après :

- création du template ;
- création d’un bloc ;
- création d’un champ ;
- mise à jour des propriétés du champ.

Résultats attendus :

- aucun clone partiel n’est visible après l’échec ;
- le template source reste intact ;
- les identifiants du clone partiel ne sont pas réutilisés ;
- la transaction est annulée.

Priorité : critique.

7.4. Suppression de champ

État actuel à vérifier explicitement : DeleteFieldAsync supprime la définition mais ne traite pas visiblement les FieldValue existantes.

Le test doit vérifier :

- existence des valeurs avant suppression ;
- comportement réel après suppression ;
- absence de données orphelines ;
- possibilité ou non de restaurer la structure.

Tant qu’une règle métier n’est pas décidée, le défaut recommandé est de bloquer la suppression destructive ou d’archiver la définition.

Priorité : critique.

7.5. Suppression de bloc

Même vérification avec :

- plusieurs champs dans le bloc ;
- plusieurs records utilisant le template ;
- valeurs différentes par record.

Résultat attendu à définir :

- suppression logique ;
- archivage ;
- migration explicite ;
- ou refus si des records utilisent le bloc.

Aucune perte silencieuse ne doit être acceptée.

7.6. Changement de template d’un record

Scénario :

1. Créer un record avec plusieurs valeurs.
2. Associer un nouveau template.
3. Exécuter le flux de changement actuel.
4. Comparer les valeurs avant et après.

Le code actuel appelle DeleteRecordDataAsync lorsqu’un autre record existe pour le même ExternalEntityId.

Résultat attendu pour la sécurité :

- l’ancien record ne doit pas être supprimé sans décision explicite ;
- les valeurs compatibles doivent être conservées ou migrées ;
- les valeurs incompatibles doivent être signalées ;
- une erreur doit laisser l’ancien état intact.

Priorité : critique.

7.7. Mise à jour d’une valeur

Vérifier :

- une seule valeur par couple record/champ ;
- l’ancienne valeur est remplacée sans créer de doublon ;
- UpdatedAt du champ et du record est cohérent ;
- une erreur de sérialisation ne modifie pas l’ancienne valeur ;
- l’annulation ne laisse pas une nouvelle valeur partiellement écrite.

Priorité : haute.

7.8. Modification d’un type de champ

Scénario :

1. Créer une valeur compatible avec le type initial.
2. Modifier FieldType.
3. Relire la valeur.

Résultat attendu à décider :

- migration automatique ;
- conversion validée ;
- blocage du changement ;
- conservation de l’ancienne valeur avec avertissement.

Aucune conversion silencieuse ne doit entraîner une perte de contenu.


8. TESTS DE TRANSACTIONS ET D’ANNULATION

8.1. Annulation avant opération

Annuler le CancellationToken avant chaque opération publique.

Résultat attendu :

- aucune écriture ;
- aucune suppression ;
- aucune modification de UpdatedAt.

8.2. Annulation pendant opération

Utiliser un token annulé pendant une opération multi-écritures.

Résultat attendu :

- rollback complet ;
- aucune table partiellement modifiée ;
- exception ou résultat d’annulation documenté.

8.3. Échec d’une écriture intermédiaire

Injecter une erreur contrôlée dans le repository ou le provider SQLite de test.

Scénarios :

- insert template réussi puis insert block échoué ;
- insert block réussi puis insert field échoué ;
- suppression d’un champ réussie puis normalisation échouée ;
- mise à jour de valeur réussie puis mise à jour du record échouée.

Résultat attendu :

- état initial restauré ;
- aucun orphelin ;
- erreur observable et diagnostiquable.


9. TESTS DE SAUVEGARDE

9.1. Périmètre exporté

Avec deux Workspaces, vérifier le contenu de l’archive.

État actuel à contrôler : CompleteBackupService filtre les templates et records par Workspace, mais charge tous les Client.

Le test doit signaler ce comportement comme défaut tant que Client n’est pas Workspace-scoped.

Vérifier :

- Workspace attendu dans manifest.json ;
- templates du Workspace ;
- blocs associés ;
- champs associés ;
- records associés ;
- BlockInstances associés ;
- FieldValues associées ;
- clients réellement attendus selon la règle actuelle.

9.2. Archive complète nominale

Créer une archive contenant :

- plusieurs templates ;
- plusieurs records ;
- toutes les catégories de valeurs ;
- des pièces jointes.

Vérifier :

- manifest.json présent ;
- version de format correcte ;
- fichiers JSON lisibles ;
- nombre de lignes cohérent ;
- références internes valides ;
- contenu des valeurs identique ;
- pièces jointes présentes.

9.3. Archive interrompue

Simuler une interruption pendant :

- écriture JSON ;
- compression d’une pièce jointe ;
- copie d’une pièce jointe ;
- déplacement du fichier temporaire.

Résultats attendus :

- la sauvegarde précédente reste utilisable ;
- le fichier temporaire incomplet n’est pas considéré comme valide ;
- aucune archive finale partielle n’est annoncée comme disponible.

9.4. Pièce jointe manquante

Créer un FieldValue DocumentValue pointant vers un fichier supprimé avant la sauvegarde.

Résultat attendu à définir :

- sauvegarde refusée ;
- document exporté avec état manquant ;
- avertissement explicite.

Il ne doit pas y avoir de référence silencieusement invalide.


10. TESTS DE RESTAURATION

10.1. Restauration nominale

1. Créer une base source.
2. Créer une archive.
3. Modifier la base source.
4. Restaurer l’archive.
5. Comparer avec le snapshot initial.

Vérifier :

- mêmes identifiants ;
- mêmes templates ;
- mêmes blocs ;
- mêmes champs ;
- mêmes records ;
- mêmes valeurs ;
- documents restaurés avec chemins valides ;
- Workspace courant correctement retrouvé.

10.2. Archive absente

Résultat attendu :

- erreur contrôlée ;
- aucune modification de la base ;
- aucun fichier existant supprimé.

10.3. Archive invalide

Tester :

- ZIP illisible ;
- manifest absent ;
- version non supportée ;
- JSON invalide ;
- élément obligatoire manquant ;
- Guid invalide ;
- référence inexistante ;
- doublons d’identifiants.

Résultats attendus :

- restauration refusée avant suppression ou écriture ;
- base inchangée ;
- erreur exploitable par l’utilisateur et les logs.

Priorité : critique.

10.4. Échec pendant restauration

Injecter un échec après chaque groupe d’insertion :

- Workspace ;
- templates ;
- blocs ;
- champs ;
- records ;
- instances de blocs ;
- valeurs ;
- clients ;
- pièces jointes.

Résultat attendu :

- retour à l’état pré-restauration ;
- absence de suppression définitive ;
- restauration relançable après correction.

10.5. Restauration avec deux Workspaces

Avec le modèle actuel, tester au minimum les tables dynamiques :

1. créer des données dans A et B ;
2. sauvegarder le Workspace A ;
3. modifier les données de A et B ;
4. restaurer A ;
5. comparer les données de B.

Résultat attendu cible :

- les données de B ne changent pas.

Résultat attendu actuel :

- si la restauration est globale, le test doit échouer et être enregistré comme défaut critique ;
- ce défaut ne pourra être définitivement corrigé pour les contacts qu’après ajout de Client.WorkspaceId.


11. TESTS DE MIGRATION

11.1. Base neuve

Vérifier :

- toutes les tables attendues ;
- version de schéma correcte ;
- Workspace personnel créé une seule fois ;
- templates et records cohérents.

11.2. Base existante avant DefaultTemplateId

Créer une base sans cette colonne, appliquer la migration, puis vérifier :

- colonne présente ;
- valeur par défaut correcte ;
- données existantes intactes ;
- version enregistrée.

11.3. Base existante avec migration déjà appliquée

Relancer l’initialisation.

Résultat attendu :

- aucune erreur ;
- aucune ligne dupliquée ;
- aucune donnée modifiée inutilement ;
- migration non rejouée.

11.4. Migration interrompue

Interrompre la migration après chaque étape.

Vérifier :

- version non marquée comme terminée si la migration échoue ;
- reprise possible ;
- absence de schéma hybride non supporté.

11.5. Schéma futur inconnu

Créer une base avec une version supérieure à celle supportée.

Résultat attendu :

- démarrage refusé proprement ;
- message diagnostiquable ;
- aucune tentative destructive de rétrogradation.


12. MATRICE DE PRIORITÉ

CRITIQUE :

- lecture croisée d’un Workspace ;
- écriture sur un record étranger ;
- restauration d’archive invalide ;
- échec pendant restauration ;
- perte lors de changement de template ;
- suppression de champ avec valeurs existantes ;
- rollback incomplet.

HAUTE :

- filtrage incorrect des templates ;
- doublons de FieldValue ;
- migration rejouée ;
- archive partielle annoncée comme valide ;
- pièces jointes non restaurées ;
- annulation ignorée.

MOYENNE :

- performance de chargement ;
- propagation incomplète du CancellationToken ;
- format legacy non signalé ;
- message d’erreur insuffisamment explicite.


13. CRITÈRES DE SORTIE

La validation ne pourra être déclarée réussie que si :

- tous les tests critiques d’isolation dynamique passent ;
- aucun test critique de conservation des données n’échoue ;
- les archives invalides sont refusées sans modifier la base ;
- les restaurations échouées restaurent l’état précédent ;
- les migrations sont rejouables et vérifiables ;
- les tests contacts sont soit passants après ajout de WorkspaceId, soit explicitement bloqués avec un défaut documenté ;
- les résultats sont reproductibles sur une base temporaire ;
- chaque échec indique le scénario, la preuve, le fichier concerné et la priorité.

Une compilation réussie ne suffit pas pour déclarer cette mission validée.
