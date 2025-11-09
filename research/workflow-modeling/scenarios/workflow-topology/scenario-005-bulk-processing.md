# Scenario: Bulk Processing - Process Modeling Workflow

## Context

Testing bulk processing: Process Modeling workflow processes multiple designated issues in one PR.

This validates the integration with smart mode bulk processing patterns.

## Starting Point

- 8 issues designated to Process Modeling:
  - Issues #200-207 all labeled `workflow:process-modeling`
  - Each represents a workflow improvement suggestion
  - Process Modeling uses smart mode (max 5 items, max 500 lines)

## Steps to Follow

1. **Process Modeling Workflow Starts**
   - Query issues with `workflow:process-modeling` label
   - Find 8 issues in queue (#200-207)
   - Smart mode configuration: max 5 items, max 500 lines

2. **Process First Item (Issue #200)**
   - Process improvement
   - Update workflow documentation
   - Track lines changed: ~80 lines
   - Update issue #200:
     - Add comment: "Processed in PR #XXX. Changes: [summary]"
     - Keep label `workflow:process-modeling` (until all done)
   - Continue to next item

3. **Process Items #201-204**
   - Process each improvement
   - Update workflows
   - Track cumulative lines changed
   - After #204: ~450 lines total

4. **Check Stopping Criteria**
   - Items processed: 5 (hit max items limit)
   - Lines changed: ~450 (below max lines limit)
   - Decision: STOP (max items reached)

5. **Finalize PR**
   - Close processed issues #200-204
   - Leave issues #205-207 with label `workflow:process-modeling` (still in queue)
   - PR description: Summary of 5 improvements processed
   - Merge PR

6. **Next Run**
   - Process Modeling queries again
   - Find remaining issues #205-207 in queue
   - Process these in next PR

## Expected Outcome

✅ Bulk processing works
✅ Smart mode stopping criteria applied
✅ Processed issues closed
✅ Unprocessed issues remain in queue
✅ Next run picks up remaining items

## Success Criteria

- [ ] Workflow can query multiple designated issues
- [ ] Smart mode integrates with label-based queue
- [ ] Processed issues can be closed
- [ ] Unprocessed issues remain in queue for next run
- [ ] Clear audit trail of what was processed

## Test Result

**Status**: PASS (bulk processing integrates with label-based queues)

**Tabletop Simulation Notes**:

Simulated bulk processing integration:

**Setup:**
8 issues in queue, all labeled `workflow:process-modeling`:
- #200: Improve research handover template
- #201: Add tech debt verification guidance
- #202: Clarify implementation baseline artifacts
- #203: Update product prioritization criteria
- #204: Add process modeling design doc template
- #205: Improve triage workflow clarity
- #206: Add workflow navigation guide
- #207: Update self-improvement evaluation checklist

**Process Modeling Workflow Execution:**

```bash
# Query designated issues
gh issue list --label "workflow:process-modeling" --json number,title,url

# Returns 8 issues (#200-207)
```

**Smart Mode Processing:**

**Item 1: Issue #200**
- Process improvement
- Update `/team/workflows/RESEARCH_WORKFLOW.md`
- Lines changed: ~80 lines
- Comment on #200: "Processed in PR #XXX. Updated handover template with clearer prototype guidance."
- **Keep issue open** (will close after PR merged)

**Item 2: Issue #201**
- Process improvement  
- Update `/team/workflows/TECH_DEBT_WORKFLOW.md`
- Lines changed: ~75 lines (cumulative: 155)
- Comment on #201: "Processed in PR #XXX. Added verification check section."

**Items 3-5: Issues #202-204**
- Process each
- Cumulative lines after #204: ~450 lines
- Items processed: 5

**Check Stopping Criteria:**
- Items processed: 5 (= max items limit) ✅ STOP
- Lines changed: ~450 (< 500 max lines limit)
- Stop reason: "Max items threshold (5)"

**PR Finalization:**
```bash
# Close processed issues
gh issue close 200 201 202 203 204 --comment "Completed in PR #XXX"

# Issues #205-207 remain open with label "workflow:process-modeling"
```

**Next Run:**
```bash
# Query again
gh issue list --label "workflow:process-modeling"

# Returns 3 issues (#205-207) - ready for next batch
```

**Key Validations:**
- ✅ Workflow can query all designated issues at once
- ✅ Smart mode stopping criteria apply (existing pattern)
- ✅ Processed issues closed (removed from queue)
- ✅ Unprocessed issues remain in queue (label unchanged)
- ✅ Next run picks up remaining items automatically
- ✅ Clear audit trail via issue comments
- ✅ Integrates with existing bulk processing patterns

**Conclusion**: Label-based queues integrate perfectly with existing smart mode bulk processing patterns. Query returns all designated issues, workflow applies stopping criteria, processes N items, closes them, remaining items stay in queue for next run.
