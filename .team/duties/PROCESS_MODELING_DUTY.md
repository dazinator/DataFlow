# Process Modeling Duty

**Purpose**: Improve workflows, procedures, and processes through systematic testing and refinement

**Layer**: 2 (Duty - specialized procedure)

**Version**: 1.0  
**Created**: 2025-11-12

---

## Required Context

**⚠️ IMPORTANT**: Read the following documents before proceeding:

### Design Documents (Architecture Understanding)

- **[Main Design](../../docs/design/prompt-engineering/README.md)** - Complete architecture with layered model
- **[Core Concepts](../../docs/design/prompt-engineering/concepts.md)** - Layer 2 definition, terminology, OS analogy
- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Semantic operations specification
- **[Testing Framework](../../docs/design/prompt-engineering/testing-framework.md)** - Change procedures, leak detection, graph management

### Procedures and Kernel

- **[Kernel Layer](../kernel/README.md)** - Platform abstraction layer
- **[Global Procedures](../procedures/README.md)** - All global procedures
- **[Duty Assignment Procedure](../procedures/duty-assignment.md)** - Determining duty from work items
- **[Work Item Creation Procedure](../procedures/work-item-creation.md)** - Creating new work items
- **[Comment Patterns Procedure](../procedures/comment-patterns.md)** - Standard comment formats
- **[Handover Procedure](../procedures/handover.md)** - Transitioning work items between duties
- **[Self-Improvement Procedure](../procedures/self-improvement.md)** - Feedback submission

### Supporting Documentation

- **[Document Hygiene](../DOCUMENT_HYGIENE.md)** - Core documentation principles
- **[Documentation Artifacts](../DOCUMENTATION_ARTIFACTS.md)** - Global documentation structure

**Semantic Operations Used**:
- `query_work_items_by_duty(duty)` - Find work items assigned to this duty
- `get_work_item_details(work_item_id)` - Retrieve work item information
- `get_work_item_duty(work_item_id)` - Get current duty assignment
- `add_work_item_comment(work_item_id, text)` - Add comment
- `update_work_item(work_item_id, fields)` - Update work item fields
- `get_parent_work_item(work_item_id)` - Get parent if exists
- `is_multi_phase(work_item_id)` - Check if part of multi-phase plan
- `submit_feedback(work_item_id, feedback_text)` - Submit self-improvement feedback

---

## Overview

**Purpose**: Systematically improve and refine team workflows and processes through testing, validation, and iterative refinement.

**Entry Point**: Work items requesting process improvements, workflow updates, or discovered inefficiencies

**Typical Duration**: 3-7 days depending on scope

**Key Principle**: Process modeling produces **refined workflow documentation** through tabletop simulation testing. This is a specialized variant of the Research Duty for improving workflows and processes.

---

## Exclusive File Ownership

**⚠️ CRITICAL**: Process Modeling duty has EXCLUSIVE OWNERSHIP of all workflow-related files:

### Files Owned by Process Modeling

- `.team/duties/*_DUTY.md` - All duty documentation files  
- `.team/prompts/*_WORKFLOW.md` - All workflow documentation files (legacy)
- `.github/copilot-instructions.md` - Copilot agent instructions
- `.github/ISSUE_TEMPLATE/*.md` - All issue templates
- `.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md` - Workflow system documentation
- `.team/procedures/*.md` - All global procedures
- `.team/kernel/**/*.md` - All kernel documentation

**Why This Matters**:
- Workflow files require tabletop simulation testing
- Cross-workflow impact analysis needed
- Systematic refinement process required
- Regression testing must be performed

**If you're in ANY other duty** and encounter tasks involving these files:
1. ✅ **STOP** - Do not update these files yourself
2. ✅ **Create or update a Process Modeling work item** for the changes needed
3. ✅ **Hand over** to process modeling duty
4. ✅ **Document** what workflow changes are needed and why

---

## Quick Start

**Comment Prefix Convention:**
- Follow [Comment Patterns Procedure](../procedures/comment-patterns.md)
- Prefix ALL comments with `[Copilot-Duty: Process Modeling]`
- Example: `[Copilot-Duty: Process Modeling] Created test scenarios and completed tabletop simulation.`

**Standard Process Modeling Flow**:
1. Query process modeling queue and check for multi-phase plan
2. Review work item and determine scope
3. Create or update `/research/workflow-modeling/plan.md`
4. Make workflow/procedure changes
5. Create 3-5 test scenarios for tabletop simulation
6. Execute tabletop tests and refine based on feedback
7. Run kernel and dependency leak detection
8. Update graph (`.team/model-graph.yaml`)
9. Archive valuable test scenarios (selective)
10. Update history and archive plan

---

## Procedure

### Step 1: Query Process Modeling Queue

Use semantic operation to find work items assigned to process modeling:

```python
# Query process modeling queue
pm_items = query_work_items_by_duty(duty="process-modeling")

print(f"Found {len(pm_items)} process modeling work items")
```

**Check for Multi-Phase Plans**:
- Follow [Multi-Phase Work Items Procedure](../procedures/multi-phase-work-items.md)
- If this is a sub-work-item, read parent context
- Update parent progress as work advances

### Step 2: Review Work Item and Determine Scope

```python
# Get work item details
details = get_work_item_details(work_item_id)

title = details['title']
description = details['description']
```

**Analyze Scope**:
- Which workflows/procedures/duties are affected?
- Is this a new feature or refinement of existing?
- How complex is the change?
- Are there dependencies on other work?

**Entry Points**:
- From Triage duty (process improvement identified)
- From backlog-driven mode (processing workflow feedback issues)
- From ad-hoc process improvement requests
- From other duties requesting workflow changes

### Step 3: Create or Update Plan

Update `/research/workflow-modeling/plan.md` to track current work:

**Plan Structure**:
```markdown
# Process Modeling Plan

## Current Work

**Issue**: #NNN - [Issue Title]
**Started**: YYYY-MM-DD
**Status**: In Progress / Testing / Complete

### Workflows/Duties/Procedures Being Updated
- [ ] Research Duty
- [ ] Implementation Duty
- [ ] Global Procedures
- [ ] Copilot Instructions
- [ ] Issue Templates

### Proposed Changes
[Brief summary of what's being changed and why]

### Testing Status
- [ ] Scenarios created
- [ ] Initial tabletop simulation complete
- [ ] Refinements based on feedback
- [ ] Regression tests passed
- [ ] Leak detection complete (kernel & dependency)

### Test Results Summary
[Summary of test outcomes and key findings]

## How to Continue
[Instructions for resuming work if interrupted]
```

### Step 4: Make Workflow/Procedure Changes

Update workflow, duty, procedure, or kernel documentation files following design principles:

**Key Rules**:
1. **Use semantic operations exclusively** - No platform-specific code
2. **Reference procedures** instead of duplicating content
3. **Follow testing framework** for node type (see Required Context)
4. **Update graph** (`.team/model-graph.yaml`) for new dependencies

**See**: 
- [Testing Framework](../../docs/design/prompt-engineering/testing-framework.md) - Change procedures by node type
- [Document Hygiene](../DOCUMENT_HYGIENE.md) - Documentation principles

### Step 5: Create Test Scenarios

Create 3-5 end-to-end test scenarios in `/research/workflow-modeling/scenarios/[duty-name]/`:

**Scenario Types**:
- **baseline**: Testing current workflow state without improvements
- **improved**: Testing workflow with proposed improvements
- **verify**: Verifying existing feature or implementation
- **edge-case**: Testing boundary conditions or unusual situations
- **regression**: Ensuring existing functionality preserved

**Scenario Template**:
```markdown
# Scenario: [Description]

## Context
[What situation is this testing?]

## Starting Point
[Where does the agent start? What's the initial state?]

## Steps to Follow
1. [Step from workflow/duty procedure]
2. [Next step]
3. [Continue following procedure...]

## Expected Outcome
[What should happen? What's success?]

## Actual Outcome
[Fill in during test execution - PASS/FAIL with notes]
```

**Scenario Naming**:
- Format: `scenario-NNN-[type]-[brief-description].md`
- Sequential numbering: 001, 002, 003
- Example: `scenario-001-baseline-research-handover.md`

### Step 6: Execute Tabletop Simulation Tests

**Tabletop Testing Process**:
1. Read the workflow/duty/procedure document
2. For each scenario:
   - Follow the procedure step-by-step as an agent would
   - Note any ambiguities, missing steps, or issues
   - Record PASS/FAIL with detailed feedback
3. Identify patterns of failure or confusion
4. Refine workflow/duty/procedure based on findings
5. Re-test affected scenarios
6. Iterate until >90% scenarios pass

**Test Quality Checks**:
- Test for verbosity and redundancy - trial removing portions and retest
- Ensure procedures are referenced, not duplicated
- Verify semantic operations used exclusively
- Check handover patterns work correctly

**Document Results**:
Update plan.md with test results summary

### Step 7: Run Leak Detection

**Kernel Leak Detection** (platform-specific operations outside kernel):

```bash
# Check for kernel leaks
.team/scripts/check-kernel-leaks.sh
```

**Expected**: Zero leaks

**Dependency Leak Detection** (content duplicated instead of referenced):

```bash
# Check for dependency leaks
.team/scripts/check-dependency-leaks.sh
```

**Expected**: Zero leaks

**See**: [Testing Framework - Leak Detection](../../docs/design/prompt-engineering/testing-framework.md)

### Step 8: Update Graph

Update `.team/model-graph.yaml` with new nodes and edges:

**For New Duty**:
```yaml
nodes:
  - id: duty-[name]
    type: duty
    path: .team/duties/[NAME]_DUTY.md
    description: [Purpose]
  
  - id: duty-[name]-tests
    type: duty
    path: research/workflow-modeling/scenarios/duties/[name]/README.md
    description: [Name] duty test scenarios

edges:
  - from: duty-[name]
    to: design-semantic-language
    type: required
    reason: Duty uses semantic operations exclusively
  
  - from: duty-[name]
    to: kernel-main
    type: required
    reason: Semantic operations implemented by kernel
  
  # Add edges to all referenced procedures
  # Add edges to tests
```

**For New Procedure**:
```yaml
nodes:
  - id: procedure-[name]
    type: procedure
    path: .team/procedures/[name].md
    description: [Purpose]

edges:
  - from: procedure-[name]
    to: procedures-main
    type: required
    reason: Part of global procedures layer
  
  - from: procedure-[name]
    to: kernel-main
    type: required
    reason: Uses semantic operations
```

**See**: [Testing Framework - Graph Maintenance](../../docs/design/prompt-engineering/testing-framework.md)

### Step 9: Archive Valuable Test Scenarios (Selective)

**Default**: Revert scenarios after validation

**Archive Only If**:
- High complexity (tests multiple interacting features)
- Regression risk (scenario caught real bug)
- Edge case documentation (unusual but important case)
- Training value (good example for learning)

**How to Archive**:
```bash
# Move valuable scenarios to regression-tests
mv research/workflow-modeling/scenarios/[duty-name]/scenario-NNN-*.md \
   research/workflow-modeling/regression-tests/[duty-name]/
```

**Why Selective**:
- Keeps repository clean
- Avoids maintenance burden
- Most workflow changes are validated once and don't need re-testing

### Step 10: Update History and Archive Plan

Update `/research/workflow-modeling/history.md`:

```markdown
## YYYY-MM-DD - [Brief Title]

**Issue**: #NNN
**Workflows Affected**: [List]
**Changes**: [Summary]
**Test Results**: X/Y scenarios passed
**Outcome**: [Improvement description]
```

Archive completed plan:

```bash
# Move plan to archive
mv /research/workflow-modeling/plan.md \
   /research/workflow-modeling/archive/YYYY-MM-DD-issue-NNN.md
```

### Step 11: Complete Work Item

Follow [Self-Improvement Procedure](../procedures/self-improvement.md) to submit feedback, then add completion comment:

```python
completion_comment = """[Copilot-Duty: Process Modeling] ✅ **Process Modeling Complete**

## Changes Made

**Workflows/Duties Updated**:
- [List updated files]

**Test Results**:
- Scenarios created: X
- Pass rate: Y% (Z/X scenarios passed)
- Leak detection: ✅ Zero leaks (kernel & dependency)

**Graph Updated**:
- Nodes added: X
- Edges added: Y

## Documentation

**History**: Updated `/research/workflow-modeling/history.md`
**Plan**: Archived to `/research/workflow-modeling/archive/YYYY-MM-DD-issue-NNN.md`
**Scenarios**: {"Reverted" or "Archived to regression-tests"}

See updated workflow/duty documentation for details.
"""

add_work_item_comment(
    work_item_id=work_item_id,
    text=completion_comment
)
```

---

## Handover Points

### Handover to Implementation

**When**: Process improvements require code changes (e.g., automation scripts)

**Process**: Use [Handover Procedure](../procedures/handover.md)

### Handover to Triage

**When**: Work item needs assessment or scope is unclear

**Process**: Use [Handover Procedure](../procedures/handover.md)

---

## Common Patterns

### Pattern 1: Duty Migration (Phase 3)

**Scenario**: Migrate workflow file to new duty structure

**Process**:
1. Create duty file in `.team/duties/[NAME]_DUTY.md`
2. Add Required Context section (procedures, kernel, semantic operations)
3. Migrate content from workflow file
4. Convert to semantic operations (eliminate platform-specific code)
5. Reference procedures instead of duplicating content
6. Create 3-5 test scenarios
7. Run leak detection (kernel & dependency)
8. Update graph with nodes and edges

**Expected Results**:
- 50-60% file size reduction (through procedure references)
- Zero kernel leaks
- Zero dependency leaks
- >90% test pass rate

### Pattern 2: New Procedure Creation (Phase 2)

**Scenario**: Extract common logic into global procedure

**Process**:
1. Identify duplicated content across duties
2. Create procedure file in `.team/procedures/[name].md`
3. Use semantic operations exclusively
4. Add to procedure README
5. Update duties to reference new procedure
6. Create test scenarios
7. Run leak detection
8. Update graph

### Pattern 3: Workflow Feedback Processing

**Scenario**: Address feedback from self-improvement submissions

**Process**:
1. Query workflow feedback tracker work item
2. Get all comments containing "Workflow Feedback Entry"
3. Filter to unaddressed feedback
4. Update plan.md with feedback being addressed
5. Process each feedback item through standard flow
6. Mark as addressed with reply comment

### Pattern 4: Cross-Cutting Documentation

**Scenario**: Documentation needed by multiple duties but not all

**Process**:
1. Assess scope (see [Documentation Scope Guide](../DOCUMENTATION_SCOPE_GUIDE.md))
2. If multiple duties but not all: Create `.team/[TOPIC].md` file
3. Update relevant duties with reference (not duplication)
4. Test with scenarios from each duty
5. Update graph with dependencies

---

## Change Procedures by Node Type

**See**: [Testing Framework - Node-Specific Change Procedures](../../docs/design/prompt-engineering/testing-framework.md)

**Summary**:

- **Kernel Changes**: Update operation mappings, run semantic contract tests
- **Procedure Changes**: Update procedure file, test with scenarios, run leak detection
- **Duty Changes**: Update duty file, create scenarios, run leak detection, update graph
- **Orchestration Changes**: Full integration testing across all duties

---

## Success Criteria

Process modeling is successful when:

- ✅ All affected files updated with changes
- ✅ Semantic operations used exclusively (zero kernel leaks)
- ✅ Procedures referenced correctly (zero dependency leaks)
- ✅ Test scenarios created and >90% pass rate achieved
- ✅ Leak detection passes (kernel & dependency)
- ✅ Graph updated with all dependencies
- ✅ History updated with changes
- ✅ Plan archived
- ✅ Self-improvement feedback submitted

---

## Related Documentation

- **[Testing Framework](../../docs/design/prompt-engineering/testing-framework.md)** - Complete testing methodology
- **[Core Concepts](../../docs/design/prompt-engineering/concepts.md)** - Layered architecture understanding
- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Operations specification
- **[Document Hygiene](../DOCUMENT_HYGIENE.md)** - Documentation principles
- **[Global Procedures](../procedures/README.md)** - All procedures to reference

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial Process Modeling Duty created from workflow migration (Phase 3.2) |
