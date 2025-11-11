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

## Label Cleanup on Entry

**⚠️ IMPORTANT**: Before starting prioritization work, check for and clean up conflicting workflow labels.

### Workflow Label Validation

When you start work on an issue in this workflow:

1. **Check the issue's labels** for any workflow labels
2. **Identify conflicts**: If the issue has MULTIPLE workflow labels (e.g., both `workflow:product-backlog` AND `workflow:triage`)
3. **Determine correct label**: 
   - If you were assigned to this issue via the product backlog queue, `workflow:product-backlog` is correct
   - If the issue has another workflow label in addition to `workflow:product-backlog`, that's label pollution
4. **Remove conflicting labels**: Remove any workflow label that is NOT `workflow:product-backlog`
5. **Add cleanup comment** noting what was corrected

### Label Cleanup Example

**For Copilot Agents** (use MCP tools):

```python
# Example: Issue has both workflow:product-backlog and workflow:triage labels
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    labels=["workflow:product-backlog"]  # Only keep the correct label
)

add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    body="[Copilot-Workflow: Product Prioritization] 🏷️ Label cleanup: Removed conflicting `workflow:triage` label. This issue is correctly in the product backlog workflow."
)
```

**Why this matters**: Issues should have exactly ONE workflow label at a time. Multiple labels create confusion about which workflow owns the issue.

**When to skip**: If the issue only has `workflow:product-backlog` label (no conflicts), proceed directly to prioritization work.

---

## Multi-Phase Issue Check

**⚠️ ALWAYS**: Check if this issue is part of a multi-phase plan before starting prioritization work.

### Quick Check

```python
# Get current issue details
issue = issue_read(
    method="get",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=CURRENT_ISSUE_NUMBER
)

# Check if parent exists
if issue.parent:
    # This is a sub-issue - read parent context
    parent = issue_read(
        method="get",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue.parent.number
    )
    # Review parent to understand overall prioritization plan
else:
    # Standalone prioritization - proceed with normal workflow
```

### If This is a Sub-Issue

**DO:**
1. ✅ Read parent issue to understand overall prioritization plan
2. ✅ Note which prioritization phase this represents
3. ✅ Review completed phases for context
4. ✅ Update parent description as prioritization progresses
5. ✅ Check if this is the last sub-issue before finalizing

**Parent Update Pattern:**

When documenting prioritization decisions:
```python
# Update parent issue description to reflect progress
# Example: Change "Phase 2 - Priority Analysis" to "Phase 2 - 5 items prioritized, handovers created"
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=parent_number,
    body=updated_description
)
```

**Closing Parent (Last Sub-Issue Only):**

If this is the last open sub-issue of the prioritization plan, include parent in PR description:
```markdown
Fixes #CURRENT_ISSUE
Fixes #PARENT_ISSUE
```

This ensures both issues close when PR merges.

**See:** [Multi-Phase Issue Procedures](/.team/MULTI_PHASE_ISSUES.md) for complete guidance including examples and troubleshooting.

---

## Overview

This workflow defines how to automatically prioritize the product backlog based on established policy criteria. It ensures that the most valuable and urgent work items are selected for implementation while maintaining a manageable active priority list.

**⚠️ Comment Prefix Convention:**
- Prefix ALL comments with `[Copilot-Workflow: Product Prioritization]` to confirm you're following this workflow
- Example: `[Copilot-Workflow: Product Prioritization] I've analyzed the backlog and selected 5 items based on the prioritization policy...`

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

**See**: `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for complete policy details and configurable parameters.

### Key Parameters

- **IMPLEMENTATION_QUEUE_LIMIT** = 10 (maximum items in implementation workflow)
- **SELECTION_BATCH_SIZE** = 100 (items per page when querying backlog)
- **DUPLICATE_SIMILARITY_THRESHOLD** = 0.8 (handled by Bulk Triage)

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

| Priority | Issue # | Title | Category | Rationale |
|----------|---------|-------|----------|-----------|
| 1 | #123 | ... | Security | Critical CVE in core code |
| 2 | #124 | ... | Feature | High business value |
| 3 | #125 | ... | Tech Debt | Quick win cleanup |
| 3 | #126 | ... | Feature | User-requested |
| 3 | #127 | ... | Bug Fix | Affects multiple users |

#### Assessed But Not Selected

Items that were reviewed but not chosen for active prioritization.

| Issue # | Title | Category | Assessment Priority | Notes |
|---------|-------|----------|-------------------|-------|
| #128 | ... | ... | 3 | Good candidate but current priorities take precedence |
| #129 | ... | ... | 4 | Nice-to-have, defer until capacity |
| #130 | ... | ... | 5 | Low impact, archive candidate |

**Purpose**: Provides transparency about what was considered and why it wasn't selected.

## Workflow Steps

**See**: `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for all configurable parameters.

### For Copilot Agents

When triggered to prioritize the backlog:

#### Step 1: Collect All Backlog Items

**⚠️ IMPORTANT**: GitHub issues with the `workflow:product-backlog` label are the **primary and authoritative backlog source**.

Query all open backlog GitHub issues with pagination support:

```python
# Collect all backlog issues with pagination
all_backlog_issues = []
page = 1
SELECTION_BATCH_SIZE = 100  # From PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md

while True:
    batch = list_issues(
        owner="uniun-technology",
        repo="lib-dataflow",
        labels=["workflow:product-backlog"],
        state="OPEN",  # Active backlog items only
        page=page,
        perPage=SELECTION_BATCH_SIZE
    )
    
    if not batch:
        break
    
    all_backlog_issues.extend(batch)
    page += 1

print(f"Collected {len(all_backlog_issues)} open backlog items")
```

For each backlog issue, extract key metadata:
- Issue number
- Title  
- Labels (to determine category, source)
- Body content (for priority override, CVE info, effort estimate)

**Note**: Housekeeping (duplicate detection, stale items, completed items) is handled by **Bulk Triage workflow** (`.team/prompts/TRIAGE_WORKFLOW.md` Step 6). Product Prioritization focuses purely on prioritization and selection.

#### Step 2: Apply Prioritization Criteria

Analyze all collected items and assign priorities based on the policy (see `PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md`):

**Prioritization Order:**
1. **Security Vulnerabilities** (check for CVE references, security category)
   - Core code Critical/High CVE → Priority 1
   - Core code Medium CVE → Priority 2
   - Core code Low CVE or Non-core → Priority 3
2. **Priority Overrides** (check issue body for manual overrides)
   - Apply any explicit priority overrides from product team
3. **Tech Debt** (check for tech-debt label or source)
   - Identify "quick wins" (effort < 1 day, high impact)
4. **Standard Items** (features, bugs, enhancements)
   - Assess based on business value, impact, dependencies

**Output**: Create a prioritized list of ALL backlog items sorted by priority (1-5).

**Example Analysis:**
```python
prioritized_items = []

for issue in all_backlog_issues:
    # Determine priority based on criteria
    priority = assess_priority(issue)  # Returns 1-5
    
    prioritized_items.append({
        'number': issue['number'],
        'title': issue['title'],
        'priority': priority,
        'category': get_category(issue),
        'rationale': get_rationale(issue, priority)
    })

# Sort by priority (1 = highest)
prioritized_items.sort(key=lambda x: x['priority'])
```

#### Step 3: Present Prioritization for Review

**Before proceeding to selection**, present the prioritized list to the reviewer for confirmation.

Post a comment on the prioritization issue with:

```markdown
[Copilot-Workflow: Product Prioritization] 📊 **Prioritization Analysis Complete**

I've analyzed {total_items} backlog items and assigned priorities based on the policy.

## Prioritized Backlog Items

**Priority 1 (Highest)**:
- #{number}: {title} - {rationale}

**Priority 2 (High)**:
- #{number}: {title} - {rationale}

**Priority 3 (Normal)**:
- #{number}: {title} - {rationale}
[... up to 10-15 items shown ...]

**Lower priorities**: {count} additional items prioritized as P4-P5

---

## Prioritization Policy Applied

✅ Security vulnerabilities assessed (core vs non-core)
✅ Priority overrides honored
✅ Tech debt items identified ({count} found, {quick_wins} quick wins)
✅ Standard selection criteria applied

---

**Next Step: Selection**

To proceed with selection and move items to the implementation queue, reply with:
- `@copilot proceed with selection` - Use default limits from params file
- `@copilot skip` - Review only, no selection

**To adjust prioritization**: Reply with specific feedback and I'll re-analyze.

See `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for configuration details.
```

**Wait for reviewer response** before proceeding to Step 4.

#### Step 4: Check Implementation Queue Capacity

**Only proceed after reviewer approves prioritization in Step 3.**

Query the current implementation queue to determine available capacity:

```python
# Get current implementation queue size
IMPLEMENTATION_QUEUE_LIMIT = 10  # From PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md

implementation_queue = list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:implementation"],
    state="OPEN"
)

current_queue_size = len(implementation_queue)
available_slots = IMPLEMENTATION_QUEUE_LIMIT - current_queue_size

print(f"Implementation queue: {current_queue_size}/{IMPLEMENTATION_QUEUE_LIMIT} items")
print(f"Available slots: {available_slots}")
```

**If no slots available** (`available_slots <= 0`):
```markdown
[Copilot-Workflow: Product Prioritization] ⏸️ **Implementation Queue Full**

The implementation queue currently has {current_queue_size} open items (limit: {IMPLEMENTATION_QUEUE_LIMIT}).

**No items will be selected** until implementation team completes current work.

**Recommendation**: Wait for implementation queue to clear, then re-run prioritization.

**Current queue status**: [List top 5 items in implementation queue]
```

Stop here - do not select any items.

**If slots available** (`available_slots > 0`):
Proceed to Step 5.

#### Step 5: Select Items for Implementation Queue

Select the top-priority items (up to `available_slots`) from the prioritized list and move them to the implementation workflow.

```python
# Select top items based on available capacity
items_to_select = prioritized_items[:available_slots]

print(f"Selecting {len(items_to_select)} items for implementation queue")

# Move selected items to implementation workflow
for item in items_to_select:
    # Update workflow label
    issue_write(
        method="update",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=item['number'],
        labels=["workflow:implementation"]  # Remove product-backlog, add implementation
    )
    
    # Add handover comment
    add_issue_comment(
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=item['number'],
        body=f"""[Copilot-Workflow: Product Prioritization] 🔄 **Selected for Implementation**

This item has been prioritized and moved to the implementation queue.

**Priority**: {item['priority']}
**Rationale**: {item['rationale']}

See `.team/prompts/IMPLEMENTATION_WORKFLOW.md` for implementation guidance.
"""
    )
```

#### Step 6: Update Prioritization File

Update `/product/prioritization.md` with the results:

```python
# Create prioritization summary
prioritization_content = f"""# Product Backlog Prioritization

**Last Updated**: {datetime.now().strftime("%Y-%m-%d")}
**Updated By**: Copilot Agent - Automated Prioritization

## Selection Summary

**Items Selected for Implementation**: {len(items_to_select)}
**Implementation Queue Status**: {current_queue_size + len(items_to_select)}/{IMPLEMENTATION_QUEUE_LIMIT}
**Backlog Items Reviewed**: {len(all_backlog_issues)}

### Selected Items (Moved to Implementation Queue)

| Priority | Issue # | Title | Category | Rationale |
|----------|---------|-------|----------|-----------|
"""

for item in items_to_select:
    prioritization_content += f"| {item['priority']} | #{item['number']} | {item['title'][:50]}... | {item['category']} | {item['rationale'][:80]}... |\n"

prioritization_content += f"""

## Remaining Backlog Items

**Priority 1-2 (High)**: {len([item for item in prioritized_items[len(items_to_select):] if item['priority'] in [1, 2]])} items
**Priority 3 (Normal)**: {len([item for item in prioritized_items[len(items_to_select):] if item['priority'] == 3])} items  
**Priority 4-5 (Lower)**: {len([item for item in prioritized_items[len(items_to_select):] if item['priority'] in [4, 5]])} items

Top unselected items:

| Priority | Issue # | Title | Category | Notes |
|----------|---------|-------|----------|-------|
"""

# Show top 10 unselected items
for item in prioritized_items[len(items_to_select):len(items_to_select)+10]:
    prioritization_content += f"| {item['priority']} | #{item['number']} | {item['title'][:50]}... | {item['category']} | Awaiting capacity |\n"

prioritization_content += f"""

## Priority Legend

- **1** = Highest priority (critical, blocking, time-sensitive)
- **2** = High priority (important, significant value)
- **3** = Normal priority (default for most work)
- **4** = Lower priority (nice-to-have)
- **5** = Lowest priority (defer unless capacity allows)

## Prioritization Policy

See `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for complete policy details:
- Critical security vulnerabilities take precedence
- At least 1 tech debt item included when available  
- Priority overrides honored
- Selection based on implementation queue capacity

## Notes

**Policy Compliance**:
- ✅ Security vulnerabilities assessed
- ✅ Tech debt requirement met ({tech_debt_count} items available, {tech_debt_selected} selected)
- ✅ Priority overrides applied ({override_count} overrides processed)
- ✅ Queue capacity checked ({available_slots} slots available)

**Housekeeping**: Duplicate detection, stale items, and completed items are managed by Bulk Triage workflow (`.team/prompts/TRIAGE_WORKFLOW.md` Step 6).
"""

# Write to file (would use create_or_update_file in actual implementation)
```

#### Step 7: Report Completion

Post a summary comment on the triggering issue:

```markdown
[Copilot-Workflow: Product Prioritization] ✅ **Prioritization and Selection Complete**

## Summary

**Backlog Analyzed**: {total_items} open items
**Items Selected**: {selected_count} items moved to implementation queue
**Implementation Queue**: {new_queue_size}/{IMPLEMENTATION_QUEUE_LIMIT} items

## Selected Items

{list_selected_items_with_priorities}

## Implementation Queue Status

**Before**: {current_queue_size} items
**After**: {new_queue_size} items  
**Remaining Capacity**: {remaining_capacity} slots

## Prioritization Details

**Priority Distribution** (remaining in backlog):
- P1 (Highest): {p1_count} items
- P2 (High): {p2_count} items
- P3 (Normal): {p3_count} items
- P4-P5 (Lower): {p4_p5_count} items

**Policy Compliance**:
- ✅ Security vulnerabilities prioritized
- ✅ Tech debt requirement met
- ✅ Priority overrides honored
- ✅ Queue capacity managed

**Updated File**: `/product/prioritization.md`

---

**Next Steps**:
- Implementation team: Select from items now in `workflow:implementation` queue
- Product team: Review remaining high-priority items for next cycle
- For housekeeping: Create Bulk Triage issue to clean duplicates/stale items

See `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for configuration.
```


## Handling Edge Cases

The workflow should gracefully handle these edge cases:

### No Implementation Queue Capacity

**Scenario**: Implementation queue is at or over capacity (`workflow:implementation` has >= IMPLEMENTATION_QUEUE_LIMIT items)

**Action**:
- Do NOT select any items
- Report queue status
- Recommend waiting for implementation team to complete current work
- Prioritization analysis can still be performed and shared for review

### No Tech Debt Items Exist

**Scenario**: Backlog has no tech debt items (all tech debt has been addressed)

**Action**: 
- Skip tech debt policy requirement
- Note in prioritization: "No tech debt items in backlog"
- Select items from security, features, and bugs

### More High-Priority Items Than Slots

**Scenario**: More items qualify as Priority 1 or Priority 2 than available implementation slots

**Action**:
- Select highest priority items first (P1 before P2)
- Use creation date as tie-breaker (older items first)
- Remaining high-priority items stay in backlog for next cycle
- Document in summary: "X additional P1/P2 items awaiting capacity"

### Fewer Items Than Available Slots

**Scenario**: Backlog has fewer items than available implementation slots

**Action**:
- Select all backlog items (OK to have fewer than limit)
- Note in summary: "All X backlog items selected (fewer than limit of Y)"
- No need to fill artificially

### All Items Equal Priority

**Scenario**: After applying criteria, many items appear to have equal priority

**Action**:
- Apply standard selection criteria (business value, dependencies, effort)
- If still equal: Use creation date (oldest first)
- Rationale: Older items have been waiting longer

### Priority Override Conflicts

**Scenario**: Multiple items have priority overrides that exceed available slots

**Example**: 3 items with P1 override, but only 2 slots available, and 1 P1 security item already selected

**Action**:
- Security items take precedence over overrides (security policy first)
- Select highest-priority overrides that fit
- Remaining override items stay in backlog for next cycle
- Human should review and potentially adjust overrides if needed

### Malformed Override Field

**Scenario**: Priority override field exists but is incomplete or incorrectly formatted

**Action**:
- If priority number is present and valid (1-5): Use it
- If missing required fields (who set it, reason): Use the priority but note the incomplete data
- If priority number is invalid or missing: Ignore the override and process as normal item
- Document any malformed overrides found

### No Security Core/Non-Core Clarity

**Scenario**: Security item location is unclear (is `/tools/` core or non-core?)

**Action**:
Use this classification:
- **Core**: `/src/` excluding test projects (DataFlow.Core, DataFlow.Common, etc.)
- **Non-Core**: `/sample/`, `/tools/`, `/poc/`, test projects, build scripts
- If unclear: Treat as non-core (safer, caps at P3) and note the assumption

**Note on Housekeeping**: Duplicate detection, completed item closure, and stale item flagging are now handled by the **Bulk Triage workflow** (`.team/prompts/TRIAGE_WORKFLOW.md` Step 6). Product Prioritization focuses purely on prioritization and selection.


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

**Note**: Housekeeping (duplicate detection, stale items, completed items) is now handled in the Bulk Triage workflow, not Product Prioritization.

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

**Note on Housekeeping**: For duplicate detection, stale item cleanup, and completed item closure across all workflow labels, use the **Bulk Triage workflow** (see `.team/prompts/TRIAGE_WORKFLOW.md` Step 6).

---

## Handover to Next Workflow

When prioritization is complete, hand over high-priority items to the appropriate next workflow.

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete handover patterns and troubleshooting.

### Handover to Implementation

**When**: Priority items are ready for implementation

```bash
./.github/scripts/workflow/handover-issue.sh \
  $ISSUE product-backlog implementation "Prioritization complete. Priority [N] assigned. Ready for implementation."
```

**Comment Should Include**:
- Priority level assigned (P1, P2, P3, etc.)
- Why this priority was assigned
- Any special considerations

### Handover to Research

**When**: High-priority item needs validation before implementation

```bash
./.github/scripts/workflow/handover-issue.sh \
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

See: \`.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md\`"
```

