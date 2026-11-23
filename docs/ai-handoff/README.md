# AI Handoff — ChatGPT × Codex

This folder is the shared, Git-backed handoff point between ChatGPT (product/architecture/QA direction) and Codex/GitHub Copilot (repository execution).

## Branch model — mandatory

- `develop` is the canonical application integration branch. It must contain the latest accepted, buildable application state, not merely agent reports or planning documents.
- `feature/*` branches are temporary implementation branches. Every mission starts from the current `develop` unless its brief explicitly names another approved base.
- A feature branch is not considered integrated or globally complete until its changes are reviewed, tested, and merged into `develop`.
- `ai-handoff/report-*` branches are report-only. They must never contain application code and must never be used as a base for implementation.
- Do not start a dependent implementation mission while its prerequisites exist only on an unmerged feature branch.
- Before each mission, record the exact base SHA and branch. Before integration, refresh refs and compare the exact current SHAs.

## Standard implementation cycle

1. ChatGPT writes a uniquely named mission in `outbox/` and identifies the required base branch/SHA policy.
2. Codex fetches the latest remote refs and verifies that the required prerequisites exist on the named base. If not, stop and report the mismatch; do not improvise by switching bases or copying files.
3. Codex creates a dedicated `feature/<mission>` branch from the current approved `develop` SHA (unless the brief explicitly authorizes a different base).
4. Codex implements only the mission scope, tests using isolated temporary data, and records exact commands/results.
5. Codex publishes the feature branch and opens a PR targeting `develop` only when the brief authorizes PR creation. No automatic merge.
6. ChatGPT reviews the report and relevant diff. The user approves integration. After merge, verify the resulting `develop` SHA and confirm prerequisites are actually present there.
7. Only then may the next dependent mission begin.

## Reporting cycle

1. ChatGPT writes mission/decision files in `outbox/`.
2. Codex reads the applicable brief and repository instructions before acting.
3. Codex publishes a complete Markdown-formatted `.txt` report under `inbox/` on a dedicated `ai-handoff/report-*` branch based on current `develop`.
4. The report branch must contain only handoff/report files. Never put product code on it.
5. Codex includes the complete report in its final response, directly copyable into the conversation.
6. ChatGPT reviews the report and updates `SESSION_STATE.md` and/or issues the next mission.
7. Reports are evidence, not approvals. A report's existence does not authorize implementation, integration, or merge.

## Integration of existing divergent branches

If an existing feature branch is significantly behind/diverged from `develop`, do not blindly merge it and do not continue stacking dependent missions on it. First run a read-only integration audit. Then choose an explicit strategy: merge as a whole after corrections, split into smaller coherent PRs, or rebuild selected changes in controlled batches from `develop`. The choice must be documented before any rebase, cherry-pick, merge, or code transfer.

## Safety and quality guardrails

- No merge, rebase, cherry-pick, force-push, reset, or destructive Git operation without explicit user approval.
- Never modify the user's real SQLite database during tests.
- Never commit secrets, tokens, passwords, private keys, customer data, or production database contents.
- Do not let multiple agents write the same handoff file concurrently.
- Do not treat static analysis as runtime proof.
- Do not claim a test/build passed without actual command output.
- Do not silently change a mission's base branch or copy partial files from another branch.
- Preserve the product rule: “Modifier” edits content only; pencil “✎” edits full structure/template.

## Shared folders

- `outbox/`: instructions and decisions written by ChatGPT.
- `inbox/`: reports and deliverables written by Codex.
- `SESSION_STATE.md`: authoritative project status and next approved work.
- `AGENT_REPORT_TEMPLATE.txt`: required report structure.

## Important limitations

This is Git-based synchronization, not a live shared folder. ChatGPT cannot see uncommitted local files. A file becomes available only after it is pushed to GitHub and the GitHub connector can read the branch.
