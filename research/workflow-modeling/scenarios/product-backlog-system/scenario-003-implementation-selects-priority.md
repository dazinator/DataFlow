# Scenario: Implementation Team Selects From Prioritized Backlog

## Context

Implementation team receives a new implementation GitHub issue. The issue doesn't specify a particular backlog item, so they need to select the highest priority item from the backlog.

## Starting Point

- New GitHub issue created using implementation template
- Issue field says: "Backlog Item: Next from prioritization list"
- Copilot agent assigned to the issue
- No specific backlog item specified
- Agent needs to find and propose highest priority item

## Steps to Follow

Following Implementation Workflow with product backlog integration:

1. **Read copilot instructions** - Start from `.github/copilot-instructions.md`
2. **Navigate to Implementation Workflow** - Follow pointer to `.team/workflows/IMPLEMENTATION_WORKFLOW.md`
3. **Find backlog selection section** - Workflow should explain how to select from backlog
4. **Check prioritization file**:
   - Navigate to `/product/prioritization.md`
   - Read the prioritization list
   - Identify highest priority item (Priority 1)
5. **Pause and propose**:
   - Comment on PR/issue: "Highest priority item is: [item-id] - [title]"
   - Wait for reviewer confirmation
6. **After confirmation**:
   - Read backlog item file completely
   - Review handover assets (if folder exists)
   - Update backlog item status to "In Progress"
   - Proceed with implementation
7. **After completion**:
   - Update backlog item status to "Completed"
   - Add PR link to backlog item
   - Move to `/product/resolved/YYYY-MM/`

## Expected Outcome

- Agent correctly finds prioritization file
- Agent identifies highest priority item
- Agent pauses and comments with selection (doesn't just start)
- Reviewer confirms
- Agent proceeds with clear understanding of what to implement
- Backlog item status is updated appropriately
- Item is archived when complete

## Success Criteria

- [ ] Instructions clearly explained where to find prioritization
- [ ] Priority selection logic was clear (highest = Priority 1)
- [ ] Workflow clearly indicated to pause and comment
- [ ] No gaps in handover reading instructions
- [ ] Status update steps were clear
- [ ] Archive process was clear
- [ ] No confusion or back-tracking needed
- [ ] Integration with implementation workflow was smooth

## Test Result

**Status**: FAIL
**Notes**: Tabletop simulation revealed the following issues:

### Issues Found

1. **Workflow still references old backlog location**
   - Line 100: "If implementing from tech debt backlog (`/research/backlog/`)"
   - Should reference `/product/backlog/`
   - Instructions for reading backlog README point to wrong location

2. **No guidance on selecting from prioritization**
   - No mention of `/product/prioritization.md`
   - No instructions on how to identify highest priority
   - No guidance on pausing to comment

3. **No clear integration point for product backlog**
   - Step 2 talks about research handovers and old tech debt backlog
   - Need new "Step 2: Read Product Backlog Item" section
   - Should handle both specific item and "next priority" cases

4. **Status update instructions missing**
   - No guidance on updating backlog item status
   - No archiving instructions for completed items
   - No reference to `/product/README.md` for procedures

## Refinements Needed

### Implementation Workflow Updates Required

1. **Update Step 2: Read Handover Document**
   - Change to "Step 2: Read Product Backlog Item"
   - Add section for selecting from prioritization
   - Add section for reading backlog item
   - Handle both specific item and "next priority" cases
   - Reference `/product/README.md`

2. **Add backlog item status management**
   - Instructions to update status to "In Progress" when starting
   - Instructions to update status to "Completed" when done
   - Instructions to archive to `/product/resolved/YYYY-MM/`
   - Reference `/product/README.md` for details

3. **Update backlog references throughout**
   - Replace `/research/backlog/` with `/product/backlog/`
   - Update examples and paths

### Specific Content Needed

Replace Step 2 with:

```markdown
## Step 2: Read Product Backlog Item

All implementation work should reference a product backlog item. See `/product/README.md` for complete backlog system documentation.

### If Backlog Item Specified

Issue will specify backlog item path like:
- `Backlog Item: research-2025-11-08-flow-composability`
- `Backlog Path: /product/backlog/research-2025-11-08-flow-composability.md`

**Steps:**
1. Read backlog item file completely
2. Review handover folder if exists (`/product/backlog/[item-id]/`)
3. Update backlog item status to "In Progress"
4. Proceed with implementation

### If "Next from Prioritization" Specified

Issue will say:
- `Backlog Item: Next from prioritization list`

**Steps:**
1. Open `/product/prioritization.md`
2. Identify highest priority item (Priority 1, or lowest number available)
3. **PAUSE**: Comment on issue with selected item:
   ```
   Selected highest priority item:
   - ID: [item-id]
   - Title: [title]
   - Priority: [N]
   
   Awaiting confirmation to proceed.
   ```
4. **WAIT** for reviewer confirmation
5. After confirmation, follow "Backlog Item Specified" steps above

### Reading Backlog Item

1. **Read backlog item file** at `/product/backlog/[item-id].md`:
   - Summary and context
   - Implementation guidance
   - Success criteria
   - References

2. **Review handover assets** (if handover folder exists):
   - Prototype code in `[item-id]/prototype/`
   - Design docs in `[item-id]/design/`
   - Benchmarks in `[item-id]/benchmarks/`

3. **Update status to "In Progress"**:
   ```markdown
   **Status**: In Progress
   **Updated**: YYYY-MM-DD
   ```

### After Implementation Complete

1. Update backlog item:
   ```markdown
   **Status**: Completed
   **Updated**: YYYY-MM-DD
   
   ## Status History
   - **YYYY-MM-DD**: Completed (PR: #[N])
   ```

2. Archive backlog item:
   ```bash
   # Create monthly archive folder if needed
   mkdir -p product/resolved/$(date +%Y-%m)
   
   # Move backlog item and handover folder
   mv product/backlog/[item-id].md product/resolved/$(date +%Y-%m)/
   mv product/backlog/[item-id]/ product/resolved/$(date +%Y-%m)/ # if exists
   ```

See `/product/README.md` for detailed archiving procedures.
```

