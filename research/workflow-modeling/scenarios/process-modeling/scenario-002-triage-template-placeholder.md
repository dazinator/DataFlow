# Scenario 002: Triage - Template Placeholder Artifact

## Test Type
Process Modeling - Feedback Backlog Triage

## Scenario Description
Testing Rule 2: Template Placeholder check during bulk processing triage.

A feedback issue is a template artifact from migration with placeholder text and no real content. The triage procedure should identify this and close the issue as "not planned".

## Given (Initial State)

**Mock Feedback Issue #259**:
```
Title: [Feedback] #[number] (YYYY-MM-DD)

Body:
## Workflow Feedback Entry

**Date**: YYYY-MM-DD
**Issue/PR**: #[number]
**Workflow**: [Workflow Name]

### What Worked Well
[List positives]

### What Didn't Work Well
[List issues]

### Suggested Improvement
[Specific improvement]
```

**Workflow State**:
- Agent is processing bulk process modeling queue
- Reached Step 3.5: Triage Feedback Backlog
- Applying Rule 2: Template Placeholder check

## When (Action)

Agent applies triage Rule 2:
1. Read issue #259
2. Check title for "Template placeholder" OR check body for only placeholder text
3. Finding: Title contains "#[number]" and "YYYY-MM-DD" (placeholders)
4. Finding: Body contains only template placeholders: "YYYY-MM-DD", "#[number]", "[Workflow Name]", "[List positives]", etc.
5. Action: Close issue as "not planned" with comment

## Then (Expected Outcome)

### Expected Actions
1. **Close Issue as Not Planned**:
   ```python
   issue_write(
       method="update",
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=259,
       state="closed",
       state_reason="not_planned"  # Important: distinguish from completed
   )
   ```

2. **Add Comment**:
   ```python
   add_issue_comment(
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=259,
       body="[Copilot-Workflow: Process Modeling] This appears to be a template artifact from migration. Closing as not actionable."
   )
   ```

3. **Skip Processing**: Issue is NOT added to processing queue

### Success Criteria
- ✅ Issue is closed (state="closed")
- ✅ Closed with state_reason="not_planned" (not "completed")
- ✅ Comment explains why (template artifact)
- ✅ Issue is NOT in the processing queue for Step 4
- ✅ Reduces noise in backlog

## Test Result

**Status**: ⏳ PENDING (waiting for tabletop simulation)

**Actual Outcome**: [To be filled during simulation]

**Notes**: [Any observations during testing]

## Regression Test Value

**Retain as regression test?** YES
- Common after migrations
- Tests template detection logic
- Clear pass/fail criteria
- Prevents wasted effort on empty issues
