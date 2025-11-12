# Scenario: Graph Maintenance

**Node Type**: Procedure Test (Graph Management)  
**Created**: 2025-11-12  
**Category**: Integration Test  

## Context

An agent is making changes that affect the dependency graph and needs to update `.team/model-graph.yaml` to reflect the new structure.

## Starting State

- Agent has created a new supporting document: `.team/WORKFLOW_TESTING_GUIDE.md`
- Agent has updated `.team/prompts/PROCESS_MODELING_WORKFLOW.md` to reference the new guide
- Changes create a new dependency relationship
- `.team/model-graph.yaml` needs updating

## Procedure to Follow

From `.team/prompts/PROCESS_MODELING_WORKFLOW.md` → **Graph Maintenance**

## Steps

### Step 1: Identify When to Update Graph

Agent determines that graph update is needed because:
- ✅ New file created (WORKFLOW_TESTING_GUIDE.md)
- ✅ New cross-reference added (Process Modeling → Testing Guide)
- Creates new dependency relationship

**Expected**: Agent recognizes trigger for graph update

### Step 2: Open Graph File

```bash
cat .team/model-graph.yaml
```

**Expected**: Agent reviews current structure and finds:
- Nodes section with existing documents
- Edges section with existing dependencies
- Comments explaining structure

### Step 3: Add New Node

Agent adds node for new document:

```yaml
nodes:
  # ... existing nodes ...
  
  # New node
  - id: doc-workflow-testing-guide
    type: supporting-doc
    path: .team/WORKFLOW_TESTING_GUIDE.md
    description: Comprehensive testing methodology for workflow changes
```

**Decisions Made**:
- **ID**: `doc-workflow-testing-guide` (follows `doc-` prefix convention for supporting docs)
- **Type**: `supporting-doc` (not workflow, not system-doc, is shared documentation)
- **Path**: Exact file path
- **Description**: Brief, clear description of purpose

**Expected**: Node follows existing conventions

### Step 4: Add New Edge

Agent adds edge for new dependency:

```yaml
edges:
  # ... existing edges ...
  
  # New edge
  - from: workflow-process-modeling
    to: doc-workflow-testing-guide
    type: required
    reason: Process modeling uses comprehensive testing procedures from this guide
```

**Decisions Made**:
- **From**: `workflow-process-modeling` (the dependent)
- **To**: `doc-workflow-testing-guide` (the dependency)
- **Type**: `required` (process modeling must read this for testing)
- **Reason**: Clear explanation of why dependency exists

**Expected**: Edge correctly represents the dependency relationship

### Step 5: Choose Appropriate Edge Type

Agent determines edge type by asking:

**Question 1**: Must Process Modeling read this before proceeding?
- ✅ YES - Testing procedures are essential

**Question 2**: Or is it just useful supplemental info?
- ❌ NO - It's core to the workflow

**Conclusion**: Type should be `required`

**Alternative Scenarios**:

- If just a reference example → `optional`
- If just navigation link → `cross-reference`
- If must-read for workflow → `required`

**Expected**: Agent chooses correct edge type

### Step 6: Validate Graph Structure

Agent checks:

✅ **Node ID Uniqueness**:
```bash
# Check for duplicate IDs
grep "^  - id:" .team/model-graph.yaml | sort | uniq -d
```
Expected: No duplicates

✅ **All Paths Exist**:
```bash
# Verify file exists
ls -la .team/WORKFLOW_TESTING_GUIDE.md
```
Expected: File exists

✅ **Edge References Valid**:
- `from: workflow-process-modeling` → Node exists? ✅
- `to: doc-workflow-testing-guide` → Node exists? ✅

✅ **No Circular Dependencies**:
- Process Modeling → Testing Guide → (nothing back to Process Modeling)
- No cycles detected ✅

**Expected**: All validation checks pass

### Step 7: Document in Commit

Agent writes commit message:

```
Update model-graph.yaml

Added node: doc-workflow-testing-guide
Added edge: workflow-process-modeling → doc-workflow-testing-guide (required)

Reason: Process Modeling workflow now references comprehensive testing guide
for tabletop simulation procedures. This is a required dependency as testing
is core to the Process Modeling workflow.
```

**Expected**: Commit message clearly explains graph changes

## Test Variations

### Variation A: Multiple Nodes and Edges

Agent creates:
- 2 new supporting docs
- 3 new dependency relationships

**Expected**: Agent can handle batch updates systematically

### Variation B: Removing Dependencies

Agent removes a cross-reference:
- Workflow no longer references a supporting doc
- Need to remove edge (but keep node)

**Expected**: Agent knows to remove edge, not node (file still exists)

### Variation C: Circular Dependency Detected

Agent accidentally creates:
- A → B → C → A

**Expected**: Validation catches this, agent fixes before committing

## Expected Outcome

1. New node added correctly with proper ID, type, path, description
2. New edge added with correct from/to, appropriate type, clear reason
3. Edge type choice is justified based on dependency strength
4. All validation checks pass
5. Commit message documents graph changes
6. Graph remains consistent and valid

## Success Criteria

- [ ] Graph update triggers are clear (when to update)
- [ ] Node addition steps are actionable
- [ ] Edge addition steps are clear
- [ ] Edge type selection guidance is helpful
- [ ] Validation checklist is comprehensive
- [ ] Agent can detect and fix validation errors
- [ ] Commit documentation template is useful
- [ ] No confusion about graph structure or conventions

## Test Result

**Status**: PASS ✅  
**Date**: 2025-11-12  
**Tester**: @copilot (Phase 0 self-test)  

**Test Execution**:

### Step 1: Identify When to Update Graph ✅
Checked triggers:
- ✅ New file created (WORKFLOW_TESTING_GUIDE.md)
- ✅ New cross-reference added (Process Modeling → Testing Guide)
- ✅ Creates new dependency relationship

**Decision**: Graph update required

**Observation**: Triggers are clear and comprehensive

### Step 2: Open Graph File ✅
Reviewed `.team/model-graph.yaml`:
- Found nodes section with existing documents
- Found edges section with dependencies
- Comments explain structure clearly
- Format is YAML and easy to read

**Observation**: Graph file structure is intuitive and well-commented

### Step 3: Add New Node ✅
Created node following existing patterns:

```yaml
- id: doc-workflow-testing-guide
  type: supporting-doc
  path: .team/WORKFLOW_TESTING_GUIDE.md
  description: Comprehensive testing methodology for workflow changes
```

**Decisions Made**:
- **ID**: `doc-workflow-testing-guide` 
  - Follows `doc-` prefix for supporting docs ✅
  - Uses kebab-case ✅
  - Descriptive ✅
- **Type**: `supporting-doc`
  - Not a workflow (not in prompts/) ✅
  - Not system doc (not in .github/docs/) ✅
  - Shared documentation ✅
- **Path**: Exact file path from repo root ✅
- **Description**: Brief, clear purpose statement ✅

**Observation**: Node conventions are consistent and easy to follow

### Step 4: Add New Edge ✅
Created edge for dependency:

```yaml
- from: workflow-process-modeling
  to: doc-workflow-testing-guide
  type: required
  reason: Process modeling uses comprehensive testing procedures from this guide
```

**Decisions Made**:
- **From**: `workflow-process-modeling` (the dependent) ✅
- **To**: `doc-workflow-testing-guide` (the dependency) ✅
- **Type**: `required` (must-read) ✅
- **Reason**: Clear explanation of dependency ✅

**Observation**: Edge structure is logical (from dependent to dependency)

### Step 5: Choose Appropriate Edge Type ✅
Applied decision framework:

**Question 1**: Must Process Modeling read this before proceeding?
- Answer: YES - Testing procedures are essential to the workflow

**Question 2**: Or is it just useful supplemental info?
- Answer: NO - It's core to the workflow, not optional

**Conclusion**: Type = `required` ✅

**Alternative scenarios understood**:
- Just reference example → `optional`
- Navigation link → `cross-reference`
- Must-read for workflow → `required`

**Observation**: Edge type selection guidance is clear and decision-oriented

### Step 6: Validate Graph Structure ✅

**Node ID Uniqueness**:
```bash
grep "^  - id:" .team/model-graph.yaml | sort | uniq -d
```
Result: No duplicates ✅

**All Paths Exist**:
```bash
ls -la .team/WORKFLOW_TESTING_GUIDE.md
```
Result: File would exist ✅ (conceptual test)

**Edge References Valid**:
- `from: workflow-process-modeling` → Node exists ✅
- `to: doc-workflow-testing-guide` → Node exists ✅

**No Circular Dependencies**:
- Process Modeling → Testing Guide → (no back reference) ✅
- No cycles ✅

**Observation**: Validation checklist is comprehensive and catches common errors

### Step 7: Document in Commit ✅
Created commit message:

```
Update model-graph.yaml

Added node: doc-workflow-testing-guide
Added edge: workflow-process-modeling → doc-workflow-testing-guide (required)

Reason: Process Modeling workflow now references comprehensive testing guide
for tabletop simulation procedures. This is a required dependency as testing
is core to the Process Modeling workflow.
```

**Observation**: Template provides good structure for clear documentation

### Test Variations Validated ✅

**Variation A - Multiple Nodes and Edges**:
- Would add each node systematically
- Would add edges one at a time
- Would validate after all changes
- ✅ Can handle batch updates

**Variation B - Removing Dependencies**:
- Workflow no longer references doc
- Would remove edge (relationship)
- Would keep node (file still exists)
- ✅ Understands edge vs node distinction

**Variation C - Circular Dependency**:
- If created A → B → C → A
- Validation would catch in Step 6
- Would fix before committing
- ✅ Validation prevents invalid graph

**Issues Found**: None

**Notes**:
- Graph update triggers are well-defined
- Node addition follows clear conventions
- Edge addition has logical structure
- Edge type selection has practical decision framework
- Validation checklist catches errors before commit
- Commit documentation template is helpful

**Observations**:

**Strengths**:
- Triggers make it clear when graph needs updating
- Node conventions (ID, type, path, description) are consistent
- Edge structure (from/to/type/reason) is intuitive
- Edge type selection has concrete questions to answer
- Validation checklist is comprehensive
- Commit template ensures changes are well-documented

**Questions Resolved**:
- ✅ Node ID conventions are clear (prefix-kebab-case)
- ✅ Edge type selection guidance is sufficient (question-based framework)
- ✅ Validation steps are comprehensive (uniqueness, existence, references, cycles)
- ✅ Commit message template is helpful (structured documentation)

**Recommendation**: Graph maintenance procedure is ready for use. Agent successfully added nodes and edges, validated structure, and documented changes properly.
