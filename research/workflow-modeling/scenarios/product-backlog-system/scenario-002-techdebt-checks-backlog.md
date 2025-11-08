# Scenario: Tech Debt Team Checks and Adds to Backlog

## Context

Tech debt team is conducting analysis and has discovered a potential improvement. They need to check if it's already in the backlog, and if not, add it.

## Starting Point

- Copilot agent is conducting tech debt analysis
- Analysis folder exists: `/research/tech-debt-2025-11-10/`
- Discovery: "Add XML documentation to public APIs" identified
- Need to check if this is already in backlog before creating new item
- If not in backlog, need to add it

## Steps to Follow

Following Tech Debt Workflow with product backlog integration:

1. **Read copilot instructions** - Start from `.github/copilot-instructions.md`
2. **Navigate to Tech Debt Workflow** - Follow pointer to `.team/workflows/TECH_DEBT_WORKFLOW.md`
3. **Find backlog check section** - Workflow should explain to check existing backlog first
4. **Search existing backlog**:
   - Read `/product/README.md` to understand how to search
   - Use grep or file listing to search for related items
   - Check if "XML documentation" or similar already exists
5. **If item exists**:
   - Read existing item
   - Update if needed (add notes, update priority recommendation)
   - Reference in tech debt findings
6. **If item doesn't exist**:
   - Create new file: `techdebt-2025-11-10-add-xml-documentation.md`
   - Fill in template with finding details
   - Add to tech debt findings report
7. **Complete tech debt analysis** per normal workflow

## Expected Outcome

Case 1 (item already exists):
- Agent finds existing backlog item
- Updates it if needed or just references it
- Avoids creating duplicate
- Tech debt report references existing item

Case 2 (item is new):
- Agent creates new backlog item with proper format
- Item is added to backlog without duplication
- Tech debt report references new item

## Success Criteria

- [ ] Instructions were clear about when/how to check backlog
- [ ] Search guidance in `/product/README.md` was sufficient
- [ ] No gaps in workflow documentation
- [ ] Agent correctly identified duplicate (Case 1) or new item (Case 2)
- [ ] Backlog item format was clear
- [ ] Integration with tech debt workflow was smooth
- [ ] No confusion about which system to use (old vs new)

## Test Result

**Status**: FAIL
**Notes**: Tabletop simulation revealed the following issues:

### Issues Found

1. **Workflow points to old backlog location**
   - Line 164: `review /research/backlog/` 
   - Should be `/product/backlog/`
   - Old search examples use wrong path

2. **No mention of product backlog system**
   - No reference to `/product/README.md`
   - No explanation of new backlog structure
   - No pointer to backlog item template

3. **File naming convention not explained**
   - Old system: `YYYY-MM-DD-[name].md`
   - New system: `techdebt-YYYY-MM-DD-[name].md`
   - Need to explain the source prefix

4. **Unclear when to add vs update**
   - No clear guidance on updating existing items
   - No example of when to merge vs keep separate

## Refinements Needed

### Tech Debt Workflow Updates Required

1. **Update Phase 1, Step 3: Review Existing Backlog**
   - Change path from `/research/backlog/` to `/product/backlog/`
   - Reference `/product/README.md` for backlog system
   - Update search examples with new paths
   - Explain filtering for `techdebt-*` items

2. **Add guidance on creating backlog items**
   - Reference `/product/backlog-item-template.md`
   - Explain `techdebt-` prefix naming convention
   - Provide example

3. **Update backlog item structure section**
   - Point to `/product/README.md` for format
   - Remove duplicate information (DRY principle)

### Specific Content Needed

Update Phase 1, Step 3:

```markdown
**3. Review Existing Product Backlog**

**Before starting new exploration**, review `/product/backlog/` for existing items:

See `/product/README.md` for complete backlog system documentation.

```bash
# List all tech debt backlog items
ls -lt product/backlog/techdebt-*.md

# Search for high-priority items
grep -l "Priority: High" product/backlog/techdebt-*.md

# Search by keyword
grep -i "keyword" product/backlog/*.md
```

**For each backlog item found**:
1. **Validate it's still relevant** - Has the code changed? Is it still an issue?
2. **If valid and similar to new finding** - Update existing item instead of creating new one
3. **If no longer valid** - Recommend archiving
4. **If new finding** - Create new backlog item following format in `/product/README.md`
```

Add after findings section:

```markdown
### Adding Findings to Product Backlog

For NEW findings (not already in backlog):

1. Create backlog item file in `/product/backlog/`:
   - Naming: `techdebt-YYYY-MM-DD-[short-name].md`
   - Use `/product/backlog-item-template.md` as template
   - Fill in all sections

2. Reference in findings report

For EXISTING backlog items:
- Add note to existing item about re-validation
- Update priority if changed
- Reference in findings report
```

