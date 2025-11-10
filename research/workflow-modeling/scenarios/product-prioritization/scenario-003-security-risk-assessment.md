# Scenario 003: Security Vulnerability Risk Assessment

## Context

Copilot agent is triggered to perform product backlog prioritization. The backlog contains multiple security vulnerabilities with different CVE ratings and impact scopes (core vs. non-core code). The workflow must correctly perform risk assessment and assign appropriate priorities.

## Starting Point

**Trigger**: GitHub issue with title "Product Backlog Prioritization - Security Focus"

**Current Backlog** (in `/product/backlog/`):
- 12 active backlog items total:
  - 4 security vulnerabilities (various CVE ratings and scopes)
  - 2 tech debt items
  - 4 features
  - 2 bugs

**Security Items Details:**
1. `security-2025-11-08-critical-cve-core.md`
   - CVE-2024-99999, Critical rating
   - Scope: `/src/DataFlow.Core/ChannelManager.cs`
   - Category: Security

2. `security-2025-11-07-medium-cve-core.md`
   - CVE-2024-88888, Medium rating
   - Scope: `/src/DataFlow.Common/Utilities.cs`
   - Category: Security

3. `security-2025-11-06-high-cve-samples.md`
   - CVE-2024-77777, High rating
   - Scope: `/sample/WebApiExample/Startup.cs`
   - Category: Security

4. `security-2025-11-05-no-cve-tests.md`
   - No CVE reference
   - Scope: `/src/Tests.Shared/TestHelpers.cs`
   - Category: Security
   - Description: Potential security issue in test helper

## Steps to Follow

Based on `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md`:

### Step 1: Understand the Request
1. Read the triggering issue: "Product Backlog Prioritization - Security Focus"
2. Note specific guidance: Focus on security
3. Security items should be carefully assessed

### Step 2: Collect All Backlog Items
1. Navigate to `/product/backlog/`
2. List all `.md` files (12 total)
3. Read each file and extract metadata
4. Create inventory, flagging security items

### Step 3: Apply Selection Criteria

**3.1: Identify Security Vulnerabilities** ← **KEY STEP FOR THIS SCENARIO**

Process each security item with risk assessment:

**Item 1: `security-2025-11-08-critical-cve-core.md`**
- Security-related: YES (category = Security)
- CVE: CVE-2024-99999
- CVE Rating: Critical
- Scope: `/src/DataFlow.Core/` → **Core Code**
- **Risk Assessment**: Critical CVE in core code
- **Priority Assignment**: Priority 1 (Highest)
- **Action**: FLAG FOR SELECTION

**Item 2: `security-2025-11-07-medium-cve-core.md`**
- Security-related: YES
- CVE: CVE-2024-88888
- CVE Rating: Medium
- Scope: `/src/DataFlow.Common/` → **Core Code**
- **Risk Assessment**: Medium CVE in core code
- **Priority Assignment**: Priority 2 (High)
- **Action**: FLAG FOR SELECTION

**Item 3: `security-2025-11-06-high-cve-samples.md`**
- Security-related: YES
- CVE: CVE-2024-77777
- CVE Rating: High
- Scope: `/sample/` → **Non-Core Code** (sample project)
- **Risk Assessment**: High CVE but in non-core code (samples)
- **Priority Assignment**: Priority 3 (Normal) - maximum for non-core
- **Action**: Consider for selection (not automatic P1/P2)

**Item 4: `security-2025-11-05-no-cve-tests.md`**
- Security-related: YES
- CVE: None
- Scope: `/src/Tests.Shared/` → **Non-Core Code** (test code)
- **Risk Assessment**: No CVE, test code only
- **Priority Assignment**: Priority 3 (Normal) - maximum for non-core
- **Action**: Consider for selection but lower priority than CVEs

**Selection After Security Assessment:**
- MUST SELECT: Item 1 (P1), Item 2 (P2)
- Slots used: 2/5
- Items 3 and 4 are P3, compete with other items for remaining 3 slots

**3.2: Identify Tech Debt Items**
- Find 2 tech debt items (1 quick win)
- Policy requires at least 1
- **Action**: Select quick win for slot 3
- Slots used: 3/5

**3.3: Check Priority Overrides**
- Review all 12 items
- **Result**: No overrides found
- **Action**: Continue

**3.4: Fill Remaining Slots**
Remaining slots: 2
Candidates: Items 3 & 4 (security, P3), 4 features, 2 bugs

Given "Security Focus" guidance:
- Prioritize remaining security items over non-security
- Select Item 3 (High CVE in samples) - slot 4
- Select Item 4 (test security issue) - slot 5

Final selection:
1. P1: Critical CVE in core
2. P2: Medium CVE in core
3. P3: Tech Debt quick win
4. P3: High CVE in samples
5. P3: Security issue in tests

### Step 4: Generate Prioritization Tables

**Selected Items Table** (5 items):
| Priority | Backlog Item ID | Title | Category | Rationale |
|----------|----------------|-------|----------|-----------|
| 1 | security-2025-11-08-critical-cve-core | Critical CVE in DataFlow.Core | Security | Critical CVE in core code - immediate risk |
| 2 | security-2025-11-07-medium-cve-core | Medium CVE in Common Utilities | Security | Medium CVE in core code - high priority |
| 3 | techdebt-2025-11-07-dependency-updates | Update Dependencies | Tech Debt | Quick win, satisfies tech debt policy |
| 3 | security-2025-11-06-high-cve-samples | High CVE in Sample Project | Security | High CVE but non-core code (sample) |
| 3 | security-2025-11-05-no-cve-tests | Security Issue in Test Helpers | Security | Security issue, test code only |

**Assessed But Not Selected Table** (7 items):
| Backlog Item ID | Title | Category | Assessment Priority | Notes |
|----------------|-------|----------|-------------------|-------|
| techdebt-2025-11-06-refactor-pipeline | Refactor Pipeline Logic | Tech Debt | 3 | Good candidate but tech debt quick win selected |
| feature-2025-11-05-metrics-api | Add Metrics API | Feature | 3 | High value but security takes precedence |
| feature-2025-11-04-dashboard | Monitoring Dashboard | Feature | 3 | Defer due to security focus |
| bug-2025-11-03-performance-regression | Performance Regression | Bug | 3 | Important but security prioritized |
| feature-2025-11-02-documentation | Improve Documentation | Feature | 4 | Lower priority |
| bug-2025-11-01-minor-warning | Minor Compiler Warning | Bug | 5 | Low impact |
| feature-2025-10-31-experimental | Experimental Feature | Feature | 5 | Experimental, defer |

### Step 5: Update Prioritization File

Update `/product/prioritization.md`:
- Last Updated: 2025-11-08
- Updated By: Copilot Agent - Automated Prioritization
- Replace tables with new selections
- **Add detailed notes** about security risk assessments

**Notes section should include:**
```markdown
## Notes

**Security-Focused Prioritization:**
All 4 security vulnerabilities in the backlog have been included in active priorities due to "Security Focus" guidance.

**Risk Assessments Performed:**
1. **Critical CVE in Core** (P1): CVE-2024-99999 in `/src/DataFlow.Core/` - immediate risk to production code
2. **Medium CVE in Core** (P2): CVE-2024-88888 in `/src/DataFlow.Common/` - core code affected
3. **High CVE in Samples** (P3): CVE-2024-77777 in `/sample/` - non-core code, maximum P3 per policy
4. **Test Security Issue** (P3): No CVE, test code only - non-core, informational priority

**Policy Applied:**
- Core code vulnerabilities: Prioritized by CVE criticality (Critical→P1, Medium→P2)
- Non-core code vulnerabilities: Maximum P3 priority regardless of CVE rating
- Tech debt: 1 item included per policy (quick win selected)
```

### Step 6: Report Completion

Post summary comment on triggering issue:
```markdown
✅ **Backlog Prioritization Complete - Security Focused**

**Summary:**
- Total backlog items reviewed: 12
- Active priorities selected: 5
- Items assessed but not selected: 7
- Security vulnerabilities in backlog: 4 (all selected)

**Priority Breakdown:**
- Priority 1 (Highest): 1 item (Critical CVE in core code)
- Priority 2 (High): 1 item (Medium CVE in core code)
- Priority 3 (Normal): 3 items (2 Security non-core, 1 Tech Debt)

**Security Risk Assessments:**
- **P1**: Critical CVE-2024-99999 in DataFlow.Core (core code)
- **P2**: Medium CVE-2024-88888 in Common utilities (core code)
- **P3**: High CVE-2024-77777 in sample project (non-core, capped at P3)
- **P3**: Security issue in test helpers (non-core, no CVE)

**Policy Compliance:**
- Core code CVEs prioritized by criticality
- Non-core CVEs capped at P3 per policy
- Tech debt quick win included

**Updated File:** `/product/prioritization.md`
```

## Expected Outcome

After following the workflow:

1. **All security items identified** and assessed

2. **Risk assessments performed correctly**:
   - Core vs. non-core scope determined
   - CVE ratings evaluated
   - Appropriate priorities assigned

3. **Core code security items** prioritized by CVE rating:
   - Critical → P1
   - Medium → P2

4. **Non-core security items** capped at P3 regardless of CVE rating

5. **Prioritization file** contains detailed risk assessment notes

6. **Summary comment** explains security focus and assessments

## Success Criteria

- [ ] All security items correctly identified
- [ ] Core vs. non-core scope assessment instructions clear
- [ ] CVE rating to priority mapping followed correctly
- [ ] Non-core priority cap (P3) applied correctly
- [ ] Risk assessment notes detailed and helpful
- [ ] Security items take precedence per policy
- [ ] Tech debt policy still satisfied (1 item)
- [ ] Summary provides transparency on security decisions

## Test Result

**Status**: PASS

**Notes**: Scenario validated against refined workflow. Security risk assessment process is comprehensive.

### What Worked Well
- Core vs. non-core scope assessment is clearly defined
- CVE rating to priority mapping is explicit (Critical→P1, Medium→P2 for core)
- Non-core priority cap (P3) is clearly stated
- Edge case section addresses "what defines core code" explicitly
- Security items without CVE have guidance (P3 for non-core, judgment for core)
- All 4 security items in scenario are correctly assessed per workflow

### Risk Assessment Validation
✅ Critical CVE in core → P1 (correct)
✅ Medium CVE in core → P2 (correct)
✅ High CVE in samples (non-core) → P3 (correctly capped)
✅ No CVE in tests (non-core) → P3 (correct)

### Edge Case Coverage
The workflow now includes:
- Definition of what constitutes "core code" vs. "non-core"
- How to handle CVEs without severity ratings
- How to handle security issues without CVE references
- Classification guidance for ambiguous locations (/tools/, build scripts, etc.)

### Suggested Improvements
None - scenario is well-handled by current workflow.

## Edge Cases to Consider

After basic simulation:

1. **What defines "core code"?** Is `/src/Tests.Shared/` core or non-core? (Test code = non-core per workflow)
2. **CVE without rating**: How to handle CVE references with no severity rating?
3. **Multiple critical CVEs**: If 3 critical CVEs in core, do all get P1? (Yes, per policy)
4. **Security without CVE**: How to assess priority if no CVE exists? (Use P3 for non-core, judgment for core)
5. **Tooling/build code**: Is `/tools/` or build scripts core or non-core? (Non-core per workflow)
