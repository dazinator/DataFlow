# Scenario: Research Team Creates Backlog Item

## Context

Research team has completed research on a new feature and needs to hand over work to implementation team using the new product backlog system.

## Starting Point

- Copilot agent has completed research work
- Research folder exists: `/research/flow-composability-unification/`
- Research includes:
  - Findings in README.md
  - Design documents in `design/`
  - Prototype code that was tested
  - Benchmarks showing performance
- Reviewer has approved the research findings
- Code is ready to be reverted (research workflow requirement)

## Steps to Follow

Following Research Workflow with product backlog integration:

1. **Read copilot instructions** - Start from `.github/copilot-instructions.md`
2. **Navigate to Research Workflow** - Follow pointer to `.team/workflows/RESEARCH_WORKFLOW.md`
3. **Find handover section** - Research workflow should explain how to create backlog item
4. **Create backlog item file**:
   - Navigate to `/product/backlog/`
   - Create file: `research-2025-11-08-flow-composability-unification.md`
   - Fill in template with research findings
5. **Create handover folder**:
   - Create `/product/backlog/research-2025-11-08-flow-composability-unification/`
   - Copy prototype code to `prototype/` subfolder
   - Copy design docs to `design/` subfolder
   - Copy benchmarks to `benchmarks/` subfolder
6. **Link from research folder**:
   - Update research handover section to reference backlog item
   - Or create handover document that points to backlog item
7. **Notify product team** via comment on research PR

## Expected Outcome

- Backlog item file exists at `/product/backlog/research-2025-11-08-flow-composability-unification.md`
- Backlog item is properly formatted with all required sections
- Handover folder contains all research assets
- Research folder references the backlog item
- Product team is notified and can prioritize the item
- Research code gets reverted as normal

## Success Criteria

- [ ] Instructions were clear and unambiguous
- [ ] No gaps or missing information about backlog system
- [ ] Workflow led to expected outcome
- [ ] No confusion or back-tracking needed
- [ ] Backlog item is properly formatted and complete
- [ ] Handover folder structure is clear
- [ ] Integration with research workflow is smooth

## Test Result

**Status**: FAIL
**Notes**: Tabletop simulation revealed the following issues:

### Issues Found

1. **No mention of product backlog in Research Workflow**
   - Workflow talks about creating handover issues in `/research/[topic]/handover/`
   - No guidance to create product backlog items
   - No pointer to `/product/README.md`

2. **Unclear when to use backlog vs handover folder**
   - Old system: handover in research folder
   - New system: backlog item in product folder
   - No transition guidance

3. **Missing integration instructions**
   - No step explaining product backlog system
   - No reference to backlog item template
   - No guidance on naming convention

## Refinements Needed

### Research Workflow Updates Required

1. **Add new Phase 6.5: Create Product Backlog Item** (before code reversion)
   - Explain product backlog system
   - Reference `/product/README.md` for details
   - Provide step-by-step instructions
   - Explain backlog item naming convention
   - Explain handover folder structure

2. **Update Phase 6: Code Reversion section**
   - Mention that handover assets now go to product backlog
   - Update paths in examples

3. **Add Quick Navigation pointer**
   - Add "Product Backlog" to navigation at top
   - Link to `/product/README.md`

### Specific Content Needed

Add section before Phase 6:

```markdown
### Phase 6: Create Product Backlog Item

Before reverting code, create a backlog item in the product backlog system for implementation team.

**See `/product/README.md` for complete backlog system documentation.**

**Steps:**
1. Create backlog item file:
   - Path: `/product/backlog/research-YYYY-MM-DD-[name].md`
   - Use `/product/backlog-item-template.md` as template
   - Fill in all sections with research findings

2. Create handover folder (if you have assets):
   - Path: `/product/backlog/research-YYYY-MM-DD-[name]/`
   - Copy prototype code to `prototype/` subfolder
   - Copy design docs to `design/` subfolder (or reference existing)
   - Copy benchmarks to `benchmarks/` subfolder

3. Reference from research folder:
   - Create `/research/[topic]/handover/README.md`
   - Point to backlog item: "Implementation handover: See `/product/backlog/[item-id].md`"

4. Notify product team via PR comment
```

