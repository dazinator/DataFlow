# Workflow Maintenance Guide

This guide provides systematic patterns for maintaining workflow documentation, including parameter management and issue template simplification.

**Scope**: Process Modeling workflow only

**When to Use**: When creating or updating workflow documentation files (`.team/prompts/*_WORKFLOW.md`) or related issue templates.

---

## Workflow Parameter Management

When creating or updating workflows, systematically identify and manage configurable parameters to improve maintainability and customization.

### What Are Workflow Parameters?

**Workflow parameters** are configurable values that drive workflow algorithms and behavior:
- **Work-in-progress limits** (e.g., `IMPLEMENTATION_QUEUE_LIMIT = 10`)
- **Pagination sizes** (e.g., `SELECTION_BATCH_SIZE = 100`)
- **Thresholds and criteria** (e.g., `STALE_THRESHOLD_DAYS = 180`)
- **Similarity ratios** (e.g., `DUPLICATE_SIMILARITY_THRESHOLD = 0.8`)

**NOT workflow parameters** (keep inline):
- Example values used for explaining concepts
- Informational time estimates (e.g., "5-15 minutes typical")
- Illustrative thresholds in documentation (e.g., "use <10/min for low frequency")
- Context-specific values in scenarios

### When to Extract Parameters

Extract values to a separate params file when:

1. ✅ **Algorithmic use**: Value is used in workflow decision logic or algorithms
2. ✅ **Configurability**: Value might need adjustment based on team/project needs
3. ✅ **Multiple references**: Value is referenced multiple times in the workflow
4. ✅ **Explicit customization point**: Designed to be changed by users

**Do NOT extract when**:

1. ❌ **Example values**: Used to explain concepts (e.g., "typical PR has 3-7 files")
2. ❌ **Informational estimates**: Time or scope estimates (e.g., "30 minutes typical")
3. ❌ **Scenario-specific**: Context-dependent values in examples
4. ❌ **One-off references**: Value used once for illustration

### How to Structure Parameter Files

**Naming Convention:**
```
[WORKFLOW_NAME]_PARAMS.md
```

**Examples:**
- `PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md`
- `TRIAGE_WORKFLOW_PARAMS.md`
- `RESEARCH_WORKFLOW_PARAMS.md`

**File Structure** (follow PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md pattern):

```markdown
# [Workflow Name] Parameters

Configuration parameters for the [Workflow Name] workflow (`.team/prompts/[WORKFLOW_NAME].md`).

## Core Parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `PARAM_NAME` | `value` | What this parameter controls |
| `ANOTHER_PARAM` | `value` | What this controls |

## [Category Name] (if needed)

| Parameter | Value | Description |
|-----------|-------|-------------|
| `PARAM_NAME` | `value` | Description |

## Notes (optional)

Additional context about parameter usage, dependencies, or rationale.
```

### Updating Workflows to Reference Parameters

When params file exists, reference it in the workflow:

**At the top of the workflow** (after overview, before detailed steps):

```markdown
## Configuration

**📖 See [Workflow Parameters](./<WORKFLOW_NAME>_PARAMS.md)** for configurable values.

Key parameters:
- `PARAM_NAME`: Brief description
- `ANOTHER_PARAM`: Brief description
```

**In code examples** within the workflow:

```python
# Reference parameter from params file
SELECTION_BATCH_SIZE = 100  # From WORKFLOW_NAME_PARAMS.md

items = list_issues(
    owner="...",
    repo="...",
    perPage=SELECTION_BATCH_SIZE  # Use parameter
)
```

### Decision Framework

Use this framework when encountering numeric values in workflows:

```
Question 1: Is this value used in workflow logic/algorithms?
├─ NO → Keep inline (it's illustrative)
└─ YES → Continue to Question 2

Question 2: Should users be able to customize this value?
├─ NO → Keep inline (it's a fixed constant)
└─ YES → Continue to Question 3

Question 3: Is this value referenced multiple times?
├─ NO → Consider inline with comment about customization
└─ YES → Extract to params file

Result: Extract to [WORKFLOW_NAME]_PARAMS.md
```

### Examples

**Extract to Params File:**
```python
# ✅ GOOD - Extracted
IMPLEMENTATION_QUEUE_LIMIT = 10  # From PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md
```

**Keep Inline (Example Value):**
```markdown
# ✅ GOOD - Kept inline
For high-frequency scenarios (>100/min), use event-driven approach.
For low-frequency scenarios (<10/min), simple polling is sufficient.
```

**Keep Inline (Informational):**
```markdown
# ✅ GOOD - Kept inline
**Typical Duration**: 5-15 minutes per issue (single mode)
```

### Retrospective Parameter Extraction

When updating existing workflows:

1. **Review for parameters**: Scan workflow for numeric values
2. **Apply decision framework**: Determine what should be extracted
3. **Create params file**: Follow structure pattern above
4. **Update workflow**: Reference params file
5. **Document in PR**: Note parameter extraction in PR description
6. **Test scenarios**: Verify workflow still works with params file

### Maintenance Considerations

**When adding new parameters:**
- Add to existing params file if workflow already has one
- Keep table format consistent
- Document rationale for chosen values

**When changing parameter values:**
- Update only the params file
- Document reason for change (comment or PR description)
- Consider impact on existing workflows using that parameter

**When removing parameters:**
- Remove from params file
- Update workflow to remove references
- Document why parameter is no longer needed

---

## Issue Template Simplification

GitHub issue templates should focus on gathering context, not duplicating workflow procedural details.

### Core Principle

**Templates point to workflows, they don't duplicate them.**

- ✅ **Issue template role**: Gather context, set expectations, reference workflow
- ✅ **Workflow document role**: Contain detailed procedural steps and algorithms
- ✅ **Label + copilot-instructions.md role**: Route agents to correct workflow

**Why this matters:**
- **Maintenance burden**: Changes in one place (workflow doc) not two (template + workflow)
- **Single source of truth**: Workflow doc is authoritative
- **Clarity**: Users fill in context, agents follow detailed workflow steps
- **Flexibility**: Workflows can evolve without template changes

### What Belongs in Issue Templates

**DO include in templates:**
- Context gathering fields (research objectives, success criteria, etc.)
- Links to relevant workflow documentation
- Clear statement of which workflow applies
- Essential warnings or prerequisites
- Outcome expectations (high-level)

**DON'T include in templates:**
- Step-by-step procedural instructions (those go in workflow docs)
- Detailed algorithmic logic (workflow docs)
- Checklists that duplicate workflow steps (workflow docs)
- "For @copilot" procedural sections (copilot-instructions.md + workflow doc handles routing)

### Template Simplification Pattern

**Before (Duplicative):**
```markdown
## For @copilot

**Investigation Steps:**
1. Read `.team/prompts/PROCESS_MODELING_WORKFLOW.md` for complete workflow
2. Update `/research/workflow-modeling/plan.md` to track this work
3. Create test scenarios in `/research/workflow-modeling/scenarios/`
4. Validate changes through tabletop simulation
5. Update relevant documentation based on test results
6. Archive successful scenarios (or revert if appropriate)
7. Complete self-improvement evaluation

### Checklist for @copilot
- [ ] I have read workflow documentation
- [ ] I will create research folder structure
- [ ] I will revert exploratory code after approval
- [ ] I will create implementation issue
```

**After (Simplified):**
```markdown
## For @copilot

**Workflow**: Follow `.team/prompts/PROCESS_MODELING_WORKFLOW.md` for complete process.

**Quick Reference**:
- This issue uses the `workflow:process-modeling` label
- See copilot-instructions.md for workflow routing
- Complete self-improvement evaluation before PR review
```

**Key changes:**
- Removed duplicate procedural steps (already in workflow doc)
- Kept essential reference to workflow doc
- Noted workflow label (routing mechanism)
- Kept critical reminder (self-improvement)

### Template Review Checklist

When reviewing issue templates for simplification:

- [ ] **Identify duplicated content**: What appears in both template and workflow doc?
- [ ] **Keep context fields**: Research objectives, success criteria, constraints
- [ ] **Remove procedural steps**: These belong in workflow doc
- [ ] **Strengthen workflow reference**: Clear link to workflow doc
- [ ] **Verify label routing**: Correct workflow label applied
- [ ] **Test navigation**: Can agent find workflow doc from template?
- [ ] **Update "For @copilot" section**: Point to workflow, don't duplicate

### Common Template Antipatterns

**Antipattern 1: Procedural Checklists**
```markdown
# ❌ BAD - Duplicates workflow
### Checklist for @copilot
- [ ] Read workflow documentation
- [ ] Create research folder
- [ ] Execute steps 1-10 from workflow
- [ ] Complete self-improvement evaluation
```

**Better:**
```markdown
# ✅ GOOD - References workflow
**Workflow**: Follow `.team/prompts/RESEARCH_WORKFLOW.md`
**Note**: Complete self-improvement evaluation before PR review
```

**Antipattern 2: Algorithm Details**
```markdown
# ❌ BAD - Contains algorithmic logic
Query backlog issues:
1. Filter by workflow:product-backlog label
2. Sort by priority
3. Check implementation queue capacity
4. Select top N items where N = LIMIT - current_count
5. Apply security priority boost...
```

**Better:**
```markdown
# ✅ GOOD - Points to workflow
**Workflow**: See `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md` for selection algorithm.
```

**Antipattern 3: Detailed Instructions**
```markdown
# ❌ BAD - Step-by-step duplication
**Steps:**
1. Create /research/[topic]/ folder with plan.md
2. Write exploratory code in /poc/ or /src/
3. Document findings in research folder
4. Create handover issue using template
5. Save prototype code to handover/prototype/
6. After approval, revert all code changes
7. Only docs remain for review
```

**Better:**
```markdown
# ✅ GOOD - High-level outcome
**Workflow**: Follow `.team/prompts/RESEARCH_WORKFLOW.md`
**Outcome**: Research findings + implementation-ready issue (exploratory code reverted)
```

### Retrospective Template Simplification

As part of process modeling workflow, review issue templates:

1. **Identify duplication**: Compare template with workflow doc
2. **Apply simplification pattern**: Remove procedural content
3. **Strengthen references**: Clear links to workflow docs
4. **Create test scenario**: Verify agent can navigate from template to workflow
5. **Test workflow**: Confirm template still provides needed context
6. **Document changes**: Note simplification in PR description

### Example: Simplified Research Template

**Key Simplifications:**
- **Removed**: Step-by-step checklist (duplicates RESEARCH_WORKFLOW.md)
- **Removed**: Detailed "Key Points" section (already in workflow)
- **Kept**: Context fields (research objective, questions, validation approach)
- **Kept**: High-level outcome expectations
- **Strengthened**: Reference to RESEARCH_WORKFLOW.md
- **Result**: Template focuses on context gathering, workflow doc contains procedures

This pattern ensures:
- ✅ Templates remain focused and scannable
- ✅ Workflow docs are single source of truth for procedures
- ✅ Maintenance is easier (update one place)
- ✅ Agents can navigate clearly from template to detailed workflow
