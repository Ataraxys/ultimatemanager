# Universal AI Agent Protocol

**Applies to every AI agent working on UltimateManager** — including GitHub Copilot, Codex, ChatGPT-connected agents, and any future agent or automation. This protocol applies to every task: implementation, bug fix, compile fix, refactoring, audit, test, documentation, and architecture work.

The mission brief may impose stricter rules. It may narrow this protocol for a specific task, but an agent must not silently ignore or weaken it.

## 1. Before doing any work

1. Read this file and `README.md`.
2. Read `SESSION_STATE.md` and the exact mission/decision documents relevant to the task.
3. Inspect the current Git status, branch, remote refs, and current commit. Preserve pre-existing user changes.
4. Confirm the authorized branch, base SHA, task scope, and prohibited actions.
5. If the required base/prerequisites are missing, the task conflicts with repository state, or the scope is ambiguous, stop and report the blocker. Do not improvise.

## 2. While working

1. Stay within the explicit task scope. Do not bundle unrelated cleanup, broad refactoring, dependency upgrades, formatting sweeps, or architectural redesign.
2. Preserve existing behavior and data. Never use the user's real SQLite database for tests; use isolated temporary databases and fixtures.
3. Follow the project's architecture decisions and existing conventions. If a necessary design decision is missing, explain the options and ask rather than silently inventing a new architecture.
4. Do not perform merge, rebase, cherry-pick, reset, force-push, branch deletion, or other destructive Git operations without explicit user approval.
5. Do not change `develop` or `master` unless the mission explicitly authorizes it.
6. Never commit secrets, credentials, private keys, personal/customer data, or production database contents.

## 3. Verification and evidence

1. Run the relevant build, tests, and checks that are feasible within the task.
2. Record the exact commands, environment/configuration that materially affects results, and actual outcomes.
3. Distinguish clearly between PASS, FAIL, BLOCKED, and NOT RUN.
4. Do not claim success based on code inspection, expected behavior, static analysis, or another agent's statement alone.
5. If a check cannot be run, explain why and what remains unverified.

## 4. Mandatory GitHub handoff report — every task

Every agent must publish a report even for a small fix, a compile-only correction, a read-only audit, or a task that ends blocked. No task is exempt.

1. Commit application/code changes on the authorized implementation branch when the task permits code changes. Do not put application code on a report-only branch.
2. Create a unique report file under `docs/ai-handoff/inbox/`, using the naming convention `YYYYMMDD-agent-short-description.txt`.
3. Publish the report on a dedicated `ai-handoff/report-<short-description>` branch based on the current approved `develop`, following the reporting cycle in `README.md`. The report branch must contain handoff files only.
4. Include the exact implementation branch, base SHA, and code commit SHA. If there is no code commit (for example, a read-only audit or blocked task), state that explicitly and identify the inspected SHA.
5. Use `AGENT_REPORT_TEMPLATE.txt` and complete every relevant section. Mark inapplicable sections as N/A with a reason; do not silently omit them.
6. Include the complete report in the agent's final response as well as publishing it to GitHub.
7. Never overwrite an earlier report. One report per task; use a new unique filename.
8. If GitHub publication fails or is unavailable, state that clearly in the final response and provide the complete report for manual publication. Do not pretend the report was published.

The report must include:
- Agent identity/name and task identifier.
- Objective and authorized scope.
- Starting branch and exact base/starting SHA.
- Final implementation branch and exact code commit SHA, if any.
- Diagnosis and concrete changes, including file paths.
- Exact verification commands and PASS/FAIL/BLOCKED/NOT RUN results.
- Risks, regressions considered, data/schema/migration implications.
- Work deliberately not done and remaining blockers/decisions.
- Recommended next step, without assuming approval.

## 5. Final response

The agent's final response must be concise but complete:
- What was done (or why it was blocked).
- Exact branch and commit SHA.
- Verification summary with honest status.
- Report path and report branch, plus publication status.
- Remaining risks and the next recommended action.

A report is evidence for independent review, not approval. Publishing a report, passing tests, or opening a PR does not authorize integration or merge.

## 6. No silent handoff gaps

If the task spans multiple agents, each agent must publish its own report for its own work and reference predecessor reports/commits. The coordinating agent must produce a final consolidated report identifying all contributing agents and exact commits. Do not assume another agent will document your work.
