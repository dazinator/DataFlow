# Workflow Feedback Migration

**Date**: 2025-11-10  
**Status**: In Progress  
**Parent Issue**: #251  
**Implementation Issue**: #252

---

## Overview

Workflow feedback has been migrated from file-based tracking (`.github/workflow-improvements.md`) to GitHub Issues for better tracking and integration.

## Migration Status

- [x] **Phase 1**: Parent feedback tracker issue created (#254)
- [x] **Phase 2**: Migration script created and tested (`.github/scripts/migrate-workflow-feedback.py`)
- [x] **Phase 3**: All workflow documentation updated to use issue-based feedback
- [x] **Phase 4**: Original file archived to `.github/archive/workflow-improvements.md`
- [x] **Phase 5**: Execute migration script to create all 30 feedback issues (completed using MCP tools)
- [x] **Phase 6**: Link all feedback issues as sub-issues of parent #254 (completed using MCP tools)

---

## New System

**Parent Tracker**: Issue #254 - `[Workflow Feedback] Tracker`

**Finding Feedback**:
- Search: `"[Workflow Feedback] Tracker" in:title state:open`
- Query open children: Use `issue_read(method="get_sub_issues", issue_number=254)`

**Process**: See "Self-Improvement Loop" in `.github/copilot-instructions.md`

---

## Historical Data

- **Original file**: `.github/archive/workflow-improvements.md`
- **Total entries**: 30 feedback entries across 5 workflows
  - Research: 4 entries
  - Implementation: 6 entries
  - General: 10 entries
  - Process Modeling: 7 entries
  - POC: 3 entries
- **Status**: 28 open, 2 closed (based on ✅ markers)

---

## Completing the Migration

The migration script is ready to execute but was not run as part of this PR to allow for review before creating 30 new issues.

### To Complete Migration

Run the migration script with the GitHub CLI authenticated:

```bash
# Navigate to repository root
cd /path/to/lib-dataflow

# Ensure gh CLI is authenticated
gh auth status

# Run migration script (dry-run first to verify)
python3 .github/scripts/migrate-workflow-feedback.py --dry-run

# Execute actual migration
python3 .github/scripts/migrate-workflow-feedback.py
```

**What the script does:**
1. Parses all 30 entries from archived `workflow-improvements.md`
2. Creates GitHub issues for each entry
3. Applies `workflow:process-modeling` label
4. Sets correct open/closed status based on ✅ markers
5. Outputs issue numbers for manual sub-issue linking

**Manual step required after script completes:**
- Link all created child issues to parent #254 using GitHub UI or GraphQL API
- See implementation guide for linking examples

### Alternative: Manual Migration

If preferred, feedback entries can be migrated manually:

1. Open `.github/archive/workflow-improvements.md`
2. For each entry, create a new issue using the feedback template
3. Link each issue to parent #254 as a sub-issue

---

## Providing Feedback (New Process)

When completing work:

1. Find parent tracker: Search for `[Workflow Feedback] Tracker`
2. Create child feedback issue (see template in copilot-instructions.md)
3. Link to parent using `sub_issue_write` MCP tool

See workflow documentation for complete instructions.

---

## Rollback Plan

If issues arise:

1. Restore `.github/workflow-improvements.md` from archive
2. Revert workflow documentation changes
3. Close created feedback issues
4. Document what went wrong

The parent tracker issue #254 can remain - it doesn't interfere with the file-based system.

---

## References

- **Design Document**: `/research/workflow-modeling/feedback-issues-design.md`
- **Implementation Guide**: `/research/workflow-modeling/IMPLEMENTATION_GUIDE.md`
- **Phase 1 (Complete)**: Issue #242
- **Parent Issue**: Issue #251
- **Feedback Tracker**: Issue #254
- **Migration Script**: `.github/scripts/migrate-workflow-feedback.py`
