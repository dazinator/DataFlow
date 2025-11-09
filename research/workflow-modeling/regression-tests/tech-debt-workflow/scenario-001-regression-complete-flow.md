# Scenario: Regression - Complete Tech Debt to Implementation Flow

## Context

Regression test validating the complete flow from tech debt discovery through to implementation, ensuring all workflow changes integrate correctly. This scenario should be run when making future changes to Tech Debt, Implementation, or Product Prioritization workflows.

## Starting Point

- Tech debt discovery agent completes analysis
- Implementation agent picks up a tech debt item from product backlog
- Following updated workflows

## Steps to Follow

### Tech Debt Discovery Phase

1. ✅ Agent creates research folder: `research/tech-debt-[YYYY-MM-DD]/`
2. ✅ Agent reviews existing backlog: `ls -lt product/backlog/techdebt-*.md`
   - **VERIFY**: Uses `/product/backlog/` (not `/research/backlog/`)
3. ✅ Agent performs systematic exploration
4. ✅ Agent creates findings report WITHOUT reviewer decision checkboxes
   - **VERIFY**: No "Reviewer Decision" section with "Implement Now", "Backlog", "Won't Fix" checkboxes
5. ✅ Agent creates findings with verification checks:
   ```markdown
   ## Verification Check
   
   ```bash
   dotnet build 2>&1 | grep "CS0436" | wc -l
   # Expected: ~15 warnings
   ```
   ```
   - **VERIFY**: Each finding includes executable verification check
6. ✅ Agent creates ALL findings as backlog items in `/product/backlog/`:
   - `techdebt-[YYYY-MM-DD]-finding-001.md`
   - `techdebt-[YYYY-MM-DD]-finding-002.md`
   - etc.
   - **VERIFY**: ALL findings go to product backlog (no selection phase)
7. ✅ Agent reverts exploratory code
8. ✅ Agent commits documentation and backlog items
9. ✅ NO reviewer selection phase
   - **VERIFY**: Phase 4 "Reviewer Evaluation and Selection" does NOT exist

### Product Prioritization Phase

10. ✅ Product team reviews `/product/backlog/`
11. ✅ Product team creates `/product/prioritization.md` with ordered list
12. ✅ Top priority item selected

### Implementation Phase

13. ✅ Implementation agent assigned to implement top priority item
14. ✅ Agent reads backlog item: `/product/backlog/techdebt-[YYYY-MM-DD]-finding-001.md`
15. ✅ Agent reaches Step 0 - Handover Critical Review
16. ✅ Agent sees "Tech Debt Verification" in checklist
    - **VERIFY**: "Tech Debt Verification" section exists in Step 0 checklist
17. ✅ Agent executes verification check from backlog item:
    ```bash
    dotnet build 2>&1 | grep "CS0436" | wc -l
    ```
18. **Scenario A - Tech Debt Exists**:
    - Result: 15 warnings (matches expectation)
    - ✅ Agent continues with implementation
    - ✅ Agent completes work
    - ✅ Agent updates backlog item status to "Completed"
    - ✅ Agent archives to `/product/resolved/`

19. **Scenario B - Tech Debt Already Fixed**:
    - Result: 0 warnings (tech debt gone)
    - ✅ Agent STOPS implementation
    - ✅ Agent updates backlog item status to "Resolved (Already Fixed)"
    - ✅ Agent archives to `/product/resolved/`
    - ✅ Agent comments on issue explaining tech debt was already addressed

## Expected Outcome

Complete integration:
- Tech debt workflow creates ALL items in product backlog (no selection)
- Product backlog serves as single source of truth
- Product Prioritization workflow handles ordering
- Implementation workflow includes verification check
- Early detection prevents wasted effort on already-fixed issues

## Success Criteria

- [x] Tech debt discovery creates all items in `/product/backlog/`
- [x] No reviewer selection phase blocking tech debt workflow
- [x] Product team controls prioritization separately
- [x] Implementation team executes verification check at Step 0
- [x] Clear handling of both verification outcomes
- [x] Workflow integration is seamless

## Verification Points

**Tech Debt Workflow:**
- [ ] Uses `/product/backlog/` (not `/research/backlog/`)
- [ ] No Phase 4 "Reviewer Evaluation and Selection"
- [ ] Findings report has no "Reviewer Decision" checkboxes
- [ ] All findings include verification checks
- [ ] All findings go to product backlog

**Implementation Workflow:**
- [ ] Step 0 includes "Tech Debt Verification" checklist item
- [ ] Clear example of verification check execution
- [ ] Clear guidance for both outcomes (exists/already fixed)

## Test Result

**Status**: PASS

**Last Tested**: 2025-11-09

**Notes**:
- Complete flow works correctly with all changes
- Clear separation of concerns:
  - Tech Debt Discovery: Comprehensive discovery, all to backlog
  - Product Prioritization: Selection and ordering
  - Implementation: Verification then execution
- Verification check prevents wasted effort
- All three workflows integrate cleanly
- No blocking dependencies on reviewer availability
- Product backlog is single source of truth for all work items
