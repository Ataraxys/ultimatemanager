# Mission 1 — Sécurisation SQLite, migrations et sauvegardes

## Statut

**Planifiée — non implémentée.** Ce document ne constitue pas un compte rendu d’implémentation et aucune modification du code n’est incluse dans cette mission documentaire.

## Objectif

Sécuriser la persistance locale sans perte de données métier, en traitant ensemble la propriété de la connexion SQLite, l’initialisation, les migrations, les transactions, la concurrence et la restauration des pièces jointes.

## Hors périmètre

- isolation des contacts par Workspace : [Mission 2](../02-workspace-isolation/README.md) ;
- refonte complète du moteur de formulaires : [Mission 3](../03-dynamic-forms/README.md) ;
- création immédiate des projets Domain/Application/Infrastructure ;
- modification de l’interface ou des dépendances NuGet.

## État observé

- plusieurs services créent une connexion vers le même fichier SQLite ;
- l’initialisation et les migrations sont dispersées ;
- certaines opérations multi-écritures ne sont pas transactionnelles ;
- la restauration complète peut supprimer globalement des données ;
- SQLite et les fichiers physiques ne peuvent pas être rendus atomiques par une transaction SQLite seule ;
- les tests automatisés exécutables ne sont pas encore disponibles.

## Résultats attendus

1. Un composant unique possède l’ouverture de la base, la configuration, l’initialisation et les migrations.
2. L’initialisation est idempotente et exécutée une seule fois par base et processus, avec protection contre les appels concurrents.
3. Les écritures concurrentes sont sérialisées selon une politique documentée ; les lectures et écritures ne partagent pas des connexions non maîtrisées.
4. Les opérations multi-tables sont transactionnelles et vérifient les invariants avant validation.
5. Toute suppression métier est précédée d’une règle de conservation explicite ; une erreur de restauration n’autorise pas une suppression automatique.
6. Les pièces jointes sont restaurées dans un staging temporaire, avec manifeste et journal de reprise, puis finalisées sans supprimer prématurément les anciennes copies.
7. Une interruption entre SQLite et le système de fichiers laisse une opération reprenable et permet un retour arrière documenté.
8. Les tests produisent des résultats classés **Passant**, **Échouant** ou **Bloqué**, avec cause et preuve.

## Plan d’exécution

### Étape 1 — Contrat d’infrastructure

- définir le propriétaire de la connexion et de la durée de vie ;
- définir le verrou de concurrence applicable aux écritures ;
- définir le point d’entrée unique de l’initialisation ;
- définir le format et la progression des migrations ;
- définir les chemins injectables de base, staging, sauvegarde et pièces jointes.

**Sortie :** contrat validé avant toute implémentation.

### Étape 2 — Initialisation et migrations

- créer un seul pipeline d’initialisation idempotent ;
- journaliser la version de schéma ;
- rendre chaque migration vérifiable et rejouable ;
- ne pas ignorer silencieusement les erreurs de migration ;
- tester deux appels concurrents d’initialisation.

### Étape 3 — Transactions et conservation métier

- identifier chaque cas d’utilisation multi-écritures ;
- utiliser une transaction pour les écritures SQLite atomiques ;
- vérifier l’existence et la cohérence des données avant suppression ;
- conserver les données métier et les anciennes versions tant qu’une politique de rétention n’est pas validée ;
- tester rollback, commit et reprise après erreur.

### Étape 4 — Restauration réversible des pièces jointes

La restauration doit être en deux phases :

1. **Préparation** : valider l’archive, écrire les pièces jointes dans un staging hors de l’emplacement actif, calculer les empreintes, vérifier le manifeste et préparer les écritures SQLite.
2. **Finalisation** : valider la transaction SQLite, basculer les fichiers préparés de manière contrôlée, conserver l’ancien emplacement tant que le journal n’est pas marqué terminé.

Le journal doit contenir l’identifiant d’opération, les étapes réalisées, les fichiers attendus, les fichiers finalisés et les erreurs. Une reprise doit pouvoir terminer ou annuler l’opération sans supprimer les anciennes pièces jointes. Le nettoyage est différé et soumis à vérification.

### Étape 5 — Concurrence et récupération

- tester plusieurs opérations de lecture/écriture simultanées ;
- tester deux restaurations simultanées et définir le comportement attendu ;
- tester un crash simulé à chaque frontière entre staging, transaction et finalisation ;
- vérifier l’absence de verrou SQLite laissé ouvert ;
- vérifier qu’une opération interrompue est détectée et reprise ou annulée.

### Étape 6 — Validation et livraison

- exécuter le [plan de tests](TEST_PLAN.md) ;
- renseigner le [rapport d’implémentation](IMPLEMENTATION_REPORT.md) uniquement après exécution réelle ;
- reporter séparément les tests passants, échouants et bloqués ;
- mettre à jour l’état global sans transformer un plan en fonctionnalité disponible.

## Critères d’acceptation

- aucun scénario validé ne perd de donnée métier ;
- une initialisation concurrente ne crée pas de schéma partiellement initialisé ;
- les transactions SQLite échouées sont annulées ;
- les fichiers existants sont conservés jusqu’à confirmation de la restauration ;
- une restauration interrompue est détectable, reprenable et réversible ;
- chaque test possède un statut et une preuve reproductible ;
- les tests bloqués indiquent explicitement leur prérequis, sans être comptés comme passants.

## Références

- [Conception SQLite](../../01-architecture/DATABASE_DESIGN.md)
- [Architecture](../../01-architecture/ARCHITECTURE.md)
- [Décisions](../../01-architecture/DECISIONS.md)
- [Roadmap](../../02-roadmap/ROADMAP.md)
- [État d’implémentation](../../02-roadmap/IMPLEMENTATION_STATUS.md)
