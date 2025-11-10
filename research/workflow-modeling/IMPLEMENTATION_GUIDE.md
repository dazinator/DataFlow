# Implementation Guide: Migrate to Issue-Based Feedback System

**Date**: 2025-11-10  
**Status**: Ready for Implementation  
**Approved By**: Tabletop Simulation (all scenarios PASS)

---

## Overview

This guide provides step-by-step instructions for migrating from file-based workflow feedback (`.github/workflow-improvements.md`) to GitHub issue-based tracking using parent-child issue relationships.

**Estimated Total Effort**: 6-7 hours (using MCP tools directly, no script development needed)

---

## Prerequisites

- [x] Design document created and reviewed
- [x] Test scenarios created and validated
- [x] Tabletop simulation completed (all scenarios PASS)
- [ ] Approval to proceed with migration

---

## Phase 1: Create Parent Feedback Issue

**Duration**: 5 minutes  
**Risk**: Low

### Parent Issue Template

```markdown
Title: [Workflow Feedback] Tracker

Labels: workflow:process-modeling

Body:
# Workflow Feedback Tracker

This issue tracks feedback and improvement suggestions for all workflows.

Each suggestion is tracked as a child issue below. **Open children = not yet addressed**.

## Migration Note

This parent issue was created during migration from `.github/workflow-improvements.md` on 2025-11-10.
All historical feedback has been migrated to child issues.

## How This Works

1. **Workflow agents** create child feedback issues under this parent after completing work
2. **Process Modeling workflow** processes open children using standard process modeling methodology
3. **Closed children** = implemented improvements

See [Process Modeling Workflow](/.team/prompts/PROCESS_MODELING_WORKFLOW.md) for details.

## Providing Feedback

When completing work on an issue:

1. Search for this parent issue: `[Workflow Feedback] Tracker`
2. Create child issue with your feedback (use template format below)
3. Link child to this parent using `sub_issue_write` MCP tool

### Feedback Issue Template

```markdown
## Workflow Feedback Entry

**Date**: YYYY-MM-DD
**Issue/PR**: #XXX - Brief description
**Workflow**: [Workflow Name or "Multiple" if spans workflows]

### What Worked Well

- [List things that worked well]

### What Didn't Work Well

- [List pain points or confusion]

### Suggested Improvement

[Specific, actionable improvement with rationale]

### Implementation Notes

[Optional: Hints for implementation, affected files, etc.]
```

## Query Open Feedback

**For Process Modeling**:

```python
# Get open children from parent
parent = issue_read(
    method="get_sub_issues",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=[parent_number]
)

open_children = [c for c in parent.children if c.state == "open"]
```
```

### Label Requirements

The parent issue only needs the existing `workflow:process-modeling` label:

- **`workflow:process-modeling`** (should already exist)
  - Description: "Process Modeling workflow queue item"
  - Color: `#D4C5F9` (light purple)

No new labels need to be created.

### Creation Steps

1. **Create parent issue**:
   ```python
   parent = issue_write(
       method="create",
       owner="uniun-technology",
       repo="lib-dataflow",
       title="[Workflow Feedback] Tracker",
       labels=["workflow:process-modeling"],
       body="... (use template above) ..."
   )
   
   print(f"Created parent issue #{parent.number}")
   ```

**Action**: Create the single parent feedback tracker issue

---

## Phase 2: Migrate Historical Feedback Using MCP Tools

**Duration**: 2-3 hours  
**Risk**: Low (direct MCP usage, no script needed)

### Migration Approach

Instead of building a one-time migration script, use MCP tools directly to read and migrate feedback entries. This approach:

- ✅ Avoids building/maintaining a throwaway script
- ✅ Uses the same tools workflows will use going forward
- ✅ Allows manual review and duplicate checking
- ✅ More transparent and debuggable

### Migration Requirements

1. **Read** `.github/workflow-improvements.md` manually or with simple parsing
2. **Extract** each feedback entry with metadata
3. **Check for duplicates** before creating
4. **Create** child issues for each entry using `issue_write`
5. **Link** children to parent using `sub_issue_write` (ensures proper sub-issue relationship)
6. **Preserve** implementation status (open vs closed based on ✅ markers)

### Migration Process Using MCP Tools

**For each feedback entry** in `.github/workflow-improvements.md`:

1. **Read entry** manually or with simple text processing
2. **Check for duplicates** by searching existing issues
3. **Create child issue** using `issue_write`:
   ```python
   child = issue_write(
       method="create",
       owner="uniun-technology",
       repo="lib-dataflow",
       title="[Brief description from first improvement]",
       state="open",  # or "closed" if all improvements have ✅
       body="""
       ## Workflow Feedback Entry
       
       **Date**: YYYY-MM-DD
       **Issue/PR**: #XXX
       **Workflow**: [Workflow name from section]
       
       **Migration Note**: Migrated from `.github/workflow-improvements.md`
       
       ### What Worked Well
       [Content from entry]
       
       ### What Didn't Work Well
       [Content from entry]
       
       ### Suggested Improvements
       [Content from entry - preserve ✅ markers]
       """
   )
   ```

4. **Link to parent as sub-issue** (IMPORTANT):
   ```python
   sub_issue_write(
       method="add",
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=parent_number,  # From Phase 1
       sub_issue_id=child.id  # From create response
   )
   ```

**Status Logic**:
- If ALL improvements start with ✅ → Create with `state="closed"`
- If ANY improvements lack ✅ → Create with `state="open"`
- Preserve ✅ markers in issue body for reference

**Duplicate Detection**:
```python
# Search for similar titles before creating
existing = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query=f"in:title {first_few_words}"
)

# Review matches and skip if duplicate found
```

**Entry Structure** in current file:
```markdown
- **Date**: YYYY-MM-DD
- **Issue/PR**: #XXX or description
- **What worked well**: ...
- **What didn't work well**: ...
- **Suggested improvement**: 
  1. Item one
  2. ✅ Item two (implemented)
  3. Item three
```

**Action**: Migrate entries using MCP tools directly, checking for duplicates

---

## Phase 3: Execute Migration

**Duration**: 1-2 hours  
**Risk**: Low (manual review of each entry)

### Pre-Migration Checklist

- [ ] Parent issue created
- [ ] Parent issue number documented
- [ ] Backup of `.github/workflow-improvements.md` created
- [ ] Have access to MCP `issue_write` and `sub_issue_write` tools

### Migration Steps

1. **Get parent issue number** (from Phase 1)
2. **Open** `.github/workflow-improvements.md` for reading
3. **For each entry**:
   - Extract date, issue/PR, what worked, what didn't work, improvements
   - Check for duplicates using `search_issues`
   - If not duplicate, create child issue with `issue_write`
   - **IMPORTANT**: Link as sub-issue using `sub_issue_write(method="add", issue_number=parent, sub_issue_id=child.id)`
   - Mark entry as processed (comment out or track separately)
4. **Verify** after every 10-15 entries:
   ```python
   parent_data = issue_read(
       method="get_sub_issues",
       issue_number=parent_number
   )
   print(f"Total children: {len(parent_data)}")
   ```

### Verification

After migration:

```python
# Get all sub-issues
parent_data = issue_read(
    method="get_sub_issues",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=parent_number
)

total = len(parent_data)
open_count = sum(1 for c in parent_data if c.state == "open")
closed_count = sum(1 for c in parent_data if c.state == "closed")

print(f"Migration Results:")
print(f"  Total: {total}, Open: {open_count}, Closed: {closed_count}")
```

**Expected**: ~65-70 child issues total (verify against source file count)

- Check parent issue #{parent_number} shows sub-issues in GitHub UI
- Spot-check a few child issues for correctness
- Verify open/closed status matches ✅ markers in original entries

**Action**: Execute migration using MCP tools with duplicate checking

---

## Phase 4: Update Workflow Documentation

**Duration**: 2-3 hours  
**Risk**: Low

### Files to Update

1. **Process Modeling Workflow** (`.team/prompts/PROCESS_MODELING_WORKFLOW.md`)
2. **Research Workflow** (`.team/prompts/RESEARCH_WORKFLOW.md`)
3. **Implementation Workflow** (`.team/prompts/IMPLEMENTATION_WORKFLOW.md`)
4. **Tech Debt Workflow** (`.team/prompts/TECH_DEBT_WORKFLOW.md`)
5. **Product Prioritization Workflow** (`.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md`)
6. **Copilot Instructions** (`.github/copilot-instructions.md`)

### Change Pattern for Each Workflow

**Old (File-Based)**:
```markdown
### Self-Improvement Evaluation

Before marking PR ready for review:

1. Reflect on what worked well and what didn't
2. Add feedback entry to `.github/workflow-improvements.md`
3. Use the template format in that file
```

**New (Issue-Based)**:
```markdown
### Self-Improvement Evaluation

Before marking PR ready for review:

1. Reflect on what worked well and what didn't
2. Create feedback issue:
   - Search for parent: `[Workflow Feedback] Tracker`
   - Create child issue with feedback (see template below)
   - Link child to parent using `sub_issue_write`
3. Reference feedback issue in PR description

#### Feedback Issue Template

```markdown
Title: [Brief description of improvement]

Body:
## Workflow Feedback Entry

**Date**: YYYY-MM-DD
**Issue/PR**: #XXX
**Workflow**: [Workflow Name]

### What Worked Well
[List positives]

### What Didn't Work Well
[List pain points]

### Suggested Improvement
[Specific, actionable improvement]
```

#### Creating Feedback Issue

```python
# 1. Find or create parent
parent = search_issues(
    query="[Workflow Feedback] Tracker in:title state:open"
)[0]

# If parent doesn't exist, create it (see parent template)

# 2. Create child feedback issue
child = issue_write(
    method="create",
    title="Brief improvement description",
    labels=["workflow:process-modeling"],
    body="... (use template above) ..."
)

# 3. Link to parent
sub_issue_write(
    method="add",
    issue_number=parent.number,
    sub_issue_id=child.id
)
```
```

### Process Modeling Workflow Updates

**Backlog-Driven Mode** - Change from file parsing to issue querying:

**Old**:
```markdown
1. Read `.github/workflow-improvements.md`
2. Select top unaddressed entry
3. Process improvement
4. Remove entry from file
```

**New**:
```markdown
1. Query parent feedback trackers for open children
2. Select oldest/highest-priority open feedback issue
3. Process improvement
4. Close feedback issue with implementation comment
```

**Detailed Changes**:

```markdown
### Backlog-Driven Mode: Querying Feedback

**Query Open Feedback**

```python
# Get the feedback tracker parent
parent = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query='"[Workflow Feedback] Tracker" in:title state:open'
)[0]

# Get open children
parent_data = issue_read(
    method="get_sub_issues",
    issue_number=parent.number
)

open_children = [c for c in parent_data.children if c.state == "open"]
```

**Alternative: Query by label if needed**

```python
# Get feedback tracker by label
trackers = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query="label:workflow:process-modeling in:title '[Workflow Feedback] Tracker' state:open"
)



# Sort by priority/date
all_feedback.sort(key=lambda x: x.created_at)
```

### Processing Feedback

**When processing a feedback issue**:

1. **Read full details**:
   ```python
   feedback = issue_read(
       method="get",
       issue_number=selected_issue.number
   )
   ```

2. **Create scenarios and test** (standard process modeling)

3. **Implement improvement** (update workflow documentation)

4. **Close feedback issue**:
   ```python
   issue_write(
       method="update",
       issue_number=feedback.number,
       state="closed"
   )
   
   add_issue_comment(
       issue_number=feedback.number,
       body="""
       ✅ **Implemented**
       
       [Description of changes made]
       
       **Pull Request**: #XXX
       **Documentation**: [links]
       
       Thank you for the feedback!
       """
   )
   ```
```

**Action**: Update all workflow documentation files

---

## Phase 5: Archive Original File

**Duration**: 15 minutes  
**Risk**: None

### Steps

1. **Move file to archive**:
   ```bash
   mkdir -p .github/archive
   mv .github/workflow-improvements.md .github/archive/
   ```

2. **Create migration note**:
   ```bash
   cat > .github/WORKFLOW-FEEDBACK-MIGRATION.md << 'EOF'
   # Workflow Feedback Migration

   **Date**: 2025-11-10

   Workflow feedback has been migrated from `.github/workflow-improvements.md` 
   to GitHub Issues for better tracking and integration.

   ## New System

   - **Parent Issues**: Search for `label:feedback-tracker`
   - **Child Issues**: Individual feedback entries linked to parents
   - **Process**: See "Self-Improvement Loop" in `.github/copilot-instructions.md`

   ## Historical Data

   - **Original file**: `.github/archive/workflow-improvements.md`
   - **Migrated entries**: ~65-70 issues created across 7 parent trackers
   - **Migration script**: Available in commit history

   ## Providing Feedback (New Process)

   When completing work:

   1. Search for parent tracker: `[Workflow Name] Feedback Tracker`
   2. Create child feedback issue
   3. Link to parent using `sub_issue_write`

   See workflow documentation for complete instructions.
   EOF
   ```

3. **Update .gitignore** (if needed):
   ```bash
   # Ensure .github/archive/ is tracked
   ```

**Action**: Archive file and create migration documentation

---

## Phase 6: Testing and Validation

**Duration**: 1 hour  
**Risk**: Low

### Test Cases

1. **Verify parent issues exist and are searchable**
2. **Verify child issues are linked correctly**
3. **Test feedback creation** (create new test feedback issue)
4. **Test Process Modeling query** (query open feedback)
5. **Test feedback closure** (close a test issue)
6. **Verify documentation is clear** (spot-check updated workflows)

### Validation Checklist

- [ ] Parent issue created and labeled correctly
- [ ] All historical entries migrated (count matches)
- [ ] Open/closed status preserved correctly
- [ ] Parent-child links functional
- [ ] All workflow docs updated
- [ ] Copilot instructions updated
- [ ] Original file archived
- [ ] Migration note created
- [ ] Test feedback creation works
- [ ] Test feedback querying works

**Action**: Complete validation checklist

---

## Rollback Plan

If issues arise during migration:

1. **Stop migration immediately**
2. **Delete created issues** (if in dry-run testing phase)
3. **Restore** `.github/workflow-improvements.md` from archive
4. **Revert** workflow documentation changes
5. **Document** what went wrong
6. **Revise** migration script/approach
7. **Re-test** and try again

**Parent issues can remain** - they don't interfere with file-based system.

---

## Success Criteria

Migration is successful when:

- ✅ Parent issue created
- ✅ All ~65-70 historical entries migrated to child issues
- ✅ **All children linked as sub-issues** using `sub_issue_write` (visible in GitHub UI)
- ✅ Open/closed status preserved accurately
- ✅ Duplicates checked and avoided
- ✅ All workflow documentation updated
- ✅ Copilot instructions updated
- ✅ Original file archived (not deleted)
- ✅ Migration documented
- ✅ Test feedback creation works
- ✅ Process Modeling can query and process feedback

---

## Estimated Timeline

| Phase | Duration | Dependencies |
|-------|----------|--------------|
| 1. Create parent | 5 min | None |
| 2. Migrate with MCP | 2-3 hours | Phase 1 complete |
| 3. Execute migration | 1-2 hours | Phase 2 started (can overlap) |
| 4. Update workflows | 2-3 hours | Phase 3 complete |
| 5. Archive file | 15 min | Phase 4 complete |
| 6. Test & validate | 1 hour | Phase 5 complete |

**Total**: 6-7 hours (reduced from 7-9 by eliminating script development)

---

## Notes for Future Maintainers

- Parent issues should **never be closed** - they're permanent containers
- Child issues can be closed when feedback is addressed
- Multiple feedback items can reference the same parent issue/PR
- Process Modeling workflow processes open children in priority order
- New feedback uses same parent issues (no need to create new parents)
- If a workflow category is added, create a new parent following the template

---

## Contact

For questions or issues during migration:
- Review design document: `/research/workflow-modeling/feedback-issues-design.md`
- Review simulation results: `/research/workflow-modeling/scenarios/feedback-issues/SIMULATION_RESULTS.md`
- Check archived plan: `/research/workflow-modeling/archive/[date]-feedback-issues.md`
