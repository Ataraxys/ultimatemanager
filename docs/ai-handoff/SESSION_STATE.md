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
- Step 4A: read-only audit completed and reviewed; findings confirmed, with template-list behavior reserved for product arbitration.
- Step 4B: read-only detailed design completed and reviewed. Status: **VALIDÉ SOUS RÉSERVES**. Review: `docs/ai-handoff/outbox/20261123_MISSION-1-STEP-4B-CHATGPT-REVIEW.md`.

### Accepted reservations

- JSON backup generation can race under concurrent client writes; ordering/serialization needs a dedicated design.
- Historical-data test is not a fixture from a real prior schema.
- Initialization uniqueness is not directly instrumented.
- Dynamic-table initialization is not covered by end-to-end fresh-install integration tests.
- Real backup file failures are not injected.
- `CompleteBackupService` still has a private SQLite connection and a global destructive restore path. It is explicitly excluded from Step 4C and remains a high-priority unresolved risk.
- Coordinator CloseAsync / ExitLock failure injection remains untested.

### Step 4C — next: Workspace and DynamicForm SQLite hardening

- Step 4C is authorized only within the detailed scope of the Step 4B ChatGPT review.
- Migrate `LocalWorkspaceContext` and `SqliteDynamicFormService` to `ISqliteDatabase`; remove private connections and constructor-started DDL.
- Add centralized `PRAGMA user_version` migration runner, fail-closed behavior for unknown schemas, and transactional multi-write operations.
- Preserve old records and values on template mismatch or failure; no implicit destructive replacement.
- Do not change Workspace isolation, Client schema, dynamic engine design, or UI.
- Do not modify `CompleteBackupService`, ZIP backups, JSON import/export, or attachment restore in Step 4C.
- Required review: complete report in Codex final response and unique `.txt` under `docs/ai-handoff/inbox/`, published on a dedicated report-only branch.

### Step 4D — pending: backup and restore safety

- Separate mission after Step 4C review.
- Must decide and implement explicit Workspace vs full-application archive scopes.
- ZIP v1 must not be silently restored while its scope is ambiguous.
- No Workspace restore may delete global Clients or rows belonging to another Workspace.
- Add full archive validation, staging, failure injection, and recovery journal/marker before authorizing restore implementation.

## Guardrails

- Never use or modify the user's real SQLite database during tests.
- No destructive migration or restore without explicit approved policy.
- No Workspace isolation changes in Mission 1.
- No dynamic-engine redesign in the SQLite-centralization mission.
- Preserve the exact UI rule: “Modifier” edits content only; pencil “✎” edits the full structure/template.
- Do not claim that local uncommitted changes are visible through GitHub.
- Cloud orchestration PR #4 is parked; continue the normal manual `@execute` / `@review` workflow.
