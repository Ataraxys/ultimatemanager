# UltimateManager — AI Session State

Last updated: 2026-11-24

## Current authorized task — full independent review of PR #5

- Mission published: `docs/ai-handoff/outbox/20261124_MISSION-PR5-REVUE-COMPLETE-82-FICHIERS.md`.
- Mission publication commit on `integration/reconcile-feature-branches`: `0837de814e955cc280e9741ea620537ab3def2d2`.
- Scope: strictly read-only, exhaustive review of every file changed by PR #5; no code changes, no corrective commits, no approval and no merge.
- Last GitHub PR metadata after mission publication: PR #5 open, draft, not merged; base `develop` at `47b6dbf615cd62cdb9c5c0be018dda60310c826d`; head `integration/reconcile-feature-branches` at `0837de814e955cc280e9741ea620537ab3def2d2`; 83 changed files (+9,946/-186); mergeable=false at that observation. The new outbox mission itself is committed on the PR head, so the changed-file count is now 83 rather than the previously reported 82. Re-fetch live metadata before starting.
- Previous independent corrective review is published at `docs/ai-handoff/inbox/20261124-CODEX-pr5-corrective-review.txt` on `ai-handoff/report-corrective-backup-templates`. It accepted the targeted corrective diff with environment/UI caveats, but explicitly did not approve the full PR.
- Required output: exhaustive file-by-file review matrix, evidence-backed P0–P3 findings, separate product/architecture decisions, exact verification results, and a single verdict APPROVE / REQUEST CHANGES / BLOCKED.
- Report branch must be report-only and based on the refreshed current `develop` SHA. No application code on that branch.
- Do not merge PR #5. Do not modify `develop`, the integration code, the feature source branch, or the protected local worktree. Preserve the pre-existing `UltimateManager.csproj` change on `feature/block-instances-fix`.

## Latest corrective mission — backup fail-closed and template visibility

- Mission: `docs/ai-handoff/outbox/20261124_CORRECTIVE-BACKUP-TEMPLATES.md`.
- Authorized implementation branch: `integration/reconcile-feature-branches`; base `develop` was `47b6dbf615cd62cdb9c5c0be018dda60310c826d`.
- Source branch inspected read-only: `feature/block-instances-fix` at `bde661d52b811ab7e06278a64faf64f467354daf`; it contains `feature/add-personalisation` at `2bbcd1b35c9481794e5d4f9be379e8fb486c4560`.
- Corrective code commit: `0618726d2ccce1a0a9c765a6d14aa08cf581bc62`.
- Corrective documentation/session commits preceded the full-review mission; latest integration head is recorded above.
- `CompleteBackupService.PickAndRestoreAsync` fails closed with `NotSupportedException` before file selection or database initialization. The destructive import path is removed. The UI restore toolbar action is disabled and the page explains that restore is temporarily unavailable. Backup creation and sharing remain available; safe restore is not implemented.
- `SqliteDynamicFormService.GetTemplatesAsync` returns every template in the active Workspace, still ordered by name. A used template remains visible and reusable; another Workspace's template stays filtered out.
- Implementation report claimed Android Debug build PASS (0 warnings, 0 errors); SQLite harness PASS (`failed=0`, 39 PASS, tests 16/17 NOT_TESTED); manual MAUI flow NOT TESTED (`adb` unavailable). The independent reviewer could not reproduce Android build because of environment write/NuGet/asset errors; see the report for exact commands.
- Independent corrective review: `docs/ai-handoff/inbox/20261124-CODEX-pr5-corrective-review.txt`, report branch `ai-handoff/report-corrective-backup-templates`, report commit `63c6c277f90e170d2f8dc0c4293996e78bd79e35` (report blob SHA `8e4ef86c1502840170e77cda6565cf5e780017a0`). Verdict: targeted correction acceptable with build/UI caveats; full PR not approved.
- Historical test 40 was considered insufficient for two distinct clients reusing the same source template. The full PR review mission explicitly requires revalidation of this gap and the template provisional-clone lifecycle.

## Earlier integration status and known audit areas

The following risks were identified in earlier reviews and must be revalidated against the exact current PR diff; do not assume they remain present or have been fixed:

1. Backup restore destructive behavior (targeted correction now disables restore; verify every entry point).
2. Template visibility/reuse and template clone lifecycle.
3. BlockInstance identity and whether repeated independent occurrences of a block are supported.
4. SQLite coordinator adoption across services.
5. Client deletion and orphaned dynamic records/values.
6. Workspace scoping for clients, search, records and exports.
7. Structure-edit cancellation and persistence rollback.
8. Backup export scope consistency between clients and dynamic records.
9. Actual UI validation versus service-level harness coverage.

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

## Branch governance — mandatory

- `develop` is the canonical application integration branch.
- `feature/*` branches are temporary implementation branches based on the current `develop` unless a mission explicitly authorizes another base.
- `ai-handoff/report-*` branches contain reports only and are never implementation bases.
- Every mission must have a uniquely named brief in `outbox/`; every agent task must have a unique report in `inbox/`.
- Before each task, refresh remote refs and record exact SHAs. Stop if the authorized base or prerequisites are unclear.
- No merge, rebase, cherry-pick, reset, force-push, branch deletion, or destructive Git operation without explicit user approval.
- No push to `develop` or `master` without explicit authorization.
- Never claim local uncommitted changes are visible through GitHub.
- Never use or modify the user's real SQLite database during tests.
- Preserve the exact UI rule: “Modifier” edits content only; pencil “✎” edits the full structure/template.
- Cloud orchestration PR #4 remains parked; continue the normal manual `@execute` / `@review` workflow.

