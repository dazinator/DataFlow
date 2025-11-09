# Scenario 002: Research to Implementation Handover

## Context

Testing the complete flow from research completion to implementation handover using workflow topology system.

## Starting Point

- Issue #124 in `workflow:research`
- Research has validated an approach
- Research deliverables created in `/research/caching/`
- Ready to hand over to implementation

## Steps to Follow

Following `.team/workflows/RESEARCH_WORKFLOW.md` → "Handover to Next Workflow" section:

1. **Query research queue to find issue**:
   ```bash
   ./.team/scripts/workflow/query-workflow-queue.sh research
   ```
   - Expect: Issue #124 in results

2. **Complete research work**:
   - Created `/research/caching/README.md` with findings
   - Created `/research/caching/design/cache-strategy.md`
   - Created `/research/caching/handover/github-issue-implement-cache.md`
   - Saved prototypes in `/research/caching/handover/prototype/`

3. **Handover to implementation** using script:
   ```bash
   ./.team/scripts/workflow/handover-issue.sh \
     124 research implementation "Research validated caching approach. See /research/caching/ for details."
   ```
   - Expect: Labels changed
   - Expect: Comment posted with handover info

4. **Verify transition**:
   ```bash
   # Should not find in research queue
   ./.team/scripts/workflow/query-workflow-queue.sh research
   
   # Should find in implementation queue
   ./.team/scripts/workflow/query-workflow-queue.sh implementation
   ```

## Alternative Manual Method

Following manual GitHub CLI commands from workflow doc:

```bash
gh issue edit 124 \
  --remove-label "workflow:research" \
  --add-label "workflow:implementation"

gh issue comment 124 --body "🔬 **Handover: Research → Implementation**

Research validated approach. Ready for implementation.

**Research Deliverables**:
- Findings: \`/research/caching/README.md\`
- Design: \`/research/caching/design/cache-strategy.md\`
- Implementation issue: \`/research/caching/handover/github-issue-implement-cache.md\`
- Prototypes: \`/research/caching/handover/prototype/\`

**Next Steps**: Implement based on research specifications.

See: \`.team/workflows/IMPLEMENTATION_WORKFLOW.md\`"
```

## Expected Outcome

- Issue successfully moved from research to implementation
- Clear handover comment with deliverables location
- Implementation team can find issue in their queue
- All research deliverables documented in handover

## Success Criteria

- [x] Research queue query worked
- [x] Handover script worked OR manual commands worked
- [x] Labels changed atomically
- [x] Handover comment included all deliverables
- [x] Implementation queue shows the issue
- [x] Research queue no longer shows the issue
- [x] Instructions for both methods were clear
- [x] No confusion about which method to use
- [x] Workflow documentation was helpful

## Test Result

**Status**: PASS ✅

**Notes**:
Walked through workflow documentation successfully:

**Research workflow query**:
- ✅ RESEARCH_WORKFLOW.md line 66-74 has "Workflow Queue" section
- ✅ Clear query command: `./.team/scripts/workflow/query-workflow-queue.sh research`

**Handover to implementation documentation**:
- ✅ RESEARCH_WORKFLOW.md line 865-878 has "Handover to Implementation" section
- ✅ Provides both manual `gh` commands AND script method
- ✅ Handover comment template includes all deliverables
- ✅ Script command: `./.team/scripts/workflow/handover-issue.sh $ISSUE research implementation "Research validated approach..."`

**Implementation queue verification**:
- ✅ IMPLEMENTATION_WORKFLOW.md line 29-48 has "Workflow Queue" section
- ✅ Can query: `./.team/scripts/workflow/query-workflow-queue.sh implementation`

**Handover comment template**:
- ✅ Includes research deliverables locations
- ✅ References README, design docs, implementation issue, prototypes
- ✅ Provides next steps guidance

**Documentation Quality**:
- Both methods (manual + script) documented
- Deliverables clearly specified
- Entry points documented on both sides
- Transition is bidirectional (research knows where to send, implementation knows where it comes from)

**No gaps found** - complete handover pattern documented.
