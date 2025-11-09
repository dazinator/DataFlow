# Scenario: Re-Triage - Misassigned Issue

## Context

Testing re-triage capability: Implementation workflow discovers issue was misassigned.

This validates the handover/re-designation mechanism.

## Starting Point

- Issue #150: labeled `workflow:implementation`
- Issue was incorrectly routed by triage
- Actually needs research, not direct implementation

## Steps to Follow

1. **Implementation Workflow Starts**
   - Query issues with `workflow:implementation` label
   - Find issue #150 in queue
   - Read issue description
   - Assess: "This needs research validation first, not direct implementation"

2. **Re-Designation Decision**
   - Decision: Re-assign to research workflow
   - Change label #150: `workflow:implementation` → `workflow:research`
   - Add comment: "Re-designating to Research Workflow. This issue requires approach validation before implementation. Reasoning: [specific details about why research is needed]"

3. **Research Workflow Picks Up**
   - Query issues with `workflow:research` label
   - Find issue #150 in queue
   - Read issue + comment history
   - Understand: "This came from implementation, needs validation"
   - Process research work

## Alternative: Re-Triage to Triage Queue

If unsure which workflow is appropriate:

1. **Implementation Workflow**
   - Change label #150: `workflow:implementation` → `workflow:triage`
   - Add comment: "Returning to triage. Unclear if this needs research or different approach."

2. **Triage Workflow**
   - Query issues with `workflow:triage` label
   - Find issue #150
   - Re-assess with additional context
   - Designate to appropriate workflow

## Expected Outcome

✅ Issue can be re-designated
✅ Comment provides reasoning
✅ Receiving workflow sees history
✅ No data loss
✅ Audit trail preserved

## Success Criteria

- [ ] Workflows can change designation
- [ ] Comment history provides context
- [ ] Re-designation is simple (label change)
- [ ] Both direct handover and re-triage work
- [ ] Receiving workflow has full context

## Test Result

**Status**: PASS (re-designation works cleanly with labels)

**Tabletop Simulation Notes**:

Simulated re-triage scenario:

**Starting State:**
- Issue #150 labeled `workflow:implementation`
- Implementation workflow queries and finds #150 in queue
- Reads issue description

**Implementation Assessment:**
- Issue says: "Implement new flow composition API"
- Implementation reads and realizes: "This needs research validation first"
- Reasoning: Multiple architectural approaches possible, need to validate best approach

**Re-Designation Action:**
Using GitHub CLI/API:
```bash
# Change label
gh issue edit 150 --remove-label "workflow:implementation" --add-label "workflow:research"

# Add comment explaining transition
gh issue comment 150 --body "Re-designating to Research Workflow. This issue requires approach validation before implementation. Multiple architectural patterns possible (builder, fluent API, functional composition). Research team should prototype and recommend approach."
```

**Research Workflow Picks Up:**
1. Queries: `gh issue list --label "workflow:research"`
2. Finds issue #150 in queue
3. Views issue with `gh issue view 150`
4. Sees comment history showing it came from implementation
5. Understands context: Needs architectural validation
6. Processes research work

**Alternative: Re-Triage Flow:**
If implementation is unsure which workflow:
```bash
# Send back to triage
gh issue edit 150 --remove-label "workflow:implementation" --add-label "workflow:triage"
gh issue comment 150 --body "Returning to triage. Unclear if this needs research, tech debt analysis, or different approach. Please reassess."
```

Triage workflow then:
1. Queries: `gh issue list --label "workflow:triage"`
2. Finds #150
3. Re-assesses with additional context from comments
4. Designates appropriately

**Key Validations:**
- ✅ Workflows can easily change designation (label edit)
- ✅ Comment history provides full context and reasoning
- ✅ Re-designation is simple and atomic
- ✅ Both direct handover and re-triage work
- ✅ Receiving workflow has full context via issue view
- ✅ GitHub label timeline shows all transitions (audit trail)

**Conclusion**: Re-designation mechanism is straightforward with labels. Comment history provides context. Supports both direct handover and re-triage patterns.
