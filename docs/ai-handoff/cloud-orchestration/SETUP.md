# Activation checklist — Cloud AI Orchestrator

This setup is intentionally one-time. Do not commit an API key.

## 1. Merge the infrastructure PR

The workflow is not available in the Actions tab until its file is merged into the repository's default branch (`develop`). Review the PR first; it contains orchestration files and documentation only, not UltimateManager application code.

## 2. Create an OpenAI API key

1. Open https://platform.openai.com/api-keys.
2. Create a project API key for UltimateManager automation.
3. Ensure API billing/credits are configured.
4. Store the key in a password manager until adding it to GitHub.

API usage is billed separately from ChatGPT subscription usage.

## 3. Add the GitHub Actions secret

In `Ataraxys/ultimatemanager`:

1. Settings → Secrets and variables → Actions.
2. Select **New repository secret**.
3. Name: `OPENAI_API_KEY`.
4. Secret: paste the API key.
5. Save.

Never add the key to a workflow input, file, commit, issue, PR comment, or agent report.

## 4. Check Actions permissions

- Confirm GitHub Actions is enabled for the repository.
- Confirm the repository/organization policy permits the workflow's declared `contents: write` and `pull-requests: write` permissions for `GITHUB_TOKEN`.
- Do not grant broader permissions than required.

## 5. Run a smoke test

Open Actions → **UltimateManager — Cloud AI Orchestrator** → Run workflow.

Suggested first mission:

> Read-only smoke test. Inspect the repository instructions, docs/ai-handoff/SESSION_STATE.md, and current solution/project layout. Do not modify application code, configuration, workflows, or existing documentation. Return a structured report with repository facts, available build/test projects, and cloud validation limitations. The reviewer must independently verify the report. This mission is diagnostic only.

Expected result:
- One branch named `ai/mission-<run-id>`.
- A Codex report and reviewer report in `docs/ai-handoff/inbox/`.
- A draft PR targeting `develop`.
- No application code changes in the smoke-test PR.

Close the smoke-test PR after reviewing it if it contains no useful documentation deliverable.

## 6. Launch real missions

Use the workflow's mission input to provide the complete objective, exact scope, constraints, and acceptance criteria. For risky work (database migrations, data deletion, security changes, release/deployment), explicitly require design/review first and human approval before implementation or production execution.


If a run fails after creating its mission branch, start a fresh workflow run rather than using **Re-run jobs**. The initial branch naming scheme is based on the workflow run ID; a new run receives a new ID.
