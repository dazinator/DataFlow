# Scenario 005: Triage - Supersedence Check

## Test Type
Process Modeling - Feedback Backlog Triage

## Scenario Description
Testing Rule 6: Supersedence check before processing each item.

Before processing a feedback issue, check if a more recent feedback covers the same workflow/area. If so, close the older issue with a reference to the newer one to avoid duplicate work.

## Given (Initial State)

**Mock Processing Queue** (after sorting):

```python
processing_queue = [
    # Issue A: Older feedback about triage workflow
    {
        "number": 265,
        "created_at": "2025-11-07T10:00:00Z",
        "priority": "P2",
        "title": "[Feedback] Improve Triage Workflow Examples (2025-11-07)",
        "body": """
        **Workflow**: Triage Workflow
        
        ### Suggested Improvement
        1. Add more examples to triage documentation
        2. Improve decision tree clarity
        """
    },
    
    # Issue B: Newer feedback about the SAME workflow
    {
        "number": 276,
        "created_at": "2025-11-10T11:00:00Z",
        "priority": "P1",
        "title": "[Feedback] Enhance Triage Workflow Decision Process (2025-11-10)",
        "body": """
        **Workflow**: Process Modeling Workflow  # Note: Actually about Triage improvements
        
        ### Suggested Improvement
        1. Add Quick Start anchor link to decision tree
        2. Add table of contents to TRIAGE_EXAMPLES.md (617 lines now!)
        3. Add scenario count guidance
        4. Improve visual diagrams accessibility
        5. Create quality tracking template
        """
    }
]
```

**Analysis**:
- Both issues address Triage Workflow improvements
- #276 is MORE RECENT (2025-11-10 vs 2025-11-07)
- #276 has MORE COMPREHENSIVE suggestions (5 vs 2)
- #276 includes context that #265 lacks (617 lines of examples now exist)
- #276 supersedes #265

**Workflow State**:
- Agent is about to process issue #265 (older)
- Should perform supersedence check first

## When (Action)

Agent performs supersedence check on #265:

1. **Identify topic**: Issue #265 is about "Triage Workflow"
2. **Search for related issues**: Look for other feedback about Triage Workflow
3. **Compare dates**: Find #276 (2025-11-10) is newer than #265 (2025-11-07)
4. **Assess overlap**: Both suggest improving triage documentation/examples
5. **Decision**: #276 supersedes #265 (newer, more comprehensive)
6. **Action**: Close #265 with reference to #276

## Then (Expected Outcome)

### Expected Actions

1. **Close Older Issue**:
   ```python
   issue_write(
       method="update",
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=265,
       state="closed",
       state_reason="not_planned"  # Superseded, not completed
   )
   ```

2. **Add Supersedence Comment**:
   ```python
   add_issue_comment(
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=265,
       body="[Copilot-Workflow: Process Modeling] This feedback has been superseded by #276 which covers the same area with more recent context and comprehensive suggestions. Closing in favor of the newer feedback."
   )
   ```

3. **Skip Processing**: Remove #265 from queue, continue with next item

4. **Process Newer Issue**: #276 remains in queue and gets processed normally

### Success Criteria
- ✅ Older issue #265 is closed
- ✅ Closed with state_reason="not_planned" (not "completed")
- ✅ Comment explains supersedence and links to #276
- ✅ #265 is NOT processed (saves time)
- ✅ #276 remains in queue for normal processing
- ✅ No duplicate work on same improvement area

### Edge Cases to Consider

**When NOT to close as superseded**:
- Issues cover DIFFERENT aspects of the workflow
- Older issue has valuable unique suggestions
- Newer issue doesn't fully address older issue's concerns

**Example - Should NOT close**:
```
Old issue: "Improve error handling in triage workflow"
New issue: "Add visual diagrams to triage workflow"
→ Different concerns, both valuable
```

## Test Result

**Status**: ⏳ PENDING (waiting for tabletop simulation)

**Actual Outcome**: [To be filled during simulation]

**Notes**: [Any observations during testing]

## Regression Test Value

**Retain as regression test?** YES
- Tests important deduplication logic
- Prevents wasted effort on duplicate improvements
- Shows judgment call (when to supersede vs keep both)
- Real scenario from actual backlog (#265 and #276)
