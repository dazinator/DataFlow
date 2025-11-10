# Scenario 002: History Tracking

## Context

After completing a backlog-driven process modeling improvement, the copilot agent needs to add an entry to the history log.

## Starting Point

- Process modeling work has been completed (workflows updated, tests PASS)
- history.md exists at `/research/workflow-modeling/history.md`
- Agent is ready to complete the work and archive

## Steps to Follow

Following `/.team/prompts/PROCESS_MODELING_WORKFLOW.md` → "History Tracking":

1. **Determine what to log**:
   - Brief description of the workflow improvement made
   - Focus on outcome/benefit, not process details

2. **Add entry to history.md**:
   - Format: `- **YYYY-MM-DD**: Brief description`
   - Place newest entries first within the year
   - Keep description to one line (can wrap if needed)

3. **Examples to follow**:
   - `- **2025-11-08**: Added backlog-driven mode to Process Modeling Workflow`
   - `- **2025-11-08**: Simplified Product Prioritization template (55% reduction)`

## Expected Outcome

✅ **Success Criteria:**
- Entry is added to history.md in correct format
- Entry is placed at top of current year section
- Description is concise (one line)
- Description focuses on outcome/benefit
- Date is in YYYY-MM-DD format

❌ **Failure Indicators:**
- Entry uses wrong format
- Entry is too verbose (multiple paragraphs)
- Entry is placed at wrong location (not newest-first)
- Description focuses on process instead of outcome

## Test Execution

### Simulation 1: Successful Improvement

**Context**: Completed improvement to add success criteria template to Research Workflow

**Current history.md state:**
```markdown
## 2025

- **2025-11-08**: Simplified Product Prioritization template (55% reduction)
- **2025-11-08**: Created Product Prioritization Workflow
```

**Expected update:**
```markdown
## 2025

- **2025-11-09**: Added success criteria template section to Research Workflow
- **2025-11-08**: Simplified Product Prioritization template (55% reduction)
- **2025-11-08**: Created Product Prioritization Workflow
```

**Result**: ✅ PASS
- Format is correct
- Placement is correct (newest first)
- Description is concise and outcome-focused

### Simulation 2: Unsuccessful Improvement

**Context**: Attempted improvement to add timeline estimates but determined not viable after testing

**Following guidance**: "For unsuccessful improvements: note that it was attempted"

**Expected update:**
```markdown
## 2025

- **2025-11-09**: Attempted timeline estimation guidance but determined not viable after testing
- **2025-11-08**: Simplified Product Prioritization template (55% reduction)
```

**Result**: ✅ PASS
- Unsuccessful attempts are documented
- Clear note that improvement was not viable
- Still concise

### Simulation 3: Year Boundary

**Context**: Adding first entry in a new year (2026)

**Current history.md state:**
```markdown
## 2025

- **2025-12-15**: Last improvement of 2025
```

**Expected update:**
```markdown
## 2026

- **2026-01-02**: First improvement of 2026

## 2025

- **2025-12-15**: Last improvement of 2025
```

**Result**: ✅ PASS
- New year section is created
- Proper chronological organization
- Clear format

## Test Result

**Status**: PASS ✅

**Notes:**
- History tracking guidance is clear and complete
- Format is simple and well-defined
- Examples help clarify expectations
- Guidance for successful and unsuccessful improvements is provided
- Year organization is intuitive

**Observations:**
- The "one line" guideline is flexible (can wrap) but clear
- "Outcome-focused" examples are helpful
- Unsuccessful improvement guidance prevents ambiguity

**Minor Issues:**
- None identified

## Recommendations

✅ No changes needed - history tracking guidance is clear and complete.
