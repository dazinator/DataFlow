# Procedure Test Scenarios

**Purpose**: Tabletop test scenarios for validating global procedures

**Created**: 2025-11-12 (Phase 2)

---

## Overview

This directory contains tabletop test scenarios for each global procedure. These scenarios validate that procedures are clear, complete, and use semantic operations correctly.

## Directory Structure

```
procedures/
├── duty-assignment/          # Duty assignment procedure tests
├── multi-phase-work-items/   # Multi-phase management tests
├── self-improvement/         # Self-improvement feedback tests
├── work-item-creation/       # Work item creation pattern tests
├── handover/                 # Handover procedure tests
└── comment-patterns/         # Comment pattern tests
```

---

## Scenario Testing Methodology

### Purpose of Tabletop Tests

**Why test procedures with AI?**

Even though AI can hallucinate, tabletop tests are valuable because they:
- Force systematic thinking through scenarios
- Catch missing steps or ambiguous instructions
- Verify semantic operations are used correctly
- Detect circular references or broken links
- Catch >70% of issues before human review

**See**: [Testing Framework](../../../docs/design/prompt-engineering/testing-framework.md#node-type-2-global-procedures)

### Test Scenario Structure

Each test scenario includes:

1. **Context** - What situation is being tested
2. **Starting State** - Initial work item state
3. **Procedure Steps** - Following procedure step-by-step
4. **Expected Outcome** - What should happen
5. **Success Criteria** - Checklist for validation
6. **Test Execution** - What actually happened
7. **Test Result** - PASS/FAIL with observations

### Required Test Coverage

Each procedure should have **3-5 test scenarios** covering:

1. **Happy Path** - Standard successful execution
2. **Edge Cases** - Boundary conditions or unusual but valid scenarios
3. **Error Handling** - Invalid inputs or error conditions
4. **Complex Scenarios** - Multi-step or decision-heavy flows

### Example Test Scenario

See [duty-assignment/scenario-1-clear-assignment.md](duty-assignment/scenario-1-clear-assignment.md) for a complete example.

---

## Persistence Strategy

**Selective Persistence**: Not all test scenarios are kept permanently.

### Keep If:

- ✅ Tests complex decision logic (multiple conditions)
- ✅ High regression risk (frequently used procedure)
- ✅ Time-consuming to recreate (>30 min)
- ✅ Reference value for future development

### Revert If:

- ❌ One-time validation (improvement confirmed)
- ❌ Simple verification
- ❌ Low regression risk

### Archive Location

High-value scenarios archived to: `/research/workflow-modeling/regression-tests/procedures/`

---

## Testing Process

### Creating Tests (Phase 2)

For each new procedure:

1. **Identify Scenarios** - What cases need testing?
2. **Create Scenario Files** - Use template structure
3. **Execute Tabletop Test** - Follow procedure as if you're an agent
4. **Record Results** - Document what happened
5. **Refine Procedure** - Fix any issues found
6. **Retest** - Verify fixes work
7. **Archive or Revert** - Keep valuable tests, revert temporary ones

### Running Regression Tests (Ongoing)

When changing a procedure:

1. **Find existing tests** in this directory
2. **Run all scenarios** for changed procedure
3. **Create new scenarios** for new edge cases
4. **Update procedure** if tests reveal issues
5. **Update tests** if procedure changes invalidate them

---

## Scenario Naming Convention

**Format**: `scenario-{number}-{short-description}.md`

**Examples**:
- `scenario-1-clear-assignment.md`
- `scenario-2-multiple-duties.md`
- `scenario-3-unassigned-work-item.md`
- `scenario-4-wrong-duty.md`
- `scenario-5-ambiguous-indicators.md`

**Numbering**: Sequential within each procedure directory

---

## Test Scenario Template

```markdown
# Test Scenario: {Procedure Name} - {Scenario Name}

**Procedure**: [Procedure Name](/.team/procedures/{procedure-file}.md)

**Scenario Type**: Happy Path | Edge Case | Error Handling | Complex

**Test Date**: YYYY-MM-DD

---

## Context

[What situation is being tested]

## Starting State

- Work item state
- Relevant properties
- Agent context

## Procedure Steps to Follow

Following [{Procedure Name}](/.team/procedures/{procedure}.md):

### Step 1: {Step Name}

[Expected actions and results]

### Step 2: {Step Name}

[Expected actions and results]

[... continue for all steps ...]

## Expected Outcome

- Expected result 1
- Expected result 2
- Expected result 3

## Success Criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3

## Test Execution

**Agent Actions**:
1. Action 1
2. Action 2
3. Action 3

**Semantic Operations Used**:
- `operation_1()` ✅
- `operation_2()` ✅

**Platform-Specific Code**: None ✅

## Test Result

**Status**: ✅ PASS | ❌ FAIL

**Agent Observations**: 
[What happened when following procedure]

**Issues Found**: 
[Unclear steps, missing info, etc. or "None"]

---

## Notes

[Any additional observations or context]
```

---

## Leak Detection

After creating/updating procedures, run leak detection:

### Kernel Leak Detection

**Check**: Procedures use ONLY semantic operations, no platform-specific code

**How to check**: Search for platform-specific patterns defined in `.team/kernel/domains.yaml`

**Pattern Source**: `.team/kernel/domains.yaml`

Each platform driver in the kernel domain registry defines its leak detection patterns. Always consult this file for the authoritative list.

**Example patterns from domains.yaml**:
```yaml
# GitHub driver leak patterns
leak_patterns:
  - issue_write
  - issue_read
  - add_issue_comment
  - sub_issue_write
  - list_issues
  - search_issues
  - workflow:research  # GitHub-specific label format

# Azure DevOps driver leak patterns (planned)
leak_patterns:
  - /wit/workitems
  - azure-devops
  - duty:research  # Azure DevOps-specific tag format
```

**Test approach**:
1. Load patterns from `.team/kernel/domains.yaml`
2. Search for each pattern in `.team/procedures/*.md` files
3. Verify any matches are only in anti-pattern sections (marked with ❌)

**Expected**: Zero occurrences in procedure logic (only in anti-pattern examples)

**Important**: 
- Do not hardcode leak patterns in test scripts. Always reference `domains.yaml` as the single source of truth.
- **Report results as a comment on the work item**, not as a file in the repository

**Reporting Format**:
```markdown
## Leak Detection Results

**Date**: YYYY-MM-DD
**Patterns Checked**: From `.team/kernel/domains.yaml`

### Kernel Leaks
- Platform X: ✅ Zero leaks (or ⚠️ N occurrences - context)
- Platform Y: ✅ Zero leaks

### Dependency Leaks
- ✅ All procedures reference only kernel layer

**Conclusion**: ✅ PASS / ❌ FAIL
```

### Dependency Leak Detection

**Check**: Procedures only reference kernel layer (not other procedures or duties)

**How to check**: Review "Required Context" sections

**Valid Dependencies**:
- ✅ Semantic operations from kernel
- ✅ Other procedures (cross-procedure references)
- ❌ Specific duties (procedures should be duty-agnostic)
- ❌ Orchestration layer (wrong direction)

---

## Related Documentation

- [Testing Framework](../../../docs/design/prompt-engineering/testing-framework.md) - Complete testing methodology
- [Global Procedures](/.team/procedures/README.md) - Procedures being tested
- [Semantic Language](../../../docs/design/prompt-engineering/semantic-language.md) - Operations used in procedures

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial test scenario structure for Phase 2 |
