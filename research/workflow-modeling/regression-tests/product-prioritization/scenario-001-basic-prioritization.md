# Scenario 001: Basic Prioritization with Mixed Items

## Context

Copilot agent is triggered to perform product backlog prioritization. The backlog contains a mix of features, bugs, tech debt, and one security vulnerability. No priority overrides are set.

## Starting Point

**Trigger**: GitHub issue created with title "Product Backlog Prioritization - Monthly Review"

**Current Backlog** (in `/product/backlog/`):
- 10 active backlog items total:
  - 1 security vulnerability (CVE-2024-12345, Critical, in core code)
  - 3 tech debt items (1 quick win, 2 larger efforts)
  - 4 feature requests (various value levels)
  - 2 bug fixes (1 high impact, 1 low impact)

**Current Prioritization State**: Previous prioritization exists with 5 items, but new items have been added since then.

## Steps to Follow

Based on `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md`:

### Step 1: Understand the Request
1. Read the triggering GitHub issue
2. Note it's a monthly review with no specific guidance
3. No constraints mentioned

### Step 2: Collect All Backlog Items
1. Navigate to `/product/backlog/`
2. List all `.md` files
3. Read each file and extract metadata:
   - Backlog ID
   - Source
   - Category
   - Status (filter to "Active" only)
   - Priority Override (check for any)
4. Create inventory list of 10 active items

### Step 3: Apply Selection Criteria

**3.1: Identify Security Vulnerabilities**
- Find: `security-2025-11-08-critical-cve-core.md`
- Category: Security
- CVE: CVE-2024-12345, Critical rating
- Scope: Core code (`/src/DataFlow.Core/`)
- **Risk Assessment**: Critical CVE in core code → Priority 1
- **Action**: Flag for selection

**3.2: Identify Tech Debt Items**
- Find 3 tech debt items:
  - `techdebt-2025-11-07-add-enumerator-cancellation.md` (quick win)
  - `techdebt-2025-11-06-refactor-channel-abstractions.md` (larger effort)
  - `techdebt-2025-11-05-modernize-logging.md` (medium effort)
- **Action**: Select the quick win for inclusion (satisfies "at least 1 tech debt" policy)

**3.3: Check Priority Overrides**
- Review all 10 items for "Priority Override" field
- **Result**: No overrides found
- **Action**: Continue with standard criteria

**3.4: Fill Remaining Slots**
After security (1) and tech debt (1), 3 slots remain:
- Review features and bugs based on:
  - Business value
  - Impact
  - Effort vs. value
- Select:
  - `feature-2025-11-08-flow-composability.md` (high business value)
  - `bug-2025-11-07-memory-leak-high-load.md` (high impact)
  - `feature-2025-11-06-metrics-dashboard.md` (user-requested)
- All assigned Priority 3 (Normal)

### Step 4: Generate Prioritization Tables

**Selected Items Table** (5 items):
| Priority | Backlog Item ID | Title | Category | Rationale |
|----------|----------------|-------|----------|-----------|
| 1 | security-2025-11-08-critical-cve-core | Critical CVE in DataFlow.Core | Security | Critical CVE in core code - immediate risk |
| 3 | techdebt-2025-11-07-add-enumerator-cancellation | Add EnumeratorCancellation Attributes | Tech Debt | Quick win, at least 1 tech debt per policy |
| 3 | feature-2025-11-08-flow-composability | Flow Composability Unification | Feature | High business value, critical for v2.0 |
| 3 | bug-2025-11-07-memory-leak-high-load | Memory Leak Under High Load | Bug | High impact, affects production users |
| 3 | feature-2025-11-06-metrics-dashboard | Add Metrics Dashboard | Feature | User-requested, clear value |

**Assessed But Not Selected Table** (5 items):
| Backlog Item ID | Title | Category | Assessment Priority | Notes |
|----------------|-------|----------|-------------------|-------|
| techdebt-2025-11-06-refactor-channel-abstractions | Refactor Channel Abstractions | Tech Debt | 3 | Larger effort, defer after quick win |
| techdebt-2025-11-05-modernize-logging | Modernize Logging Infrastructure | Tech Debt | 3 | Good candidate but current priorities take precedence |
| feature-2025-11-05-add-retry-policies | Add Retry Policies | Feature | 3 | Nice-to-have, defer until capacity |
| feature-2025-11-04-streaming-aggregation | Streaming Aggregation Support | Feature | 4 | Lower business priority currently |
| bug-2025-11-03-minor-ui-glitch | Minor UI Glitch in Samples | Bug | 5 | Low impact, samples only |

### Step 5: Update Prioritization File

Update `/product/prioritization.md`:
- Last Updated: 2025-11-08
- Updated By: Copilot Agent - Automated Prioritization
- Replace Active Priorities table with new selected items
- Add Assessed But Not Selected section
- Add notes about criteria applied

### Step 6: Report Completion

Post summary comment on triggering issue:
```markdown
✅ **Backlog Prioritization Complete**

**Summary:**
- Total backlog items reviewed: 10
- Active priorities selected: 5
- Items assessed but not selected: 5

**Priority Breakdown:**
- Priority 1 (Highest): 1 item (Security - Critical CVE)
- Priority 3 (Normal): 4 items (1 Tech Debt, 2 Features, 1 Bug)

**Notable Selections:**
- Critical security vulnerability in core code prioritized as P1
- Tech debt quick win included per policy
- High-impact bug fix and user-requested features selected

**Updated File:** `/product/prioritization.md`
```

## Expected Outcome

After following the workflow:

1. **Prioritization file updated** with:
   - 5 selected items in correct priority order
   - Complete "Assessed But Not Selected" table
   - Clear rationale for all selections
   - Policy compliance noted

2. **Security vulnerability** correctly identified and prioritized as P1

3. **Tech debt policy** satisfied (1 tech debt item selected)

4. **Remaining slots** filled with highest value items

5. **Summary comment** posted with clear breakdown

6. **No errors or confusion** during execution

## Success Criteria

- [ ] Instructions were clear and unambiguous
- [ ] No gaps or missing information
- [ ] Workflow led to expected outcome
- [ ] No confusion or back-tracking needed
- [ ] Security risk assessment performed correctly
- [ ] Tech debt policy applied correctly
- [ ] Prioritization file properly formatted
- [ ] Summary comment provides useful information

## Test Result

**Status**: PASS (After Refinement)

**Notes**: Workflow was refined based on initial simulation findings. All identified issues have been addressed:

1. ✅ Security item identification criteria now explicit (Category/Filename/CVE content)
2. ✅ "Quick win" definition added with concrete criteria
3. ✅ Decision framework added for standard selection
4. ✅ File update process fully specified
5. ✅ Edge cases section added with comprehensive scenarios

### What Worked Well
- Overall workflow structure is logical and easy to follow
- Security risk assessment process is well-defined
- Priority override mechanism is clear
- Tech debt policy (at least 1 item) is explicit
- MAX_SELECTED_ITEMS = 5 is clearly defined
- Templates for tables and comments are excellent
- Step-by-step structure makes execution straightforward
- **Edge cases now handled comprehensively**
- **Decision framework provides clear tie-breaker logic**

### What Didn't Work Well
(All issues resolved in refinement)

### Suggested Improvements
(All improvements implemented in workflow)
