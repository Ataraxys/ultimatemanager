# Inbox — Codex → ChatGPT

Codex agents place completed reports and deliverables here as uniquely named `.txt` files.

Naming convention:

`YYYYMMDD-HHMM_MISSION-N_STEP-N_AGENT-ROLE.txt`

Examples:

- `20261123-0200_MISSION-1_STEP-4A_PROJECT-ARCHITECT.txt`
- `20261123-0430_MISSION-1_STEP-4B_BACKEND-SPECIALIST.txt`
- `20261123-0600_MISSION-1_STEP-4C_QA-REVIEWER.txt`

The report may use Markdown headings and tables even though the extension is `.txt`.

Mandatory:
- write the complete report in this file;
- include the complete same report directly in the agent's final response;
- clearly distinguish observed facts, assumptions, and untested items;
- list tests as PASS / FAIL / NOT TESTED;
- never invent test or build results;
- state files changed, scope deviations, risks, and next recommendation.

Never overwrite an earlier report. Create a new file for each run.
