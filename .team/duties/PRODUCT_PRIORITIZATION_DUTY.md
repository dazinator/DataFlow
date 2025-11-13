# Product Prioritization Duty

**Purpose**: Prioritize backlog work items based on policy criteria and move selected items to implementation

**Layer**: 2 (Duty - specialized procedure)

**Version**: 1.0  
**Created**: 2025-11-12

---

## Required Context

**⚠️ IMPORTANT**: Read the following documents before proceeding:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction layer
- **[Issue Refinement Procedure](../procedures/issue-refinement.md)** - Detecting and refining multi-phase plans
- **[Duty Assignment Procedure](../procedures/duty-assignment.md)** - Determining duty from work items
- **[Work Item Creation Procedure](../procedures/work-item-creation.md)** - Creating new work items
- **[Comment Patterns Procedure](../procedures/comment-patterns.md)** - Standard comment formats
- **[Multi-Phase Work Items](../procedures/multi-phase-work-items.md)** - Parent-child work item management
- **[Handover Procedure](../procedures/handover.md)** - Transitioning work items between duties

**Semantic Operations Used**:
- `query_work_items_by_duty(duty)` - Find work items assigned to this duty
- `get_work_item_details(work_item_id)` - Retrieve work item information
- `get_work_item_duty(work_item_id)` - Get current duty assignment
- `assign_work_item_to_duty(work_item_id, duty)` - Change duty (handover)
- `add_work_item_comment(work_item_id, text)` - Add comment
- `update_work_item(work_item_id, fields)` - Update work item fields
- `get_parent_work_item(work_item_id)` - Get parent if exists
- `list_child_work_items(work_item_id)` - Get all children of a work item
- `is_multi_phase(work_item_id)` - Check if part of multi-phase plan

---

## Overview

**Purpose**: Prioritize product backlog work items based on established policy criteria, ensuring the most valuable and urgent work is selected for implementation while maintaining manageable queue size.

**Entry Point**: Work items requesting backlog prioritization or periodic backlog reviews

**Typical Duration**: 1-3 days depending on backlog size

**Key Principle**: Automated prioritization based on policy criteria (security, tech debt, priority overrides, business value) with human review before selection.

---

## Prioritization Policy

### Configuration

**See**: `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for all configurable parameters.

**Key Parameters:**
- **IMPLEMENTATION_QUEUE_LIMIT** = 10 (maximum items in implementation duty)
- **SELECTION_BATCH_SIZE** = 100 (items per page when querying backlog)

### Priority Levels

- **1** = Highest priority (critical, blocking, time-sensitive)
- **2** = High priority (important, significant value)
- **3** = Normal priority (default for most work)
- **4** = Lower priority (nice-to-have)
- **5** = Lowest priority (defer unless capacity allows)

### Selection Criteria (In Priority Order)

#### 1. Critical Security Vulnerabilities (Highest Priority)

Security vulnerabilities take precedence over all other work.

**Risk Assessment:**
1. Identify security-related items (check category, CVE references, security keywords)
2. Assess impact scope:
   - **Core Code**: Production code in `/src` (excluding test, tooling, samples)
   - **Non-Core**: Test code, tooling, sample projects, documentation
3. For **Core Code** vulnerabilities:
   - **Critical/High CVE** → Priority 1
   - **Medium CVE** → Priority 2
   - **Low CVE or No CVE** → Priority 3
4. For **Non-Core** vulnerabilities:
   - Maximum Priority 3 (unless explicitly overridden)

#### 2. Tech Debt Items

Aim to include **at least 1** tech debt item in selected items.

**Rationale**: Prevents tech debt accumulation by ensuring regular cleanup work.

**Selection Strategy:**
- Review all tech debt items
- Prefer "quick wins" - small effort with clear value
- Consider impact and dependencies
- Select at least 1 tech debt item if space allows

#### 3. Priority Override Mechanism

Backlog items can have **override priority** set by humans (product team, stakeholders).

**Override Format** (in work item body):
```markdown
**Priority Override**: [1-5] (Set by: [Name], Date: YYYY-MM-DD, Reason: [Brief rationale])
```

**Processing Rules:**
- If override priority is HIGHER than currently selected items → Swap it in
- If no room (all selected items equal or higher priority) → Leave as is
- Overrides should NEVER be adjusted by agent unless explicitly requested

#### 4. Standard Selection Criteria

For remaining slots (after security, tech debt, and overrides):
- Business value and impact
- Dependencies and blockers
- Effort vs. value ratio
- Stakeholder requests
- Alignment with product roadmap

---

## Quick Start

**Comment Prefix Convention:**
- Follow [Comment Patterns Procedure](../procedures/comment-patterns.md)
- Prefix ALL comments with `[Copilot-Duty: Product Prioritization]`
- Example: `[Copilot-Duty: Product Prioritization] Analyzed 45 backlog items and selected 5 based on policy.`

**Standard Prioritization Flow**:
1. Query product backlog queue and check for multi-phase plan
2. Collect all backlog work items
3. **Refine multi-phase plans into sub-issues (housekeeping)**
4. Apply prioritization criteria and assign priorities
5. Present prioritization for human review
6. After approval: Check implementation queue capacity
7. Select top items and move to implementation duty
8. Update prioritization analysis document
9. Report completion

---

## Procedure

### Step 1: Query Product Backlog Queue

Use semantic operation to find work items assigned to product backlog:

```python
# Query product backlog queue
backlog_items = query_work_items_by_duty(duty="product-backlog")

print(f"Found {len(backlog_items)} work items in product backlog")
```

**Check for Multi-Phase Plans**:
- Follow [Multi-Phase Work Items Procedure](../procedures/multi-phase-work-items.md)
- If this is a sub-work-item, read parent context
- Update parent progress as prioritization advances

### Step 2: Collect All Backlog Items

Collect all open backlog work items with pagination:

```python
# Collect all backlog items
all_backlog_items = []
page = 1
SELECTION_BATCH_SIZE = 100  # From params file
MAX_PAGES = 100  # Safety limit to prevent infinite loops

while page <= MAX_PAGES:
    batch = query_work_items_by_duty(
        duty="product-backlog",
        state="open",
        page=page,
        per_page=SELECTION_BATCH_SIZE
    )
    
    if not batch:
        break
    
    all_backlog_items.extend(batch)
    page += 1

print(f"Collected {len(all_backlog_items)} open backlog items")
```

For each item, extract:
- Work item ID
- Title
- Category (labels/tags)
- Body content (for priority override, CVE info)

### Step 2.5: Refine Backlog Issues (Housekeeping)

**Before prioritizing**, check backlog items for multi-phase plans that need refinement.

Follow [Issue Refinement Procedure](../procedures/issue-refinement.md):

```python
# Refine multi-phase plans in backlog
refined_count = 0
refinement_pending = []

for item in all_backlog_items:
    # Check if item needs refinement
    refinement_result = check_issue_refinement(item['id'])
    
    if refinement_result['refined']:
        # Item was refined into sub-issues
        refined_count += 1
        
        # Sub-issues are automatically assigned to product-backlog duty
        # They will be included in next query
        
    elif refinement_result.get('needs_approval'):
        # Refinement plan presented, waiting for approval
        refinement_pending.append(item['id'])

# If any refinements pending, pause prioritization
if refinement_pending:
    add_work_item_comment(
        work_item_id=current_work_item_id,
        text=f"""[Copilot-Duty: Product Prioritization] ⏸️ **Refinement Approval Needed**

I've identified {len(refinement_pending)} backlog items as multi-phase plans:

{list_pending_refinements}

Pausing prioritization until refinement is approved or rejected.

Reply with `@copilot proceed with all refinements` or handle each individually.
"""
    )
    return

# If any items were refined, re-query backlog to include new sub-issues
if refined_count > 0:
    add_work_item_comment(
        work_item_id=current_work_item_id,
        text=f"""[Copilot-Duty: Product Prioritization] 🔄 **Backlog Housekeeping Complete**

Refined {refined_count} multi-phase plans into sub-issues.

Re-querying backlog to include newly created sub-issues...
"""
    )
    
    # Re-query to get updated backlog with sub-issues
    all_backlog_items = []
    page = 1
    
    while page <= MAX_PAGES:
        batch = query_work_items_by_duty(
            duty="product-backlog",
            state="open",
            page=page,
            per_page=SELECTION_BATCH_SIZE
        )
        
        if not batch:
            break
        
        all_backlog_items.extend(batch)
        page += 1
    
    print(f"Updated backlog: {len(all_backlog_items)} items (including {refined_count} refinements)")
```

**Why This Matters**:
- Ensures backlog items are properly structured before prioritization
- Sub-issues from multi-phase plans can be individually prioritized
- Prevents selecting parent issues that need refinement first
- Selection process can focus on logical phase boundaries

**Selection Sensitivity**: When selecting from prioritized items, prefer next logical phase sub-issues over standalone items of equal priority.

---

### Step 3: Apply Prioritization Criteria

Analyze all items and assign priorities based on policy:

**Prioritization Order:**
1. **Security Vulnerabilities** (check for CVE references, security category)
   - Core code Critical/High CVE → Priority 1
   - Core code Medium CVE → Priority 2
   - Core code Low CVE or Non-core → Priority 3
2. **Priority Overrides** (check work item body for manual overrides)
   - Apply any explicit priority overrides from product team
3. **Tech Debt** (check for tech-debt label)
   - Identify "quick wins" (effort < 1 day, high impact)
4. **Standard Items** (features, bugs, enhancements)
   - Assess based on business value, impact, dependencies

**Output**: Create a prioritized list sorted by priority (1-5).

### Step 4: Present Prioritization for Review

**Before proceeding to selection**, present the prioritized list to reviewer:

```python
# Use comment patterns procedure
comment_text = """[Copilot-Duty: Product Prioritization] 📊 **Prioritization Analysis Complete**

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
"""

# Add comment to triggering work item
add_work_item_comment(
    work_item_id=current_work_item_id,
    text=comment_text
)
```

**Wait for reviewer response** before proceeding to Step 5.

### Step 5: Check Implementation Queue Capacity

**Only proceed after reviewer approves prioritization in Step 4.**

Query the current implementation queue to determine available capacity:

```python
# Get current implementation queue size
IMPLEMENTATION_QUEUE_LIMIT = 10  # From params file

implementation_queue = query_work_items_by_duty(
    duty="implementation",
    state="open"
)

current_queue_size = len(implementation_queue)
available_slots = IMPLEMENTATION_QUEUE_LIMIT - current_queue_size

print(f"Implementation queue: {current_queue_size}/{IMPLEMENTATION_QUEUE_LIMIT} items")
print(f"Available slots: {available_slots}")
```

**If no slots available** (`available_slots <= 0`):
- Use comment patterns to report queue is full
- Do not select any items
- Stop here

**If slots available** (`available_slots > 0`):
- Proceed to Step 6

### Step 6: Select Items for Implementation Queue

Select the top-priority items (up to `available_slots`) and move them to implementation duty:

```python
# Helper function to extract phase number from title
def extract_phase_number(title):
    """
    Extract phase number from title in format "[Phase N] Title"
    Returns None if no phase number found
    """
    import re
    match = re.search(r'\[Phase (\d+)\]', title)
    return int(match.group(1)) if match else None

# Select top items based on available capacity
# Note: Prefer next logical phase sub-issues when equal priority
items_to_select = []
selected_parents = set()  # Track parent issues to avoid selecting multiple phases

for item in prioritized_items:
    if len(items_to_select) >= available_slots:
        break
    
    # Check if this is a sub-issue
    parent_id = get_parent_work_item(item['work_item_id'])
    
    if parent_id:
        # This is a sub-issue - check if we already selected another phase
        if parent_id in selected_parents:
            continue  # Skip - already selected another phase from this parent
        
        # Check if this is the next logical phase
        siblings = list_child_work_items(parent_id)
        
        # Extract phase number from title (format: "[Phase N] Title")
        # If phase number cannot be extracted, use creation order as fallback
        current_phase_num = extract_phase_number(item['title'])
        
        if current_phase_num is not None:
            # Use explicit phase numbers for ordering
            prior_phases_complete = all(
                s['status'] == 'closed'
                for s in siblings
                if extract_phase_number(s['title']) is not None 
                and extract_phase_number(s['title']) < current_phase_num
            )
        else:
            # Fallback: use creation order (work item number) if no explicit phase number
            prior_phases_complete = all(
                s['status'] == 'closed' 
                for s in siblings 
                if s['number'] < item['number']
            )
        
        if not prior_phases_complete:
            continue  # Skip - prior phases not complete yet
        
        # This is the next logical phase - select it
        items_to_select.append(item)
        selected_parents.add(parent_id)
    else:
        # Standalone item - select it
        items_to_select.append(item)

print(f"Selecting {len(items_to_select)} items for implementation queue")

# Move selected items to implementation duty using handover procedure
for item in items_to_select:
    # Hand over to implementation duty
    assign_work_item_to_duty(
        work_item_id=item['work_item_id'],
        duty="implementation"
    )
    
    # Add handover comment following comment patterns
    handover_comment = f"""[Copilot-Duty: Product Prioritization] 🔄 **Selected for Implementation**

This item has been prioritized and moved to the implementation queue.

**Priority**: {item['priority']}
**Rationale**: {item['rationale']}

See `.team/duties/IMPLEMENTATION_DUTY.md` for implementation guidance.
"""
    
    add_work_item_comment(
        work_item_id=item['work_item_id'],
        text=handover_comment
    )
```

**See**: [Handover Procedure](../procedures/handover.md) for complete handover patterns.

### Step 7: Update Prioritization Analysis

Update the **product-backlog analysis document** with results:

**Document Location**: Analysis document under topic `product-backlog`  
**See**: [Documentation Artifacts](../DOCUMENTATION_ARTIFACTS.md) for structure

**Document Content**:
- Selection summary (items selected, queue status, backlog size)
- Selected items table (Priority, Issue #, Title, Category, Rationale)
- Remaining backlog items (priority distribution)
- Top unselected items (next candidates)
- Policy compliance notes

### Step 8: Report Completion

Post a summary comment on the triggering work item:

```python
# Use comment patterns procedure
summary_comment = """[Copilot-Duty: Product Prioritization] ✅ **Prioritization and Selection Complete**

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

**Updated Document**: Product-backlog analysis (see [Documentation Artifacts](../DOCUMENTATION_ARTIFACTS.md))

---

**Next Steps**:
- Implementation team: Select from items now in implementation queue
- Product team: Review remaining high-priority items for next cycle

See `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for configuration.
"""

add_work_item_comment(
    work_item_id=current_work_item_id,
    text=summary_comment
)
```

---

## Handover Points

### Handover to Implementation

**When**: Priority items are ready for implementation after selection

**Process**: Use [Handover Procedure](../procedures/handover.md)

```python
# Items already handed over in Step 6
# Handover includes:
# - Duty assignment change (product-backlog → implementation)
# - Handover comment with priority and rationale
```

### Handover to Research

**When**: High-priority item needs validation before implementation

**Process**: Use [Handover Procedure](../procedures/handover.md)

```python
# Hand over to research duty
assign_work_item_to_duty(
    work_item_id=item['work_item_id'],
    duty="research"
)

# Add handover comment
add_work_item_comment(
    work_item_id=item['work_item_id'],
    text="[Copilot-Duty: Product Prioritization] 🔄 High-priority item needs approach validation before implementation"
)
```

### Keep in Product Backlog

**When**: Item assessed but not selected for immediate implementation

**No handover needed** - item remains in `product-backlog` duty with status documented in analysis document.

---

## Handling Edge Cases

### No Implementation Queue Capacity

**Scenario**: Implementation queue is at or over capacity

**Action**:
- Do NOT select any items
- Report queue status in comment
- Recommend waiting for implementation team to complete current work
- Prioritization analysis can still be performed and shared for review

### No Tech Debt Items Exist

**Scenario**: Backlog has no tech debt items

**Action**: 
- Skip tech debt policy requirement
- Note in prioritization: "No tech debt items in backlog"
- Select items from security, features, and bugs

### More High-Priority Items Than Slots

**Scenario**: More items qualify as Priority 1 or Priority 2 than available slots

**Action**:
- Select highest priority items first (P1 before P2)
- Use creation date as tie-breaker (older items first)
- Remaining high-priority items stay in backlog for next cycle
- Document in summary: "X additional P1/P2 items awaiting capacity"

### Fewer Items Than Available Slots

**Scenario**: Backlog has fewer items than available slots

**Action**:
- Select all backlog items (OK to have fewer than limit)
- Note in summary: "All X backlog items selected (fewer than limit of Y)"

### All Items Equal Priority

**Scenario**: After applying criteria, many items have equal priority

**Action**:
- Apply standard selection criteria (business value, dependencies, effort)
- If still equal: Use creation date (oldest first)

### Priority Override Conflicts

**Scenario**: Multiple items have priority overrides that exceed available slots

**Action**:
- Security items take precedence over overrides
- Select highest-priority overrides that fit
- Remaining override items stay in backlog for next cycle

### Malformed Override Field

**Scenario**: Priority override field exists but is incomplete or incorrectly formatted

**Action**:
- If priority number is present and valid (1-5): Use it
- If missing required fields (who set it, reason): Use the priority but note incomplete data
- If priority number is invalid or missing: Ignore override and process as normal item
- Document any malformed overrides found

### No Security Core/Non-Core Clarity

**Scenario**: Security item location is unclear

**Action**:
Use this classification:
- **Core**: `/src/` excluding test projects
- **Non-Core**: `/sample/`, `/tools/`, `/poc/`, test projects, build scripts
- If unclear: Treat as non-core (safer, caps at P3) and note the assumption

---

## Common Patterns

### Periodic Backlog Review

**Recommendation**: Run backlog prioritization:
- **Weekly**: If backlog is very active (many new items)
- **Bi-weekly**: Standard cadence for most teams
- **Monthly**: If backlog is stable

**Process**:
1. Create work item for prioritization review
2. Follow standard prioritization procedure
3. Present results for review
4. After approval, select items for implementation

### Integration with Other Duties

#### From Research Duty

When research creates a backlog item:
1. Research creates work item with `product-backlog` duty
2. Research can trigger prioritization via comment or new work item
3. Prioritization duty evaluates the new item
4. Item is either selected for implementation or remains in backlog

#### From Tech Debt Duty

When tech debt analysis completes:
1. Tech debt items created with `product-backlog` duty
2. Tech debt team can trigger prioritization
3. Policy ensures at least 1 tech debt item is selected

#### From Implementation Duty

When implementation completes:
1. Can trigger re-prioritization to fill the open slot
2. Next highest priority item moves into implementation queue

---

## Priority Override Management

### Setting an Override

**Only humans can set priority overrides** (product team, stakeholders).

**Process**:
1. Human updates the backlog work item
2. Adds or updates the override field in work item body:
   ```markdown
   **Priority Override**: 1 (Set by: Jane Smith, Date: 2025-11-08, Reason: Critical customer blocker)
   ```
3. Triggers prioritization (via work item or comment)
4. Agent processes override during next prioritization

### Clearing an Override

**When override is no longer needed**:
1. Human removes or updates the override field
2. Triggers re-prioritization
3. Item is re-assessed based on standard criteria

### Override Guidelines

**Use overrides for**:
- Critical customer blockers
- Time-sensitive business requirements
- Regulatory compliance needs
- Stakeholder commitments

**Don't use overrides for**:
- Every item you think is important
- Personal preferences without business justification
- Items that should naturally rise through standard criteria

**Best Practice**: Limit overrides to 1-2 items at a time to maintain effectiveness.

---

## Success Criteria

Prioritization is successful when:

- ✅ All backlog items analyzed and prioritized
- ✅ All security vulnerabilities properly risk-assessed
- ✅ At least 1 tech debt item included (if any exist)
- ✅ All priority overrides processed correctly
- ✅ Selected items moved to implementation duty
- ✅ Rationale provided for all selections
- ✅ Product-backlog analysis document updated
- ✅ Summary comment posted
- ✅ Implementation queue capacity respected

---

## Related Documentation

- **[Handover Procedure](../procedures/handover.md)** - Transitioning work items between duties
- **[Multi-Phase Work Items](../procedures/multi-phase-work-items.md)** - Managing complex prioritization plans
- **[Work Item Creation Procedure](../procedures/work-item-creation.md)** - Creating prioritization work items
- **[Comment Patterns Procedure](../procedures/comment-patterns.md)** - Standard comment formats
- **[Documentation Artifacts](../DOCUMENTATION_ARTIFACTS.md)** - Analysis document structure

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial Product Prioritization Duty created from workflow migration (Phase 3.2) |
