# Product Prioritization Workflow

---

## Workflow Queue

**Query issues designated to this workflow:**

**For Copilot Agents** (use MCP tools):
```python
list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:product-backlog"],
    state="OPEN"
)
```

**For Manual/CI Use** (GitHub CLI):
```bash
gh issue list \
  --label "workflow:product-backlog" \
  --state open \
  --json number,title,url
```

**Entry Points:**
- From Triage workflow (needs prioritization)
- From Research workflow (research complete, needs priority)
- From Tech Debt workflow (debt items need priority)
- From periodic backlog reviews

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete documentation on querying and handover patterns.

---

## Overview

This workflow defines how to automatically prioritize the product backlog based on established policy criteria. It ensures that the most valuable and urgent work items are selected for implementation while maintaining a manageable active priority list.

## When to Use This Workflow

Use this workflow when:
- Manually prioritizing the product backlog
- Responding to a backlog prioritization issue or comment request
- Re-evaluating priorities after new items are added
- Conducting periodic backlog reviews

**Trigger Methods:**
1. **GitHub Issue**: Create an issue with "Product Backlog Prioritization" in the title
2. **Comment**: Comment on an existing issue with `/prioritize-backlog` or similar trigger phrase

## Prioritization Policy

### Maximum Selected Items

**MAX_SELECTED_ITEMS = 5**

The active priority list is limited to 5 items to maintain focus and ensure clear priorities for the implementation team.

### Priority Levels

- **1** = Highest priority (critical, blocking, time-sensitive)
- **2** = High priority (important, significant value)
- **3** = Normal priority (default for most work)
- **4** = Lower priority (nice-to-have)
- **5** = Lowest priority (defer unless capacity allows)

**Default Priority**: All selected items default to **Priority 3 (Normal)** unless criteria below apply.

### Selection Criteria (In Priority Order)

#### 1. Critical Security Vulnerabilities (Highest Priority)

Security vulnerabilities take precedence over all other work.

**Risk Assessment Process:**
1. Identify if item is security-related (check category, CVE references, security keywords)
2. Assess impact scope:
   - **Core Code**: Production code in `/src` (excluding test, tooling, sample projects)
   - **Non-Core**: Test code, tooling, sample projects, documentation
3. For **Core Code** vulnerabilities:
   - Check CVE criticality rating (if available)
   - **Critical/High CVE** → Priority 1 (Highest)
   - **Medium CVE** → Priority 2 (High)
   - **Low CVE or No CVE** → Priority 3 (Normal)
4. For **Non-Core** vulnerabilities:
   - Maximum Priority 3 (Normal)
   - Unless explicitly overridden

**Example:**
- CVE with Critical rating in `/src/DataFlow.Core/` → Priority 1
- Security issue in `/sample/` → Priority 3 (unless overridden)

#### 2. Tech Debt Items

When tech debt items exist in the backlog, aim to include **at least 1** in the selected items.

**Rationale**: Prevents tech debt accumulation by ensuring regular cleanup work.

**Selection Strategy:**
- Review all tech debt items (source: `techdebt-*`)
- Prefer "quick wins" - small effort with clear value
- Consider impact and dependencies
- Select at least 1 tech debt item if space allows in the 5-item limit

#### 3. Priority Override Mechanism

Backlog items can have an **override priority** set by humans (product team, stakeholders).

**Override Priority Field:**
Add to backlog item metadata:
```markdown
**Priority Override**: [1-5] (Set by: [Name], Date: YYYY-MM-DD, Reason: [Brief rationale])
```

**Override Processing:**
1. Review all backlog items for priority overrides
2. For items with overrides:
   - If override priority is HIGHER than currently selected items → Swap it in
   - If no room (all selected items equal or higher priority) → Leave as is
3. Overrides should NEVER be adjusted by copilot unless explicitly requested by human

**Example:**
- Selected items: P3, P3, P3, P3, P3
- Non-selected item with override: P2
- Action: Swap out one P3 item for the P2 override item

#### 4. Standard Selection Criteria

For remaining slots (after security, tech debt, and overrides):
- Business value and impact
- Dependencies and blockers
- Effort vs. value ratio
- Stakeholder requests
- Alignment with product roadmap

### Assessment Table

Maintain two tables in `/product/prioritization.md`:

#### Selected Items Table (Max 5)

| Priority | Backlog Item ID | Title | Category | Rationale |
|----------|----------------|-------|----------|-----------|
| 1 | ... | ... | Security | Critical CVE in core code |
| 2 | ... | ... | Feature | High business value |
| 3 | ... | ... | Tech Debt | Quick win cleanup |
| 3 | ... | ... | Feature | User-requested |
| 3 | ... | ... | Bug Fix | Affects multiple users |

#### Assessed But Not Selected

Items that were reviewed but not chosen for active prioritization.

| Backlog Item ID | Title | Category | Assessment Priority | Notes |
|----------------|-------|----------|-------------------|-------|
| ... | ... | ... | 3 | Good candidate but current priorities take precedence |
| ... | ... | ... | 4 | Nice-to-have, defer until capacity |
| ... | ... | ... | 5 | Low impact, archive candidate |

**Purpose**: Provides transparency about what was considered and why it wasn't selected.

## Workflow Steps

### For Copilot Agents

When triggered to prioritize the backlog:

#### Step 1: Understand the Request

1. Read the issue or comment that triggered prioritization
2. Check if specific guidance is provided (e.g., "prioritize security items")
3. Note any human input or constraints

#### Step 2: Collect All Backlog Items

Query all backlog GitHub issues:

```python
# Get all open backlog issues
list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:product-backlog"],
    state="OPEN"
)
```

For each backlog issue:
1. Read the issue completely
2. Extract key metadata from issue body and labels:
   - Issue number
   - Source (research/tech-debt/ad-hoc - from labels)
   - Category (from labels or issue body)
   - Status (OPEN issues are active)
   - Priority labels (if set)
3. Create an inventory list

#### Step 3: Apply Selection Criteria

**3.1: Identify Security Vulnerabilities**

Search for security items using these criteria:
1. **Category field** equals "Security" OR
2. **Filename** starts with "security-" OR
3. **Content** contains CVE references (search for "CVE-")

For each security item found:
- Perform risk assessment (core vs. non-core code)
- Assign priority based on CVE criticality
- Flag for selection if Priority 1 or 2

**3.2: Identify Tech Debt Items**

Filter items with source = "Tech Debt" OR category = "Tech Debt"

**Quick Win Criteria:**
A tech debt item qualifies as a "quick win" if it meets ANY of:
- Effort estimate ≤ 1 day
- Notes section explicitly states "quick win"
- Description indicates small, focused change with clear value
- Low effort indicators in content

Select at least 1 tech debt item for inclusion, preferring quick wins when available.

**3.3: Check Priority Overrides**
- Review all items for "Priority Override" field
- Sort override items by priority level
- Compare to currently selected items
- Swap in higher priority overrides if applicable

**3.4: Fill Remaining Slots**

After security, tech debt, and overrides processed, fill remaining slots using this decision framework:

**Decision Framework:**
1. **Production-impacting bugs** > Features (bugs that affect production users take priority)
2. **User-requested features** > Internal improvements
3. **High-value, low-effort** > High-value, high-effort (when comparing similar items)
4. **When priorities are equal**: Use creation date (older items first)

Select based on:
- Business value and impact
- Dependencies and blockers
- Effort vs. value ratio
- Stakeholder input

Assign Priority 3 (Normal) unless other criteria apply.

#### Step 4: Generate Prioritization Tables

Create two tables as defined in policy (see Assessment Table section).

**Selected Items Table:**
- Maximum 5 items
- Sort by priority (1 → 5)
- Include rationale for each selection

**Assessed But Not Selected Table:**
- All remaining "Active" backlog items
- Show assessment priority (default 3)
- Brief notes on why not selected

#### Step 5: Update Prioritization File

Update `/product/prioritization.md` following this process:

**File Update Process:**
1. If `/product/prioritization.md` doesn't exist, create from template below
2. Update "Last Updated" date to current date
3. Update "Updated By" field (e.g., "Copilot Agent - Automated Prioritization")
4. **Replace** entire "Active Priorities" table with new selected items
5. **Replace** entire "Assessed But Not Selected" section (or create if missing)
6. **Append** to Notes section (preserve previous notes if they provide valuable context)
7. Do **NOT** modify "Priority Legend" or "Prioritization Policy" sections (these are static)

**Template:**
```markdown
# Product Backlog Prioritization

**Last Updated**: YYYY-MM-DD
**Updated By**: Copilot Agent - Automated Prioritization

## Active Priorities (Max 5)

These items are approved for immediate implementation. Implementation team should select from this list.

| Priority | Backlog Item ID | Title | Category | Rationale |
|----------|----------------|-------|----------|-----------|
| ... | ... | ... | ... | ... |

## Assessed But Not Selected

These items were reviewed during prioritization but are not currently selected for active work.

| Backlog Item ID | Title | Category | Assessment Priority | Notes |
|----------------|-------|----------|-------------------|-------|
| ... | ... | ... | 3 | ... |

## Priority Legend

- **1** = Highest priority (critical, blocking, time-sensitive)
- **2** = High priority (important, significant value)
- **3** = Normal priority (default for most work)
- **4** = Lower priority (nice-to-have)
- **5** = Lowest priority (defer unless capacity allows)

## Prioritization Policy

This prioritization follows the policy defined in `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md`:
- Critical security vulnerabilities take precedence
- At least 1 tech debt item included when available
- Priority overrides honored
- Maximum 5 selected items maintained

## Notes

[Add any specific notes about this prioritization cycle:
- Special considerations applied
- Items that were close calls
- Changes from previous prioritization
- etc.]
```

#### Step 6: Report Completion

After updating the prioritization file:

1. Commit changes to `/product/prioritization.md`
2. Comment on the triggering issue/PR with summary:
   - Number of items prioritized
   - Priority breakdown (how many P1, P2, P3, etc.)
   - Notable selections (security items, tech debt, overrides applied)
   - Link to updated prioritization file
3. If triggered by comment, respond with confirmation

**Example Comment:**
```markdown
✅ **Backlog Prioritization Complete**

**Summary:**
- Total backlog items reviewed: 12
- Active priorities selected: 5
- Items assessed but not selected: 7

**Priority Breakdown:**
- Priority 1 (Highest): 1 item (Security - Critical CVE)
- Priority 2 (High): 1 item (Feature - High business value)
- Priority 3 (Normal): 3 items (1 Tech Debt quick win, 2 Features)

**Notable Selections:**
- Security vulnerability in core code prioritized as P1
- Tech debt item "modernize-namespaces" included as quick win
- Priority override applied for item "add-logging-framework" (swapped in at P2)

**Updated File:** `/product/prioritization.md`

View the complete prioritization: [Link]
```

## Handling Edge Cases

The workflow should gracefully handle these edge cases:

### No Tech Debt Items Exist

**Scenario**: Backlog has no tech debt items (all tech debt has been addressed)

**Action**: 
- Skip tech debt policy requirement
- Select 5 items from security, features, and bugs
- Note in prioritization: "No tech debt items in backlog"

### More Than 5 High-Priority Items

**Scenario**: More than 5 items qualify as Priority 1 or Priority 2 (e.g., multiple critical CVEs)

**Action**:
- Select highest priority items first (P1 before P2)
- Use creation date as tie-breaker (older items first)
- Place remaining high-priority items in "Assessed But Not Selected" with note explaining they're next in line
- Example: "4 P1 items selected, 2 additional P1 items in queue - will move to active when slots open"

### Fewer Than 5 Active Items

**Scenario**: Backlog has fewer than 5 active items total

**Action**:
- Select all active items (OK to have fewer than 5)
- Note in prioritization: "All X active items selected (fewer than max of 5)"
- No need to fill to 5 artificially

### All Items Equal Priority

**Scenario**: After applying criteria, many items appear to have equal value/priority

**Action**:
- Use decision framework from Step 3.4
- If still equal after framework: Use creation date (oldest first)
- Rationale: Older items have been waiting longer

### Priority Override Conflicts

**Scenario**: Multiple items have priority overrides that exceed available slots

**Example**: 3 items with P1 override, but only 5 slots total, and 2 P1 security items already selected

**Action**:
- Security items take precedence over overrides (security policy first)
- Select highest-priority overrides that fit
- Remaining override items go to "Assessed But Not Selected" with note about conflict
- Human should review and potentially adjust overrides

### Malformed Override Field

**Scenario**: Priority override field exists but is incomplete or incorrectly formatted

**Action**:
- If priority number is present and valid (1-5): Use it
- If missing required fields (who set it, reason): Use the priority but note the incomplete data
- If priority number is invalid or missing: Ignore the override and process as normal item
- Add note in prioritization about malformed override found

### No Security Core/Non-Core Clarity

**Scenario**: Security item location is unclear (is `/tools/` core or non-core?)

**Action**:
Use this classification:
- **Core**: `/src/` excluding test projects (DataFlow.Core, DataFlow.Common, etc.)
- **Non-Core**: `/sample/`, `/tools/`, `/poc/`, test projects, build scripts
- If unclear: Treat as non-core (safer, caps at P3) and note the assumption

## Triggering Prioritization

### Method 1: GitHub Issue

Create a new issue with title format:
```
Product Backlog Prioritization - [Optional: Specific Focus]
```

**Example titles:**
- "Product Backlog Prioritization"
- "Product Backlog Prioritization - Focus on Security"
- "Product Backlog Prioritization - Monthly Review"

**Issue body** can include:
- Specific guidance for prioritization
- Constraints or focus areas
- Reference to recent changes that require re-prioritization

### Method 2: Comment Trigger

On any existing issue, add a comment:
```
@copilot please prioritize the product backlog
```

**Alternative trigger phrases:**
- `/prioritize-backlog`
- `@copilot run product prioritization`
- `@copilot update backlog priorities`

Copilot will:
1. Recognize the trigger
2. Execute prioritization workflow
3. Update `/product/prioritization.md`
4. Reply with summary comment

## Periodic Review

**Recommendation**: Run backlog prioritization:
- **Weekly**: If backlog is very active (many new items)
- **Bi-weekly**: Standard cadence for most teams
- **Monthly**: If backlog is stable

**Before Review:**
- Ensure all backlog items have up-to-date metadata
- Check for new security advisories
- Review any priority override requests from stakeholders
- Verify completed items are archived

**During Review:**
- Follow standard workflow steps
- Pay special attention to items that have been "Assessed But Not Selected" for multiple cycles
- Consider archiving low-priority items that are no longer relevant

## Integration with Other Workflows

### From Research Workflow

When research creates a backlog issue:
1. Research team creates GitHub issue with `workflow:product-backlog` and `research` labels
2. Research team can request prioritization via comment or issue
3. Prioritization workflow evaluates the new item
4. Item is either selected (labeled `priority-high` etc.) or remains in backlog

### From Tech Debt Workflow

When tech debt analysis completes:
1. Tech debt items created as GitHub issues with `workflow:product-backlog` and `tech-debt` labels
2. Tech debt team can trigger prioritization
3. Policy ensures at least 1 tech debt item is selected

### From Implementation Workflow

When implementation completes:
1. Completed item archived to `/product/resolved/`
2. Can trigger re-prioritization to fill the open slot
3. Next highest priority item moves into active priorities

## Priority Override Management

### Setting an Override

Only humans (product team, stakeholders) can set priority overrides.

**Process:**
1. Human opens the backlog item file
2. Adds or updates the override field:
   ```markdown
   **Priority Override**: 1 (Set by: Jane Smith, Date: 2025-11-08, Reason: Critical customer blocker)
   ```
3. Triggers prioritization (via issue or comment)
4. Copilot processes override during next prioritization

### Clearing an Override

**When override is no longer needed:**
1. Human removes or updates the override field
2. Triggers re-prioritization
3. Item is re-assessed based on standard criteria

### Override Guidelines

**Use overrides for:**
- Critical customer blockers
- Time-sensitive business requirements
- Regulatory compliance needs
- Stakeholder commitments

**Don't use overrides for:**
- Every item you think is important
- Personal preferences without business justification
- Items that should naturally rise through standard criteria

**Best Practice**: Limit overrides to 1-2 items at a time to maintain effectiveness.

## Troubleshooting

### Issue: More than 5 items seem critical

**Solution:**
- Re-evaluate what "critical" means
- Consider if some items can be broken down
- Use Priority 1 and 2 more sparingly
- Remember: Focus is more valuable than trying to do everything

### Issue: No tech debt items in top 5

**Check:**
1. Are there tech debt items in backlog? (source: `techdebt-*`)
2. Are all 5 slots filled with P1 or P2 items?
   - If yes: Tech debt may be appropriately deprioritized
   - If no: Ensure at least 1 tech debt item is included per policy

### Issue: Override not being honored

**Debug:**
1. Verify override field is correctly formatted in backlog item
2. Check override priority vs. currently selected items
3. Ensure override priority is actually higher than selected items
4. Confirm MAX_SELECTED_ITEMS = 5 is not blocking inclusion

### Issue: Security item not prioritized correctly

**Review:**
1. Is the item correctly categorized as security?
2. Was risk assessment performed (core vs. non-core)?
3. Is CVE criticality rating available and correct?
4. Check if override is preventing inclusion

## Success Criteria

Prioritization is successful when:

- ✅ Exactly 5 (or fewer) items in Active Priorities
- ✅ All security vulnerabilities properly risk-assessed
- ✅ At least 1 tech debt item included (if any exist)
- ✅ All priority overrides processed correctly
- ✅ "Assessed But Not Selected" table is complete
- ✅ Rationale provided for all selections
- ✅ `/product/prioritization.md` updated and committed
- ✅ Summary comment posted

## Examples

### Example 1: Security-Driven Prioritization

**Backlog State:**
- 2 security vulnerabilities (1 critical in core, 1 medium in samples)
- 3 tech debt items
- 5 feature requests
- 2 bug fixes

**Prioritization Result:**
1. P1: Critical security vulnerability in core code
2. P3: Medium security vulnerability in samples
3. P3: Tech debt quick win
4. P3: High-value feature request
5. P3: Bug fix affecting multiple users

### Example 2: Override Priority Swap

**Current Selected (before override):**
1. P3: Feature A
2. P3: Feature B
3. P3: Tech Debt Item
4. P3: Bug Fix A
5. P3: Feature C

**Override Added:**
- Feature D gets Priority Override = 2 (customer blocker)

**Prioritization Result (after override):**
1. P2: Feature D (override)
2. P3: Feature A
3. P3: Tech Debt Item
4. P3: Bug Fix A
5. P3: Feature B

*Feature C moves to "Assessed But Not Selected"*

### Example 3: Mixed Priorities

**Backlog State:**
- 1 critical CVE in core (should be P1)
- 1 tech debt item with override P2
- 8 other items (features, bugs, tech debt)

**Prioritization Result:**
1. P1: Critical CVE in core code
2. P2: Tech debt item (override from product team)
3. P3: Quick win tech debt (satisfies "at least 1 tech debt" policy)
4. P3: High-value feature
5. P3: User-reported bug

---

**Remember**: The goal of prioritization is to ensure the implementation team always has clear, focused priorities that deliver maximum value while managing risk and maintaining code quality.

---

## Handover to Next Workflow

When prioritization is complete, hand over high-priority items to the appropriate next workflow.

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete handover patterns and troubleshooting.

### Handover to Implementation

**When**: Priority items are ready for implementation

```bash
./.team/scripts/workflow/handover-issue.sh \
  $ISSUE product-backlog implementation "Prioritization complete. Priority [N] assigned. Ready for implementation."
```

**Comment Should Include**:
- Priority level assigned (P1, P2, P3, etc.)
- Why this priority was assigned
- Any special considerations

### Handover to Research

**When**: High-priority item needs validation before implementation

```bash
./.team/scripts/workflow/handover-issue.sh \
  $ISSUE product-backlog research "High-priority item needs approach validation before implementation"
```

### Keep in Product Backlog

**When**: Item assessed but not selected for immediate implementation

No handover needed - item remains in `workflow:product-backlog` with "Assessed But Not Selected" status in `/product/prioritization.md`.

### Close Issue

**When**: Prioritization review complete (for the prioritization issue itself, not backlog items)

```bash
gh issue close $ISSUE --comment "✅ **Product Prioritization Complete**

Prioritization review complete.

**Selected Items**: [N] items prioritized
**Prioritization Document**: \`/product/prioritization.md\`

**High Priority Items**:
- [List P1/P2 items]

See: \`.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md\`"
```

