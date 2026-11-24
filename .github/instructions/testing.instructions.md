---
applyTo: "**/*Tests/**/*.cs,**/*.Tests.cs,**/test/**/*.cs"
---

# Règles de test

- Tester les invariants métier et les cas d’utilisation sans dépendre de MAUI lorsque cela n’est pas nécessaire.
- Tester explicitement l’isolation entre Workspaces.
- Couvrir les migrations sur une base représentative des données existantes.
- Vérifier les transactions, annulations, erreurs et reprises après échec.
- Tester séparément la structure des templates et les valeurs des fiches.
- Ajouter un scénario de non-régression pour chaque perte de données potentielle identifiée dans `docs/01-architecture/ARCHITECTURE.md` et `docs/01-architecture/DATABASE_DESIGN.md`.
- Pour la Mission 1, reporter séparément les tests passants, échouants et bloqués dans `docs/03-missions/01-sqlite-security/TEST_PLAN.md` et `IMPLEMENTATION_REPORT.md`.
- Ne pas considérer une simple compilation comme une preuve de conformité fonctionnelle.
