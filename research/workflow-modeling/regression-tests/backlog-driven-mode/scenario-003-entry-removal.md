# Scenario 003: Entry Removal from workflow-improvements.md

## Context

After completing a backlog-driven process modeling improvement, the copilot agent needs to remove the processed entry from workflow-improvements.md. This happens whether the improvement was successful or unsuccessful.

## Starting Point

- Process modeling work has been completed
- An entry from workflow-improvements.md was processed
- Agent is ready to clean up and archive

## Steps to Follow

Following `/.team/workflows/PROCESS_MODELING_WORKFLOW.md` → "Mode 2: Backlog-Driven" → "After completion":

1. **Remove the entire entry** from `.github/workflow-improvements.md`
2. **Entry Removal rule**: "Remove the entire entry even if improvements were not viable. This prevents the queue from getting stuck."
3. **Add one-line summary** to `/research/workflow-modeling/history.md`
4. **Archive plan** to `/research/workflow-modeling/archive/`

## Expected Outcome

✅ **Success Criteria:**
- Entire entry is removed from workflow-improvements.md
- Section structure remains intact (headers, formatting)
- Adjacent entries are not affected
- Comments/placeholders in the section are preserved
- History.md has corresponding entry
- Queue is unblocked for next backlog-driven issue

❌ **Failure Indicators:**
- Entry is marked ✅ but not removed
- Adjacent entries are accidentally removed
- Section headers are damaged
- File structure is corrupted

## Test Execution

### Simulation 1: Remove Entry from Middle of Section

**Context**: Processed entry #101 from Research section

**Before (workflow-improvements.md excerpt):**
```markdown
## Research Workflow Improvements

### Suggestions

- **Date**: 2025-11-01
- **Issue/PR**: #100
- **What worked well**: Clear docs
- **What didn't work well**: Missing examples
- **Suggested improvement**: 
  1. ✅ **ADDRESSED**: Add examples

---

- **Date**: 2025-11-02
- **Issue/PR**: #101
- **What worked well**: Good structure
- **What didn't work well**: Missing criteria
- **Suggested improvement**: 
  1. Add success criteria template

---

- **Date**: 2025-11-03
- **Issue/PR**: #102
- **What worked well**: Everything
- **What didn't work well**: Nothing
- **Suggested improvement**: Add more examples

<!-- Add research workflow improvement suggestions here -->
```

**After removing entry #101:**
```markdown
## Research Workflow Improvements

### Suggestions

- **Date**: 2025-11-01
- **Issue/PR**: #100
- **What worked well**: Clear docs
- **What didn't work well**: Missing examples
- **Suggested improvement**: 
  1. ✅ **ADDRESSED**: Add examples

---

- **Date**: 2025-11-03
- **Issue/PR**: #102
- **What worked well**: Everything
- **What didn't work well**: Nothing
- **Suggested improvement**: Add more examples

<!-- Add research workflow improvement suggestions here -->
```

**Result**: ✅ PASS
- Entry #101 completely removed
- Entry #100 (before) preserved
- Entry #102 (after) preserved
- Section structure intact
- Comment placeholder preserved

### Simulation 2: Remove Last Entry in Section

**Context**: Processed entry #102 from Research section (the last entry)

**After removing entry #102:**
```markdown
## Research Workflow Improvements

### Suggestions

- **Date**: 2025-11-01
- **Issue/PR**: #100
- **What worked well**: Clear docs
- **What didn't work well**: Missing examples
- **Suggested improvement**: 
  1. ✅ **ADDRESSED**: Add examples

<!-- Add research workflow improvement suggestions here -->
```

**Result**: ✅ PASS
- Only entry #102 removed
- Section header and comment preserved
- No corruption

### Simulation 3: Remove Entry with Partially Addressed Items

**Context**: Entry has mix of ✅ ADDRESSED and unaddressed items

**Before:**
```markdown
- **Date**: 2025-11-05
- **Issue/PR**: #105
- **What worked well**: Good
- **What didn't work well**: Could be better
- **Suggested improvement**: 
  1. ✅ **ADDRESSED**: First improvement
  2. Second improvement (not addressed)
  3. Third improvement (not addressed)
```

**Rule**: Remove entire entry even if some items are unaddressed

**After:**
```markdown
[Entry completely removed]
```

**Result**: ✅ PASS
- Entire entry removed as specified
- This prevents partial processing from blocking the queue
- If some items are still valuable, they can be re-added as new suggestion

### Simulation 4: Remove Entry - Improvement Was Unsuccessful

**Context**: Attempted improvement but determined not viable after testing

**Rule**: "Remove the entire entry even if improvements were not viable"

**Action**:
1. Remove entry from workflow-improvements.md
2. Add to history.md: "Attempted X but determined not viable after testing"

**Result**: ✅ PASS
- Entry removed even though improvement failed
- Prevents failed improvements from blocking queue
- History preserves record of what was attempted

## Test Result

**Status**: PASS ✅

**Notes:**
- Entry removal instructions are clear: "Remove the entire entry"
- Rationale is provided: "prevents the queue from getting stuck"
- Applies to both successful and unsuccessful improvements
- Preserves history via history.md

**Observations:**
- Clear that entire entry should be removed (no partial removal)
- Handles edge cases well (last entry, partially addressed, unsuccessful)
- Section structure preservation is important
- History.md serves as permanent record

**Minor Issues:**
- None identified

## Recommendations

✅ No changes needed - entry removal guidance is clear and complete.

**Additional observation**: The approach of removing entries (rather than marking them) keeps workflow-improvements.md as a clean "to-do" list while history.md serves as the "done" list. This is a good separation of concerns.
