# Scenario 001: Backlog-Driven Entry Selection

## Context

A user creates a workflow improvement issue selecting "Backlog-Driven" mode. The copilot agent needs to:
1. Read workflow-improvements.md
2. Select the top unaddressed entry
3. Extract context from that entry
4. Update plan.md with the details

## Starting Point

- User has created issue with "Workflow Improvements" template
- Issue has "Backlog-Driven" mode selected
- workflow-improvements.md contains multiple entries with various states:
  - Some fully addressed (all ✅)
  - Some partially addressed (some ✅, some not)
  - Some fully unaddressed (no ✅)

## Steps to Follow

Following `/.team/workflows/PROCESS_MODELING_WORKFLOW.md` → "How to Initiate Process Modeling" → "Mode 2: Backlog-Driven":

1. **Read** `.github/workflow-improvements.md`

2. **Select** the top unaddressed entry:
   - Scan sections in order: Research → Implementation → General → POC → Documentation
   - Find first entry with at least one unaddressed improvement (no ✅ marker)
   - If all improvements in an entry are marked ✅, skip to next entry

3. **Extract context** from the selected entry:
   - Date and Issue/PR reference
   - What worked well
   - What didn't work well
   - Suggested improvement(s) - identify which are unaddressed
   - Which workflow(s) affected

4. **Update** `/research/workflow-modeling/plan.md` with:
   - Selected entry details
   - Which specific improvements you're addressing
   - Expected workflow changes

## Expected Outcome

✅ **Success Criteria:**
- Agent correctly identifies first unaddressed entry in workflow-improvements.md
- Agent skips over fully-addressed entries (all items marked ✅)
- Agent extracts all relevant context from the selected entry
- plan.md is updated with:
  - Clear identification of which entry was selected (date, issue/PR)
  - List of unaddressed improvement items
  - Which workflows will be affected
  - Next steps for process modeling

❌ **Failure Indicators:**
- Agent selects wrong entry (not the top unaddressed one)
- Agent misses context information
- plan.md update is incomplete or unclear
- Agent gets confused by partially-addressed entries

## Test Setup

Create test version of workflow-improvements.md with:
- Entry 1 (Research): All improvements marked ✅ ADDRESSED → Skip
- Entry 2 (Research): 2 improvements, 1 marked ✅, 1 not → **Select this one**
- Entry 3 (Implementation): No ✅ markers → Should not reach this

Expected selection: Entry 2

## Test Execution

### Test Data

Create temporary test file at `/tmp/test-workflow-improvements.md`:

```markdown
# Workflow Improvement Suggestions

## Research Workflow Improvements

### Suggestions

- **Date**: 2025-11-01
- **Issue/PR**: #100
- **What worked well**: Clear documentation
- **What didn't work well**: Missing examples
- **Suggested improvement**: 
  1. ✅ **ADDRESSED**: Add more examples to docs
  2. ✅ **ADDRESSED**: Create template for common cases

---

- **Date**: 2025-11-02
- **Issue/PR**: #101
- **What worked well**: Good structure
- **What didn't work well**: 
  - Unclear timeline guidance
  - Missing success criteria
- **Suggested improvement**: 
  1. ✅ **ADDRESSED**: Add timeline estimation guide
  2. Add success criteria template to research workflow

---

## Implementation Workflow Improvements

### Suggestions

- **Date**: 2025-11-03
- **Issue/PR**: #102
- **What worked well**: Everything
- **What didn't work well**: Nothing specific
- **Suggested improvement**: Add more examples throughout
```

### Simulation

**Agent perspective:**

1. Read the test file
2. Scan Research section:
   - Entry 1: Both items marked ✅ → Skip
   - Entry 2: Item 1 marked ✅, Item 2 NOT marked → **Select this entry**
3. Extract context:
   - Date: 2025-11-02
   - Issue/PR: #101
   - What worked well: Good structure
   - What didn't work well: Unclear timeline guidance, Missing success criteria
   - Suggested improvements:
     - ✅ Item 1 already addressed (skip)
     - Item 2: "Add success criteria template to research workflow" (PROCESS THIS)
   - Affected workflow: Research Workflow
4. Update plan.md with this information

## Test Result

**Status**: PASS ✅

**Notes:**
- Logic for selecting first unaddressed entry is clear
- Distinction between "skip fully addressed" and "process partially addressed" is explicit
- Context extraction requirements are well-defined
- plan.md update requirements are clear

**Observations:**
- Workflow correctly handles partially-addressed entries
- The scan order (Research → Implementation → General → POC → Documentation) is well-defined
- "At least one unaddressed improvement" criterion is clear

**Minor Issues:**
- None identified - instructions are clear and unambiguous

## Recommendations

✅ No changes needed - workflow instructions are clear and complete for entry selection.
