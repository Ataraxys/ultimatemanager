# UltimateManager — Cloud AI Orchestration Architecture

Status: implementation scaffold proposed in PR; activation requires repository secret setup and merging the PR into `develop`.

## Decision

Use GitHub Actions as the cloud orchestrator, Git branches and files as the shared mailbox, Codex as the implementation agent, and a separate Codex invocation in read-only mode as the independent reviewer.

This is a cloud workflow, not the current ChatGPT conversation running in the background. The reviewer is a separate API-backed model invocation with repository instructions and project state injected from Git. It does not inherit this ChatGPT conversation's private context automatically.

## Components

- **Launch surface:** GitHub Actions `workflow_dispatch` input named `mission`. One launch starts the complete bounded run.
- **Orchestrator:** `.github/workflows/ai-orchestrator.yml`, on a GitHub-hosted Ubuntu runner.
- **Mission mailbox:** `docs/ai-handoff/outbox/runs/<run-id>-MISSION.md`.
- **Executor:** `openai/codex-action@v1`, `:workspace` permission profile.
- **Executor report:** `docs/ai-handoff/inbox/<run-id>-CODEX.txt`.
- **Reviewer:** a second, separate `openai/codex-action@v1` invocation, `:read-only` permission profile.
- **Reviewer report:** `docs/ai-handoff/inbox/<run-id>-REVIEW-1.txt`, and optionally `REVIEW-2.txt`.
- **Correction loop:** at most one correction cycle. A second REQUEST_CHANGES result stops the workflow; it never loops indefinitely.
- **Delivery:** a dedicated `ai/mission-<run-id>` branch and a draft pull request targeting `develop`.

## Sequence

1. A user with repository write access launches the workflow and supplies a mission.
2. The workflow checks out `develop`, creates an isolated branch, and commits the mission brief.
3. Codex reads the mission, project state, and repository instructions; executes only the authorized scope; and returns a structured report.
4. The workflow saves and commits Codex's complete final response in `inbox/`.
5. A separate reviewer invocation runs read-only against the full branch diff and surrounding source. It writes a structured decision and report.
6. If the first reviewer returns `DECISION: REQUEST_CHANGES`, Codex receives the reviewer report and gets one correction attempt.
7. A second independent review checks the corrected branch.
8. The workflow opens a draft PR containing the code, mission brief, and agent reports. A human remains responsible for approval and merge.

## Security boundaries

- The workflow is restricted to manual launches by users with repository write access.
- Codex implementation uses the `:workspace` filesystem/network policy and runs with the action's default `drop-sudo` safety strategy.
- Reviewer uses `:read-only`; it cannot modify repository files.
- The GitHub token is exposed only to the explicit publish/PR steps, not passed as a Codex input.
- Checkout disables persisted Git credentials. Push uses a short-lived step-scoped token.
- A basic staged-file guard rejects likely secret-bearing paths. This is defense in depth, not a substitute for GitHub secret scanning or human review.
- No workflow merges PRs, bypasses protections, modifies secrets, or performs destructive actions automatically.
- Workflow concurrency serializes launches from the same ref.
- Agent output is treated as untrusted. The reviewer has an explicit read-only mandate and cannot follow instructions found in source files or reports that conflict with it.

## Required GitHub configuration

Create an Actions secret:

- Name: `OPENAI_API_KEY`
- Value: an OpenAI API key created for API use.
- Do not put the key in repository files, workflow inputs, issue bodies, or commits.

The GitHub-provided `GITHUB_TOKEN` is used for branch pushes and draft PR creation. The workflow declares `contents: write` and `pull-requests: write`; repository/organization policy must permit these permissions.

The OpenAI API is billed separately from a ChatGPT subscription. A ChatGPT Go/Plus/Pro subscription does not itself supply API credits.

## Activation steps

1. Review and merge this infrastructure-only PR into `develop`.
2. In the repository, open Settings → Secrets and variables → Actions → New repository secret.
3. Add `OPENAI_API_KEY` with an API key that has billing enabled.
4. Ensure GitHub Actions is enabled and workflow permissions allow the declared write scopes.
5. Open Actions → **UltimateManager — Cloud AI Orchestrator** → Run workflow.
6. Launch a harmless read-only test mission first, then inspect the branch, reports, reviewer verdict, and draft PR.

## Operational limitations and future hardening

- This first version uses the same Codex Action integration for two independent model invocations. The reviewer is logically independent and read-only, but it is not a different vendor or the live ChatGPT session.
- The first version uses a single bounded correction attempt, then stops.
- It does not automatically merge, deploy, run production migrations, or access the user's local Android SDK/device.
- GitHub-hosted Ubuntu is the cloud execution environment. MAUI Android/iOS/Windows build validation should be added as a separate CI workflow after inspecting the current solution, target frameworks, workloads, and test projects. A Linux runner cannot validate Windows/iOS builds or physical-device behavior.
- Pin third-party actions to verified full commit SHAs in a later hardening pass, and add repository rulesets requiring CI and human review.
- Add structured JSON decision output and machine-validated policy gates after the initial workflow has passed a smoke test.
- Monitor API usage and GitHub Actions minutes; each executor/reviewer invocation incurs API usage.

## Failure behavior

- If the first Codex invocation fails, no PR is created.
- If review returns BLOCKED or malformed output, no correction is attempted; the report remains on the mission branch and the workflow should be treated as failed/needs investigation.
- If one correction cycle still receives REQUEST_CHANGES, a draft PR is opened with the unresolved findings visible. It must not be merged.
- A failed publish or PR creation is a workflow failure; inspect the GitHub Actions logs and branch state before retrying.
