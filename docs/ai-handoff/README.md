# AI Handoff — ChatGPT × Codex

This folder is the shared, Git-backed handoff point between ChatGPT (product/architecture/QA direction) and GitHub Copilot/Codex (repository execution).

## Folders

- `outbox/`: instructions and decisions written by ChatGPT for Codex.
- `inbox/`: reports and deliverables written by Codex agents for ChatGPT.
- `SESSION_STATE.md`: compact source of truth for current project state and next approved work.
- `AGENT_REPORT_TEMPLATE.txt`: required structure for agent reports.

## Protocol

1. ChatGPT writes a uniquely named mission/instruction file in `outbox/`.
2. Codex reads the latest applicable instruction from `outbox/` before acting.
3. Codex works only within the stated scope and follows repository instructions and the selected agent role.
4. At completion, Codex writes a plain-text report (Markdown-formatted text is acceptable) in `inbox/`. Use a unique name such as `YYYYMMDD-HHMM_MISSION-N_STEP-N_AGENT-ROLE.txt`. Never overwrite an earlier report.
5. The agent must also include the complete report directly in its final response, not only in the file.
6. Codex commits and pushes the handoff files to the shared branch `develop` (or the explicitly agreed branch). ChatGPT can then read the committed files through GitHub.
7. ChatGPT reviews the report and updates `SESSION_STATE.md` and/or writes the next instruction in `outbox/`.

## Important limitations

- This is Git-based synchronization, not a live shared folder. ChatGPT cannot see uncommitted local files.
- A file becomes available to ChatGPT only after it is pushed to GitHub and the GitHub connector can read the branch.
- Do not commit secrets, tokens, passwords, private keys, customer data, or production database contents.
- Do not let multiple agents write the same handoff file concurrently.
- Do not treat an agent's claimed test result as proof without checking the command output and, where appropriate, rerunning the test.
- Never start the next implementation step merely because a report exists; wait for explicit approval in the conversation or an instruction file.
