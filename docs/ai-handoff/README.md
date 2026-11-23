# AI Handoff — UltimateManager

This folder is the shared, Git-backed mailbox and state store for the UltimateManager AI workflow.

## Cloud-first collaboration

The intended operating mode is the GitHub-hosted Cloud AI Orchestrator:

- Launch: GitHub Actions → **UltimateManager — Cloud AI Orchestrator**.
- Executor: Codex through `openai/codex-action`, with workspace write access.
- Reviewer: a separate Codex invocation with read-only access.
- Communication: mission and reports committed as text files in this repository.
- Delivery: one isolated mission branch and a draft pull request.
- Correction loop: at most one automatic correction cycle.
- Human control: no automatic merge, deployment, production migration, or destructive operation.

Architecture and activation instructions:
- `docs/ai-handoff/cloud-orchestration/ARCHITECTURE.md`
- `docs/ai-handoff/cloud-orchestration/SETUP.md`

The workflow requires the repository Actions secret `OPENAI_API_KEY`. OpenAI API billing is separate from ChatGPT subscription billing. Never commit secrets.

## Folders

- `outbox/`: mission briefs and decisions.
- `outbox/runs/`: immutable mission briefs created for each cloud run.
- `inbox/`: Codex and reviewer reports, each with a unique run ID.
- `SESSION_STATE.md`: compact source of truth for project state and next approved work.
- `AGENT_REPORT_TEMPLATE.txt`: required report structure for manual/fallback agent runs.

## Cloud protocol

1. A user with repository write access launches one workflow run and supplies the mission.
2. The workflow creates a unique branch and persists the mission brief in `outbox/runs/`.
3. Codex reads the mission, session state, and repository instructions; it executes only the authorized scope.
4. The workflow saves the complete Codex final response in `inbox/`.
5. A separate read-only reviewer inspects the branch diff and surrounding source, then saves its complete report in `inbox/`.
6. If the reviewer requests changes, Codex receives that report and gets one bounded correction attempt, followed by a second review.
7. The workflow opens a draft PR containing the deliverables and reports.
8. A human reviews and merges the PR. No agent may merge or bypass protections.

## Manual fallback protocol

If the cloud workflow is unavailable, ChatGPT can still write a unique brief in `outbox/` and Codex can publish a report in `inbox/` using a dedicated report branch. The agent must include the complete report in its final response as well as in the file.

## Safety and evidence

- Git is the shared mailbox, not a live shared filesystem.
- Agents cannot see local uncommitted files or the user's local Android SDK/device.
- Never commit secrets, tokens, passwords, private keys, customer data, or production database contents.
- Do not use the user's real SQLite database for tests.
- Distinguish facts, assumptions, and unverified claims.
- Tests/builds must be marked PASS / FAIL / NOT TESTED with exact commands and observed evidence.
- Never start an implementation step that has not been explicitly authorized.
- Never merge, deploy, run production migrations, or perform destructive data operations automatically.
