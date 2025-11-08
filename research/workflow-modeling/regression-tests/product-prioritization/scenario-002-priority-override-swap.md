# Scenario 002: Priority Override Swap

## Context

Copilot agent is triggered to perform product backlog prioritization. The backlog contains several items already prioritized, but a new priority override has been added by the product team for a customer blocker. The workflow should detect the override and swap it into the active priorities.

## Starting Point

**Trigger**: Comment on existing issue: `@copilot please prioritize the product backlog`

**Current Backlog** (in `/product/backlog/`):
- 8 active backlog items total
- No security vulnerabilities
- 2 tech debt items
- 6 feature/bug items

**Current Prioritization State** (existing 5 items, all P3):
1. P3: Feature A
2. P3: Feature B  
3. P3: Tech Debt Quick Win
4. P3: Bug Fix
5. P3: Feature C

**New Development**: Product team added priority override to Feature D:
```markdown
**Priority Override**: 2 (Set by: Product Manager, Date: 2025-11-08, Reason: Critical customer blocker for Q1 release)
```

Feature D is currently NOT in the active priorities (in the "Assessed But Not Selected" list).

## Steps to Follow

Based on `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md`:

### Step 1: Understand the Request
1. Read the triggering comment: `@copilot please prioritize the product backlog`
2. No specific guidance provided
3. Standard prioritization requested

### Step 2: Collect All Backlog Items
1. Navigate to `/product/backlog/`
2. List all `.md` files
3. Read each file and extract metadata
4. Create inventory of 8 active items

### Step 3: Apply Selection Criteria

**3.1: Identify Security Vulnerabilities**
- **Result**: No security items found
- **Action**: Continue

**3.2: Identify Tech Debt Items**
- Find 2 tech debt items:
  - `techdebt-2025-11-07-update-dependencies.md` (quick win)
  - `techdebt-2025-11-06-improve-error-messages.md` (medium effort)
- **Action**: Select quick win for inclusion (satisfies policy)

**3.3: Check Priority Overrides** ← **KEY STEP FOR THIS SCENARIO**
- Review all 8 items for "Priority Override" field
- **Found**: `feature-2025-11-07-customer-portal.md` (Feature D)
  - Priority Override: 2
  - Set by: Product Manager
  - Reason: Critical customer blocker
- **Current selected items**: All Priority 3
- **Override priority (2)** is HIGHER than current selected priorities (3)
- **Action**: Swap Feature D into active priorities, move one P3 item out

**Swap Decision:**
- Feature D (P2 override) swaps in
- Which P3 item to remove? Select lowest value from current P3 items
- Feature C has lower business value than others
- **Action**: Feature C moves to "Assessed But Not Selected"

**3.4: Fill Remaining Slots**
After override swap, slots are:
1. Feature D (P2 - override)
2. Tech Debt Quick Win (P3)
3. Feature A (P3)
4. Feature B (P3)
5. Bug Fix (P3)

No additional changes needed - 5 slots filled.

### Step 4: Generate Prioritization Tables

**Selected Items Table** (5 items):
| Priority | Backlog Item ID | Title | Category | Rationale |
|----------|----------------|-------|----------|-----------|
| 2 | feature-2025-11-07-customer-portal | Customer Portal Feature | Feature | Priority override - critical customer blocker |
| 3 | techdebt-2025-11-07-update-dependencies | Update Dependencies | Tech Debt | Quick win, at least 1 tech debt per policy |
| 3 | feature-2025-11-06-api-versioning | API Versioning | Feature | High business value |
| 3 | feature-2025-11-05-batch-processing | Batch Processing Improvements | Feature | Performance improvement |
| 3 | bug-2025-11-04-connection-timeout | Connection Timeout Issue | Bug | Affects multiple users |

**Assessed But Not Selected Table** (3 items):
| Backlog Item ID | Title | Category | Assessment Priority | Notes |
|----------------|-------|----------|-------------------|-------|
| feature-2025-11-04-export-feature | Data Export Feature | Feature | 3 | Swapped out for P2 override item |
| techdebt-2025-11-06-improve-error-messages | Improve Error Messages | Tech Debt | 3 | Good candidate but tech debt quick win selected |
| feature-2025-11-03-ui-enhancements | UI Enhancements | Feature | 4 | Nice-to-have, defer until capacity |

### Step 5: Update Prioritization File

Update `/product/prioritization.md`:
- Last Updated: 2025-11-08
- Updated By: Copilot Agent - Automated Prioritization
- Replace Active Priorities table with new items (Feature D now included at P2)
- Update Assessed But Not Selected (Feature C now listed)
- **Important**: Add note explaining override swap in Notes section

**Notes section should include:**
```markdown
## Notes

**Priority Override Applied:**
- Feature D (`feature-2025-11-07-customer-portal`) has Priority Override = 2
- Override set by Product Manager on 2025-11-08
- Reason: Critical customer blocker for Q1 release
- Action: Swapped into active priorities, replacing Feature C (lower value P3 item)
```

### Step 6: Report Completion

Post summary comment as reply to trigger comment:
```markdown
✅ **Backlog Prioritization Complete**

**Summary:**
- Total backlog items reviewed: 8
- Active priorities selected: 5
- Items assessed but not selected: 3

**Priority Breakdown:**
- Priority 2 (High): 1 item (Feature - Priority Override)
- Priority 3 (Normal): 4 items (1 Tech Debt, 2 Features, 1 Bug)

**Priority Override Applied:**
- Feature D (Customer Portal) swapped into active priorities at P2
- Override set by Product Manager (customer blocker)
- Feature C moved to assessed but not selected

**Updated File:** `/product/prioritization.md`
```

## Expected Outcome

After following the workflow:

1. **Override detected** during Step 3.3

2. **Swap executed correctly**:
   - Feature D (P2 override) moved into active priorities
   - Feature C (P3) moved to "Assessed But Not Selected"

3. **Prioritization file updated** with:
   - Feature D at P2 in correct position (after any P1, before P3)
   - 4 other items remain at P3
   - Clear notes explaining the override swap

4. **Tech debt policy** still satisfied (1 tech debt item selected)

5. **Summary comment** clearly explains override was applied

6. **Override field NOT modified** in Feature D's backlog item (copilot must never change overrides)

## Success Criteria

- [ ] Instructions for checking overrides were clear
- [ ] Override detection logic worked correctly
- [ ] Swap logic clearly explained (which item to remove)
- [ ] Priority comparison logic correct (P2 > P3)
- [ ] Override field not modified by copilot
- [ ] Summary comment highlights override application
- [ ] Notes section explains the swap decision
- [ ] All formatting correct in updated file

## Test Result

**Status**: PASS

**Notes**: Scenario validated against refined workflow. The priority override mechanism is well-specified.

### What Worked Well
- Override detection process is clear (Step 3.3)
- Swap logic is well-explained (compare override priority to selected priorities)
- Instructions specify NOT to modify override fields (copilot must never change them)
- Summary comment template includes override notification
- Notes section template shows how to document the swap

### Edge Case Coverage
The workflow's edge case section addresses potential override issues:
- Multiple overrides exceeding slots
- Override conflicts with security items
- Malformed override fields

### Validation
✅ Override field format is specified in backlog item examples
✅ Swap decision logic is clear (higher priority override replaces lower priority selected)
✅ Which item to remove is specified (lowest value P3 item)
✅ Override never modified by copilot
✅ Swap explained in Notes section

### Suggested Improvements
None - scenario is well-handled by current workflow.

## Edge Cases to Consider

After basic simulation, consider these edge cases:

1. **Multiple overrides**: What if 3 items have P1 overrides but only 5 slots total?
2. **Override equals current priority**: P3 override when all selected are P3 - no swap needed?
3. **Override lower than selected**: P4 override when all selected are P3 - should NOT swap
4. **Override field malformed**: Missing required fields - how to handle?
