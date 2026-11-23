# UltimateManager — AI Session State

Last updated: 2026-11-23

## Collaboration model

- ChatGPT acts as product/technical cofounder: roadmap, architecture, technical arbitration, independent review, and approval of next steps.
- Codex / GitHub Copilot agents execute repository analysis, implementation, and QA within explicitly assigned scopes.
- Shared handoff location: `docs/ai-handoff/`.
- ChatGPT → Codex: `docs/ai-handoff/outbox/`.
- Codex → ChatGPT: `docs/ai-handoff/inbox/`.
- All completed agent reports must be saved as unique `.txt` files and also included in the agent's final response.

## Product and architecture direction

- UltimateManager is intended to evolve into a polished, extensible business-management platform with eventual SaaS/commercial potential.
- Build local-first with SQLite now; do not introduce cloud sync prematurely.
- Keep standard business entities strongly typed (for example Contact, Task, Project, Event, Document).
- Use a dynamic object/field engine for user-defined object types and custom fields; do not turn every business entity into a generic ObjectRecord.
- Workspaces are a central future boundary; groups, users, contacts, and permissions will evolve around them.
- Prefer clean, maintainable object-oriented architecture without premature project splitting or overengineering.
- Preserve existing functionality and user data; avoid destructive schema or restore operations.
- Record architectural decisions in `docs/01-architecture/DECISIONS.md` and update affected documentation.

## Mission 1 — SQLite security, migrations and backups

### Completed and reviewed (branch-specific)

- Step 1: SQLite API/design verification completed, with journal-mode probe limitation documented.
- Steps 2 / 2B / 2C: central `ISqliteDatabase` contract and `SQLiteDatabaseCoordinator` implemented and independently reviewed on `feature/add-personalisation`; they are **not present on develop**.
- Step 3: `SqliteClientService` migrated to `ISqliteDatabase` on `feature/add-personalisation`; harness and Android Debug build were reported as passed on that branch. The implementation is **not present on develop**.
- Step 4A: read-only audit completed and reviewed on `feature/add-personalisation`; findings confirmed, with template-list behavior reserved for product arbitration.
- Step 4B: read-only detailed design completed and reviewed. Status: **VALIDÉ SOUS RÉSERVES**. Review: `docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4B-CHATGPT-REVIEW.md`.
- Step 4C: implementation **BLOCKED before coding** because its required base `develop` does not contain the Step 2/3 prerequisites. Codex correctly made no application changes. Review: `docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4C-CHATGPT-REVIEW.md`.

### Current Git reality

- `develop` currently does not contain `ISqliteDatabase`, `SQLiteDatabaseCoordinator`, `LocalWorkspaceContext`, or `SqliteDynamicFormService`.
- `feature/add-personalisation` contains these files but is significantly divergent and includes broad UI, dynamic-engine, backup, test, and documentation changes.
- Latest reviewed comparison at Step 4C review: feature branch 3 commits ahead and 12 behind `develop`; diff spans many domains.
- PR #3 must not be merged solely to unblock Step 4C. It requires a full integration audit first.
- The earlier session status overstated repository-wide completion by not distinguishing branch-local work from `develop`. This correction is authoritative.

### Next mission — integration audit of PR #3

- Mission: `MISSION-1-INTEGRATION-AUDIT-PR3`.
- Brief: `docs/ai-handoff/outbox/20261123_MISSION-1-INTEGRATION-AUDIT-PR3.md`.
- Scope: strictly read-only audit of PR #3 / `feature/add-personalisation` against current `develop`; inventory changes, verify SQLite and UI requirements, inspect tests, identify divergence/conflicts, and propose a safe integration plan.
- No merge, rebase, cherry-pick, branch update, code change, or push of application code is authorized.
- Required report: complete in Codex final response and unique `.txt` under `docs/ai-handoff/inbox/`, published on a report-only branch.

### Step 4C — paused pending integration decision

- Do not execute the previous Step 4C brief until the integration audit determines the correct application base and ChatGPT issues a revised brief.
- After integration, reissue Step 4C against the accepted branch/commit, with the Step 4B architectural decisions unchanged unless explicitly revised.

### Step 4D — pending: backup and restore safety

- Separate mission after Step 4C review.
- Must decide and implement explicit Workspace vs full-application archive scopes.
- ZIP v1 must not be silently restored while its scope is ambiguous.
- No Workspace restore may delete global Clients or rows belonging to another Workspace.
- Add full archive validation, staging, failure injection, and recovery journal/marker before authorizing restore implementation.

## Accepted reservations

- JSON backup generation can race under concurrent client writes; ordering/serialization needs a dedicated design.
- Historical-data test is not a fixture from a real prior schema.
- Initialization uniqueness is not directly instrumented.
- Dynamic-table initialization is not covered by end-to-end fresh-install integration tests.
- Real backup file failures are not injected.
- `CompleteBackupService` still has a private SQLite connection and a global destructive restore path on the feature branch; it remains a high-priority unresolved risk.
- Coordinator CloseAsync / ExitLock failure injection remains untested.

## Guardrails

- Never use or modify the user's real SQLite database during tests.
- No destructive migration or restore without explicit approved policy.
- No Workspace isolation changes in Mission 1.
- No dynamic-engine redesign in the SQLite-centralization mission.
- Preserve the exact UI rule: “Modifier” edits content only; pencil “✎” edits the full structure/template.
- Do not claim that local uncommitted changes are visible through GitHub.
- Cloud orchestration PR #4 is parked; continue the normal manual `@execute` / `@review` workflow.
