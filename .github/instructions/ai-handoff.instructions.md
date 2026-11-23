---
applyTo: "**"
---

# Protocole de collaboration IA — UltimateManager

Ce dépôt est partagé entre ChatGPT (pilotage produit, architecture et validation), Codex (missions transversales et agents spécialisés) et GitHub Copilot (assistance au développement dans l'IDE).

## Avant toute mission
1. Lire `docs/ai-handoff/SESSION_STATE.md`.
2. Lire les consignes pertinentes dans `docs/ai-handoff/outbox/`.
3. Lire les instructions du rôle/agent concerné.
4. Vérifier la branche, le statut Git et les changements locaux avant toute modification.
5. Ne jamais écraser les modifications locales non commitées de l'utilisateur.

## Pendant la mission
- Respecter strictement le périmètre autorisé.
- Ne pas élargir la mission sans le signaler et obtenir l'accord nécessaire.
- Ne pas modifier les mêmes fichiers en parallèle avec un autre agent.
- Préserver les données et la compatibilité existantes.
- Ne jamais utiliser la vraie base SQLite de l'utilisateur pour les tests.
- Ne jamais exécuter de commande Git destructive sans autorisation explicite.

## Rapport obligatoire
À la fin, créer un nouveau fichier unique dans `docs/ai-handoff/inbox/` avec l'extension `.txt`, en suivant `docs/ai-handoff/AGENT_REPORT_TEMPLATE.txt`.

Le rapport doit être complet et contenir au minimum :
- statut et résumé ;
- périmètre autorisé et écarts ;
- faits observés, hypothèses et décisions ;
- fichiers créés, modifiés, supprimés et inspectés ;
- tests PASS / FAIL / NOT TESTED, commandes et résultats ;
- résultat du build ;
- risques, blocages et impacts ;
- instructions de reproductibilité ;
- recommandation de prochaine étape.

**Obligation :** inclure le rapport complet directement dans la réponse finale de l'agent, en Markdown prêt à copier-coller. Un rapport enregistré uniquement dans un fichier est insuffisant.

Ne jamais inventer un résultat de test ou de build. Ne jamais écraser un rapport antérieur. Ne pas démarrer l'étape suivante sans validation explicite lorsque celle-ci est requise.
