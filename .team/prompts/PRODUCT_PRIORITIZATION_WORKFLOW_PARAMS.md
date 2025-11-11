# Product Prioritization Workflow Parameters

Configuration parameters for the Product Prioritization workflow (`.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md`).

## Core Parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `IMPLEMENTATION_QUEUE_LIMIT` | `10` | Maximum issues in `workflow:implementation` queue (WIP limit) |
| `SELECTION_BATCH_SIZE` | `100` | Issues to query per page (pagination size) |
| `PRIORITY_OVERRIDE_REQUIRES_RATIONALE` | `true` | Whether priority overrides need rationale |

## Priority Levels

| Level | Name | Description |
|-------|------|-------------|
| 1 | Highest | Critical, blocking, time-sensitive (security P1-P2, blockers) |
| 2 | High | Important, significant value (high-value features, tech debt) |
| 3 | Normal | Default priority (standard features, routine improvements) |
| 4 | Lower | Nice-to-have, not urgent (enhancements, optimizations) |
| 5 | Lowest | Defer unless capacity allows (exploratory work, low impact) |

## Selection Criteria

| Criteria | Rule |
|----------|------|
| **Security - Critical CVE in Core** | Priority 1 |
| **Security - High CVE in Core** | Priority 2 |
| **Security - Medium CVE in Core** | Priority 3 |
| **Security - CVE in Non-Core** | Max Priority 3 (regardless of severity) |
| **Tech Debt Minimum** | At least 1 tech debt item if any exist in backlog |
| **Core Code Definition** | `/src/` excluding test projects |
| **Non-Core Code Definition** | `/sample/`, `/tools/`, `/poc/`, tests, build scripts |

## Housekeeping Parameters (Reference Only)

These are handled by Bulk Triage workflow (`.team/prompts/TRIAGE_WORKFLOW.md` Step 6):

| Parameter | Value | Description | Handled By |
|-----------|-------|-------------|------------|
| `DUPLICATE_SIMILARITY_THRESHOLD` | `0.8` | Title similarity ratio for duplicate detection | Triage Step 6a |
| `STALE_THRESHOLD_DAYS` | `180` | Days of inactivity before stale flag | Triage Step 6c |
