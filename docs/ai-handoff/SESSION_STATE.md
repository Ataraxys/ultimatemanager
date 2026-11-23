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

### Completed and reviewed

- Step 1: SQLite API/design verification completed, with journal-mode probe limitation documented.
- Step 2 / 2B / 2C: central `ISqliteDatabase` contract and `SQLiteDatabaseCoordinator` implemented and independently reviewed. Status: VALIDÉ SOUS RÉSERVES.
- Step 3: `SqliteClientService` migrated to `ISqliteDatabase`; CRUD/search and JSON backup triggers preserved. Harness and Android Debug build passed. Independent QA status: VALIDÉ SOUS RÉSERVES.

### Accepted reservations

- JSON backup generation can race under concurrent client writes; ordering/serialization needs a dedicated design.
- Historical-data test is not a fixture from a real prior schema.
- Initialization uniqueness is not directly instrumented.
- Dynamic-table initialization is not covered by end-to-end fresh-install integration tests.
- Real backup file failures are not injected.
- `SqliteDynamicFormService`, `LocalWorkspaceContext`, and `CompleteBackupService` still have independent SQLite connections; global centralization is not complete.
- Coordinator CloseAsync / ExitLock failure injection remains untested.

## Step 4A — completed and reviewed

- Codex completed the read-only architecture audit of `SqliteDynamicFormService` and `LocalWorkspaceContext`.
- ChatGPT retrieved the report from `ai-handoff/report-MISSION-1-STEP-4A`, independently checked the relevant source files on `feature/add-personalisation`, and recorded the decision **VALIDÉ SOUS RÉSERVES**.
- Independent review confirmed the separate SQLite connections, constructor-started initialization, non-transactional multi-write operations, destructive ordering in `GetOrCreateRecordAsync`, broad swallowed migration exceptions, global destructive restore statements in `CompleteBackupService`, and orphaned field values after definition deletion.
- Review record: `docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4A-CHATGPT-REVIEW.md`.
- No implementation is authorized by this review.

## Current next step

Mission 1 — Step 4B: read-only detailed design of the SQLite migration and hardening plan.

- Mission brief: `docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4B-IMPLEMENTATION-DESIGN.md`.
- Dependency Step 4A review is satisfied by the explicit ChatGPT review record above.
- Scope: target architecture, migration sequencing for `LocalWorkspaceContext`, `SqliteDynamicFormService`, and `CompleteBackupService`; versioned schema migrations; atomicity; backup/restore safety; and test design.
- No code changes, database migrations, or real user database access are authorized during Step 4B.
- Implementation requires a separate explicit approval after Step 4B is reviewed.

## Guardrails

- Never use or modify the user's real SQLite database during tests.
- Do not start implementation until the analysis is reviewed and explicitly approved.
- No Workspace isolation changes in Mission 1.
- No dynamic-engine redesign in the SQLite-centralization mission.
- Preserve the exact UI rule: “Modifier” edits content only; pencil “✎” edits the full structure/template.
- Do not claim that local uncommitted changes are visible through GitHub.


## Cloud orchestration rollout

- On 2026-11-23, the user selected fully cloud-based orchestration.
- Infrastructure branch: `feature/cloud-ai-orchestration`.
- The branch contains the GitHub Actions orchestrator, executor/reviewer instructions, architecture and setup documentation, and the updated handoff protocol.
- The workflow is not active until its PR is reviewed/merged into `develop` and the repository Actions secret `OPENAI_API_KEY` is added.
- First activation must be a harmless read-only smoke test. Cloud MAUI build/test coverage remains a separate follow-up after inspecting the solution and test projects.
