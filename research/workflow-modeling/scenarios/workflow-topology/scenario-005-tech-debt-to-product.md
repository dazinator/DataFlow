# Scenario 005: Tech Debt to Product Prioritization

## Context

Testing the workflow where tech debt analysis completes and creates backlog items that need prioritization.

## Starting Point

- Issue #127 in `workflow:tech-debt`
- Tech debt analysis complete
- Created 5 backlog items in `/product/backlog/`
- Items need prioritization by product team

## Steps to Follow

Following `.team/prompts/TECH_DEBT_WORKFLOW.md` → "Handover to Product Prioritization" section:

1. **Tech debt analysis complete**:
   - Explored codebase for debt
   - Created findings report: `/research/tech-debt-2025-11-09/findings.md`
   - Created 5 backlog items:
     * `techdebt-2025-11-09-modernize-namespaces.md`
     * `techdebt-2025-11-09-reduce-warnings.md`
     * `techdebt-2025-11-09-add-test-helpers.md`
     * `techdebt-2025-11-09-api-documentation.md`
     * `techdebt-2025-11-09-scaffolding-tool.md`

2. **Handover to product prioritization**:
   ```bash
   ./.github/scripts/workflow/handover-issue.sh \
     127 tech-debt product-backlog "Tech debt analysis complete. Created 5 backlog items for prioritization. See /product/backlog/ and /research/tech-debt-2025-11-09/findings.md"
   ```

3. **Verify transition**:
   ```bash
   # Should no longer be in tech-debt queue
   ./.github/scripts/workflow/query-workflow-queue.sh tech-debt
   
   # Should be in product-backlog queue
   ./.github/scripts/workflow/query-workflow-queue.sh product-backlog
   ```

4. **Product team picks up**:
   Following `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md`:
   - Query product-backlog queue finds issue #127
   - Review backlog items created
   - Apply prioritization policy
   - Select top 5 items (may include some tech debt items)
   - Hand high-priority items to implementation

## Expected Outcome

- Issue successfully handed to product team
- Product team can prioritize tech debt items
- Backlog items are ready for prioritization
- Clear separation between discovery and prioritization

## Success Criteria

- [x] Tech debt workflow handover pattern is clear
- [x] Handover script worked
- [x] Issue moved to product-backlog
- [x] Handover comment references backlog items
- [x] Product prioritization can query and find issue
- [x] Backlog items are accessible
- [x] Workflow transition makes sense

## Test Result

**Status**: PASS ✅

**Notes**:
Walked through workflow documentation successfully:

**Tech debt handover to product**:
- ✅ TECH_DEBT_WORKFLOW.md line 827-837 has "Handover to Product Prioritization" section
- ✅ Clear "When" criteria: "Tech debt analysis complete, backlog items created and need prioritization"
- ✅ Script command: `./.github/scripts/workflow/handover-issue.sh $ISSUE tech-debt product-backlog "Tech debt analysis complete. Created [N] backlog items..."`

**Handover comment requirements**:
- ✅ Documentation specifies what to include:
  * Number of backlog items created
  * Location: `/product/backlog/`
  * Findings report location
- ✅ Clear and complete handover

**Product prioritization reception**:
- ✅ PRODUCT_PRIORITIZATION_WORKFLOW.md line 13-30 has "Workflow Queue" section
- ✅ Entry points include "From Tech Debt workflow (debt items need priority)"
- ✅ Can query: `./.github/scripts/workflow/query-workflow-queue.sh product-backlog`

**Process separation**:
- ✅ Clear that tech debt doesn't go straight to implementation
- ✅ Product team controls prioritization
- ✅ Tech debt items compete with features/bugs
- ✅ Proper governance

**Documentation Quality**:
- Handover pattern is complete
- Deliverables clearly specified
- Both workflows reference each other
- Separation of concerns is maintained
- Prioritization policy applies equally

**No gaps found** - proper workflow separation and handover documented.

## Observations

This scenario validates:
- Tech debt doesn't go straight to implementation
- Product team controls prioritization
- Tech debt items compete with features/bugs
- Clear handover with deliverables
