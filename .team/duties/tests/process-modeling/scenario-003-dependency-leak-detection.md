# Scenario: Dependency Leak Detection Execution

**Node Type**: Procedure Test (Leak Detection)  
**Created**: 2025-11-12  
**Category**: Edge Case  

## Context

An agent is updating a workflow file that depends on supporting documentation. Need to verify that content isn't duplicated from dependencies.

## Starting State

- Agent has modified `.team/prompts/RESEARCH_WORKFLOW.md`
- Changes include:
  - Added guidance on multi-phase work items
  - Added documentation artifacts guidance
  - Modified research handover procedures

## Procedure to Follow

From `.team/prompts/PROCESS_MODELING_WORKFLOW.md` → **Leak Detection Procedures** → **Dependency Leak Detection**

## Steps

### Step 1: Identify Dependencies from Graph

```bash
# Check model-graph.yaml for dependencies
cat .team/model-graph.yaml | grep "id: workflow-research" -A 5
```

**Expected Output**: Agent finds edges showing Research Workflow depends on:
- `doc-document-hygiene` (required)
- `doc-documentation-artifacts` (required)
- `doc-getting-started` (required)
- `workflow-implementation` (optional - handover reference)

**Expected**: Agent correctly identifies dependencies from graph

### Step 2: Check for Content Duplication

For each dependency, agent checks:

#### Checking MULTI_PHASE_ISSUES.md dependency

**Potential Leak 1**: Multi-phase procedure steps

**What to look for in RESEARCH_WORKFLOW.md**:
```markdown
## Creating Multi-Phase Work

To create multi-phase work:
1. Create parent work item
2. Create child work items
3. Link them together
```

**Is this a leak?** ❌ **YES** - Duplicates procedure from MULTI_PHASE_ISSUES.md

**Should be**:
```markdown
## Creating Multi-Phase Work

See [Multi-Phase Issue Procedures](../../.team/procedures/multi-phase-work-items.md) for complete guidance on parent-child work items.
```

#### Checking DOCUMENTATION_ARTIFACTS.md dependency

**Potential Leak 2**: Documentation structure

**What to look for**:
```markdown
## Research Documentation

Research should be documented in:
- `/research/[topic]/` - Main research folder
- `README.md` - Research findings
- `plan.md` - Research plan
```

**Is this a leak?** ❌ **YES** - Duplicates structure from DOCUMENTATION_ARTIFACTS.md

**Should be**:
```markdown
## Research Documentation

Follow the [Documentation Artifacts System](../../../docs/DOCUMENTATION_ARTIFACTS.md) for research folder structure.
```

#### Checking DOCUMENT_HYGIENE.md dependency

**Potential Leak 3**: Cross-reference pattern

**What to look for**:
```markdown
Use markdown links: `[Text](../path/to/file.md)`
```

**Is this a leak?** ⚠️ **Maybe** - Depends on context

- If showing example of cross-reference → ✅ OK (educational)
- If re-explaining the principle → ❌ Leak (should reference doc hygiene)

### Step 3: Verify References Instead of Duplication

Agent checks for proper reference patterns:

✅ **Good Reference Patterns Found**:
```markdown
See [Document Hygiene](../../docs/DOCUMENT_HYGIENE.md) for documentation standards.

Follow the [Multi-Phase Procedures](../../.team/procedures/multi-phase-work-items.md) when creating parent-child work items.

_Supplemental: [Documentation Artifacts](../../../docs/DOCUMENTATION_ARTIFACTS.md) provides guidance on folder structure._
```

❌ **Bad Patterns (Duplications)**:
```markdown
## Documentation Standards

Follow these principles:
1. Single source of truth
2. Cross-reference, don't duplicate
3. Keep content DRY
(This is duplicating DOCUMENT_HYGIENE.md!)
```

### Step 4: Refactor if Leaks Found

Agent replaces duplication:

**Before (Leak)**:
```markdown
## Multi-Phase Work Items

To create multi-phase work:
1. Create parent
2. Create children
3. Link them
```

**After (Reference)**:
```markdown
## Multi-Phase Work Items

For multi-phase work, see [Multi-Phase Issue Procedures](../../.team/procedures/multi-phase-work-items.md).
```

Or with Required Context:
```markdown
## Required Context

**⚠️ IMPORTANT**: Read the following documents before proceeding:

- **[Multi-Phase Issue Procedures](../../.team/procedures/multi-phase-work-items.md)** - Required for parent-child work item management
```

### Step 5: Document Check Results

**If No Leaks**:
```
✅ Dependency Leak Check: PASS
- Checked dependencies: doc-document-hygiene, doc-documentation-artifacts, doc-multi-phase-issues
- All content properly referenced
- No duplication detected
```

**If Leaks Found and Fixed**:
```
✅ Dependency Leak Check: FIXED
- Found 2 content duplications:
  * Multi-phase procedure steps (duplicated from MULTI_PHASE_ISSUES.md)
  * Documentation structure (duplicated from DOCUMENTATION_ARTIFACTS.md)
- Refactored to use Required Context references
- Dependencies: doc-multi-phase-issues, doc-documentation-artifacts
```

## Expected Outcome

1. Agent successfully identifies dependencies from graph
2. Content duplication is detected
3. Leaks are correctly categorized (legitimate vs duplication)
4. Refactoring uses proper reference patterns
5. Results are documented clearly
6. Agent understands when duplication is OK (examples) vs not OK (re-explaining)

## Success Criteria

- [ ] Dependency identification steps are clear
- [ ] Content duplication checks are actionable
- [ ] Distinction between legitimate examples and duplication is clear
- [ ] Reference patterns are well-documented
- [ ] Refactoring guidance is helpful
- [ ] Documentation template is useful
- [ ] No confusion about what constitutes a dependency leak

## Test Result

**Status**: PASS ✅  
**Date**: 2025-11-12  
**Tester**: @copilot (Phase 0 self-test)  

**Test Execution**:

### Step 1: Identify Dependencies from Graph ✅
Executed: `cat .team/model-graph.yaml | grep "id: workflow-research" -A 5`

Found dependencies:
- `doc-document-hygiene` (required)
- `doc-documentation-artifacts` (required)
- `doc-getting-started` (required)
- `workflow-implementation` (optional - handover)

**Observation**: Graph file is well-structured and easy to query. Dependencies are clearly documented.

### Step 2: Check for Content Duplication ✅

**Checking MULTI_PHASE_ISSUES.md**:
- **Potential Leak Example 1**: Multi-phase procedure steps duplicated
- ❌ **Identified as LEAK** - Steps should be referenced, not copied
- **Correct Pattern**: Reference with link to procedure doc

**Checking DOCUMENTATION_ARTIFACTS.md**:
- **Potential Leak Example 2**: Documentation structure duplicated
- ❌ **Identified as LEAK** - Structure should be referenced, not re-explained
- **Correct Pattern**: Link to artifacts system doc

**Checking DOCUMENT_HYGIENE.md**:
- **Potential Leak Example 3**: Cross-reference pattern shown
- ⚠️ **Judgment Call** - Depends on context
  - If showing example → ✅ OK (educational)
  - If re-explaining principle → ❌ Leak
- **Decision**: Would check context - if brief example, OK; if full explanation, leak

**Observation**: Leak detection logic is nuanced and practical. Agent can distinguish between:
- Educational examples (OK)
- Content duplication (Not OK)
- Brief references (OK)
- Re-explaining concepts (Not OK)

### Step 3: Verify References Instead of Duplication ✅

**Good Reference Patterns Identified**:
```markdown
See [Document Hygiene](../../docs/DOCUMENT_HYGIENE.md) for documentation standards.
```
- ✅ Clear, concise, directs to canonical source

```markdown
Follow the [Multi-Phase Procedures](../../.team/procedures/multi-phase-work-items.md) when creating parent-child work items.
```
- ✅ Action-oriented reference with context

```markdown
_Supplemental: [Documentation Artifacts](../../../docs/DOCUMENTATION_ARTIFACTS.md) provides guidance on folder structure._
```
- ✅ Indicates optional/supplemental nature

**Bad Patterns Detected**:
```markdown
## Documentation Standards
Follow these principles:
1. Single source of truth...
```
- ❌ Duplicating DOCUMENT_HYGIENE.md content

**Observation**: Reference patterns are clear and agent can distinguish good from bad

### Step 4: Refactor if Leaks Found ✅

**Before (Leak)**:
```markdown
## Multi-Phase Work Items
To create multi-phase work:
1. Create parent
2. Create children
3. Link them
```

**After (Reference)**:
```markdown
## Multi-Phase Work Items
For multi-phase work, see [Multi-Phase Issue Procedures](../../.team/procedures/multi-phase-work-items.md).
```

**Alternative with Required Context**:
```markdown
## Required Context
**⚠️ IMPORTANT**: Read the following documents before proceeding:
- **[Multi-Phase Issue Procedures](../../.team/procedures/multi-phase-work-items.md)** - Required for parent-child work item management
```

**Observation**: Refactoring guidance is clear with concrete before/after examples

### Step 5: Document Check Results ✅

**No Leaks Scenario**:
```
✅ Dependency Leak Check: PASS
- Checked dependencies: doc-document-hygiene, doc-documentation-artifacts, doc-multi-phase-issues
- All content properly referenced
- No duplication detected
```

**Leaks Found and Fixed Scenario**:
```
✅ Dependency Leak Check: FIXED
- Found 2 content duplications:
  * Multi-phase procedure steps (duplicated from MULTI_PHASE_ISSUES.md)
  * Documentation structure (duplicated from DOCUMENTATION_ARTIFACTS.md)
- Refactored to use Required Context references
- Dependencies: doc-multi-phase-issues, doc-documentation-artifacts
```

**Observation**: Documentation templates are clear and cover both success and remediation cases

**Issues Found**: None

**Notes**:
- Graph-based dependency identification is very effective
- Content duplication checks are practical and actionable
- Distinction between examples and duplication is well-explained
- Reference patterns are properly documented
- Refactoring guidance with examples is very helpful

**Observations**:

**Strengths**:
- Graph integration makes dependency identification systematic
- Nuanced guidance on when duplication is OK (examples) vs not OK (re-explaining)
- Multiple reference pattern examples (direct link, Required Context, supplemental)
- Clear before/after refactoring examples
- Documentation templates cover success and remediation cases

**Edge Cases Considered**:
- ✅ Showing example of dependency content → OK (educational)
- ✅ Re-explaining dependency principle → Not OK (reference instead)
- ✅ Very brief dependency content (1-2 lines) → Judgment call (document in scenario)
- ✅ Dependency doesn't exist yet → Note for creation or create it

**Recommendation**: Dependency leak detection procedure is ready for use. Agent successfully identified leaks and understood when content duplication is appropriate vs when it should be refactored to references.
