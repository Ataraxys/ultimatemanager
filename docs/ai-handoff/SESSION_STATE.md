# UltimateManager — AI Session State

Last updated: 2026-11-25

## Mission active — MAUI data stabilization, direct integration workflow

- Responsible agent: **MAUI Developer** (implementation agent, not `@qa-reviewer`).
- Mission: `docs/ai-handoff/outbox/20261125_MISSION-MAUI-DATA-STABILIZATION-INTEGRATE-DEVELOP.md`.
- The owner explicitly authorized the MAUI Developer to work directly on the existing branch `integration/reconcile-feature-branches`, commit implementation changes there, and integrate PR #5 into `develop` when the delivery criteria are met.
- No separate implementation branch is authorized or required.
- Known starting PR head before publishing this mission: `b59b6352bb2c473a1da84a1e62d7ca564a2f4e18`.
- Mission publication commit: `aea2fce96cca1f96647e2a8c4fef70520ddb20a1`. Refresh the branch and use its actual latest HEAD before coding.
- PR #5: https://github.com/Ataraxys/ultimatemanager/pull/5 — base `develop`, head `integration/reconcile-feature-branches`.
- Scope: implement applicable P1/P2 findings from the Copilot QA report (Workspace isolation, client deletion/cascades, SQLite coordinator adoption, template switching and cancellation, atomic dynamic operations, safe migrations, backup consistency/performance), preserve all product UX rules, add regression tests using temporary databases, and run available Android builds.
- Conditional integration authorization: after all P1 findings are fixed, essential tests pass, migrations are safe, and the complete PR diff is checked, merge PR #5 into `develop` using a normal merge commit. No direct push to `develop`. If criteria are not met, leave PR #5 open and report exact blockers.
- No new audit cycle. The MAUI Developer owns implementation through delivery. No handoff to another agent for implementation.
- Required report: unique `.txt` in `docs/ai-handoff/inbox/` on a report-only `ai-handoff/report-data-stabilization` branch based on refreshed `develop`, per `AGENT_PROTOCOL.md`.
- Mission file was published on the integration branch in commit `aea2fce96cca1f96647e2a8c4fef70520ddb20a1`.

## Current implementation status — committed and pushed, integration pending

- Starting implementation HEAD: `729d77eecd9a1662676eb33f172b38e8e51a74d0` on `integration/reconcile-feature-branches`; at last fetch, `origin/develop` was `47b6dbf615cd62cdb9c5c0be018dda60310c826d` and integration was 22 commits ahead, 0 behind. Re-fetch before report-only publication.
- Application changes are committed and pushed on `integration/reconcile-feature-branches` as `b5d9b4fda4f7d9d819c6fed198b1fb9c770006ff` (`fix: stabilize workspace-scoped client data`). The remote integration ref was verified to match this SHA. The pre-existing user change to `.github/copilot-instructions.md` was deliberately excluded and remains uncommitted locally.
- The commit covers Workspace-scoped client data, coordinator-backed persistence, transactional client deletion/dynamic operations, Workspace-filtered backups, old Client-schema migration, and temporary client draft/template selection.
- New temporary-database harness coverage added for atomic draft persistence/remapping, template A→B transfer and cancellation, Workspace isolation/client cascades, historical Client migration/idempotence, ZIP backup Workspace scope/temp cleanup, the `ClientEditViewModel.SaveAsync` creation path, dynamic block/field deletion cascades, and backup temp cleanup after missing attachment failure.
- Latest verified harness: `dotnet run --project Tests/SQLiteCoordinatorHarness/SQLiteCoordinatorHarness.csproj -c Debug` — 49 PASS, 2 NOT_TESTED, 0 FAIL. The NOT_TESTED cases are close/release failure injection; the coordinator has no test seam for those paths.
- Latest verified builds: `dotnet build UltimateManager.slnx -c Debug --no-restore` PASS (30 warnings; first parallel attempt failed/was interrupted, isolated rerun passed); `dotnet build UltimateManager.csproj -f net10.0-android -c Debug --no-restore` PASS; Android Release equivalent PASS. `git diff --check` on the local diff PASS. Manual UI/device test NOT RUN.
- A review found and fixed a blocking `IsBusy` double-check: new-client `SaveAsync` now delegates before acquiring the edit path's busy flag. The regression test confirms exactly one atomic create and reload with the returned persisted TemplateId.
- Current PR diff baseline was 85 files and 22 commits ahead of `develop`. A whole-PR `git diff --check origin/develop...HEAD` reports existing trailing whitespace in `docs/ai-handoff/outbox/20261124_MISSION-PR5-REVUE-COMPLETE-82-FICHIERS.md`, `docs/ai-handoff/outbox/20261125_MISSION-MAUI-DATA-STABILIZATION-INTEGRATE-DEVELOP.md`, and similar handoff-only lines; local working diff check was clean at last run. GitHub CLI `gh` is unavailable, so PR state/body/head cannot be verified or updated with that CLI.
- Complete PR comparison against `develop` last showed 85 files and 22 commits ahead, 0 behind. `git diff --check origin/develop...HEAD` reports existing trailing whitespace in handoff mission documents; the worktree diff check is clean. The `gh` CLI is unavailable, so remote PR state/body cannot be read or updated through it.
- Remaining before full delivery: update PR #5 through an available authenticated GitHub mechanism; the `gh` CLI is not installed and PR page retrieval was unavailable. Do not merge while PR state/body/complete P1-P2 safety remains unverified. Report-only branch/report publication and exact report SHA remain pending. Manual UI test remains NOT RUN.
- Do not claim mission completion, publication, or merge until those remaining items are actually completed and verified.

## Previous Copilot QA report — findings to implement, not re-audit

Report: `docs/ai-handoff/inbox/20261125-COPILOT-QA-PR5-FULL-REVIEW.txt`, branch `ai-handoff/report-copilot-pr5-full-review`, report commit `48f847392d526428df5e684c07d14270c400859a`.

Key findings recorded there:
- P1 Workspace isolation absent for clients and backup export.
- P1 client deletion may leave dynamic records, values, instances and client-specific templates orphaned.
- P1 several services open independent SQLite connections instead of consistently using the coordinator.
- P1 switching templates during new-client creation may conflict with an already-created record.
- P1 cancelling new-client creation may leave persisted provisional clones/records.
- P2 multi-step dynamic operations may not be transactional and may leave orphans.
- P2 migration code may swallow unexpected SQLite errors.
- P2 backup regeneration and snapshot behavior need correction.
- Test harness coverage gaps include Workspace isolation, client deletion, cancellation and template switching.
These are implementation leads; verify current code while fixing them and do not create another general audit deliverable.

## Previous targeted corrective work

- Corrective mission: `docs/ai-handoff/outbox/20261124_CORRECTIVE-BACKUP-TEMPLATES.md`.
- Corrective code commit: `0618726d2ccce1a0a9c765a6d14aa08cf581bc62`.
- Restore remains deliberately fail-closed/unavailable; do not reintroduce destructive restore.
- `SqliteDynamicFormService.GetTemplatesAsync` returns templates for the active Workspace, including templates already in use.
- Prior implementation report claimed Android Debug build PASS and SQLite harness 39 PASS / 2 NOT_TESTED; manual MAUI UI was NOT TESTED. Treat these as historical claims, not proof for the new changes.
- Independent corrective review: `docs/ai-handoff/inbox/20261124-CODEX-pr5-corrective-review.txt`, report commit `63c6c277f90e170d2f8dc0c4293996e78bd79e35`.

## Collaboration model

- ChatGPT handles product/technical arbitration and delivery review.
- The MAUI Developer owns this implementation end-to-end.
- Shared handoff location: `docs/ai-handoff/`.
- ChatGPT → agents: `docs/ai-handoff/outbox/`.
- Agents → ChatGPT: `docs/ai-handoff/inbox/`.
- Every completed agent task publishes one unique report.
- The report is evidence, not automatic approval.

## Product and architecture direction

- UltimateManager is intended to evolve into a polished, extensible business-management platform.
- Build local-first with SQLite now; do not introduce cloud sync prematurely.
- Keep standard business entities strongly typed.
- Use a dynamic object/field engine for user-defined object types and custom fields.
- Workspaces are a central data-isolation boundary.
- Prefer clean, maintainable object-oriented architecture without premature project splitting or overengineering.
- Preserve existing functionality and user data; avoid destructive schema or restore operations.
- Record architectural decisions in `docs/01-architecture/DECISIONS.md` when relevant.
- Preserve the exact UI rule: “Modifier” edits content only; pencil “✎” edits the full structure/template.

## Branch governance — current explicit authorization

- `develop` remains the canonical application integration branch.
- For this mission only, the owner explicitly authorizes direct implementation commits on the existing `integration/reconcile-feature-branches` branch.
- PR #5 is the authorized path for integrating that branch into `develop` after the mission's validation criteria are satisfied.
- Do not create another implementation branch for this mission.
- Do not push directly to `develop`; use PR #5 and a normal merge commit after verifying all required criteria.
- No merge/rebase/cherry-pick/reset/force-push/branch deletion except the explicitly authorized final merge of PR #5 when its conditions are met.
- Never use or modify the user's real SQLite database during tests.
- Preserve the protected worktree `feature/block-instances-fix` and its pre-existing local `UltimateManager.csproj` diff.
- Cloud orchestration PR #4 remains parked.
