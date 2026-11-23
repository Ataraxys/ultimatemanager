# Copilot Instructions

## Directives de projet
- Préférence d’architecture : commencer avec OpenStreetMap pour la carte, permettre à l’utilisateur de toucher un point afin de préremplir latitude/longitude, et isoler le fournisseur cartographique derrière une abstraction facilement remplaçable.

## Collaboration avec ChatGPT et Codex

Le dépôt utilise un protocole partagé dans `docs/ai-handoff/`.

Avant une mission :
- Lire `docs/ai-handoff/SESSION_STATE.md`.
- Lire les consignes applicables dans `docs/ai-handoff/outbox/`.
- Respecter les décisions d'architecture et le périmètre explicitement autorisé.

À la fin d'une mission :
- Déposer un rapport complet et unique dans `docs/ai-handoff/inbox/`, au format `.txt`.
- Suivre `docs/ai-handoff/AGENT_REPORT_TEMPLATE.txt`.
- Inclure le rapport complet directement dans la réponse finale de l'agent, prêt à être copié-collé.
- Distinguer les faits vérifiés, les hypothèses et les éléments non testés.
- Indiquer les tests PASS / FAIL / NOT TESTED, les commandes exécutées et les résultats réels.
- Ne jamais écraser un ancien rapport.

Git et sécurité :
- Ne jamais écraser, supprimer ou réinitialiser des modifications locales de l'utilisateur.
- Ne jamais lancer de commande destructive (clean, reset --hard, checkout destructif) sans autorisation explicite.
- Ne pas pousser de secrets, tokens, données personnelles, fichiers de base de données réels ou artefacts sensibles.
- Ne pas modifier simultanément les mêmes fichiers qu'un autre agent ou développeur.
- Ne pas commencer une étape suivante sans validation explicite lorsque la mission le demande.

Les consignes propres à chaque agent complètent ces règles générales ; en cas de conflit, signaler le conflit au lieu de choisir silencieusement.
