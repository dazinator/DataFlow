# Self-Improvement Feedback - Bulk Triage Workflow Permission Fix

**Duty**: Process Modeling  
**Work Item**: Issue about bulk triage workflow failing  
**Date**: 2025-11-13

---

## What Worked Well

✅ **Clear error diagnostics**: The 403 error message clearly indicated the missing permission (`pull_requests=read`), making root cause analysis straightforward.

✅ **Testing Framework guidance**: The tabletop simulation methodology from the Testing Framework document provided excellent structure for creating and executing test scenarios.

✅ **Minimal change principle**: Process Modeling duty's emphasis on surgical, minimal changes prevented scope creep. The fix was literally one line: adding `pull-requests: read`.

✅ **Test scenario templates**: The scenario structure from the Testing Framework (Context, Starting Point, Steps, Expected Outcome) made test creation systematic and thorough.

✅ **Leak detection automation**: The automated leak detection scripts (`check-kernel-leaks.sh`, `check-dependency-leaks.sh`) provided quick validation that changes didn't introduce new issues.

✅ **Duty ownership rules**: The clear ownership rules prevented me from accidentally modifying duty files when I should be working on workflow infrastructure.

---

## What Didn't Work Well

❌ **Missing DOCUMENT_HYGIENE.md**: The Process Modeling duty references `.team/DOCUMENT_HYGIENE.md` but the file doesn't exist. Had to proceed without it, though the Testing Framework provided sufficient guidance.

❌ **Unclear test location convention**: Initially uncertain whether workflow tests should go in `.team/workflows/tests/` or `.github/workflows/tests/`. No clear guidance in the documentation about testing GitHub Actions workflows vs testing duty workflows.

❌ **Graph update ambiguity**: Unclear whether GitHub Actions workflow changes require model graph updates. The graph focuses on duty/procedure dependencies, but workflow infrastructure isn't clearly categorized.

❌ **No guidance on GitHub Actions testing**: The Testing Framework focuses heavily on duty/procedure/kernel testing but doesn't address how to test GitHub Actions workflows themselves (beyond tabletop simulation).

---

## Specific Improvement Proposals

### 1. Create DOCUMENT_HYGIENE.md or Update References

**Problem**: Process Modeling duty step 2 says "Read `docs/DOCUMENT_HYGIENE.md`" but file doesn't exist.

**Proposal**: Either:
- Create `.team/DOCUMENT_HYGIENE.md` with documentation principles, OR
- Update Process Modeling duty to reference the Testing Framework instead, OR
- Remove the reference if it's obsolete

**Impact**: Prevents confusion and ensures agents have correct documentation principles.

---

### 2. Add GitHub Actions Testing Guidance

**Problem**: No clear guidance on how to test GitHub Actions workflows (vs. duty workflows).

**Proposal**: Add section to Testing Framework or Process Modeling duty:

```markdown
### Testing GitHub Actions Workflows

**Test Location**: `.github/workflows/tests/<workflow-name>/`

**Test Approach**:
- Use tabletop simulation for permission and API call validation
- Create scenarios for success cases, edge cases, and regressions
- Verify workflow YAML syntax with `actionlint` if available
- Test permissions are minimal but sufficient

**Not Covered by Tabletop**:
- Actual GitHub API responses (mock with expected behavior)
- Workflow trigger conditions (schedule, workflow_dispatch, etc.)
- Secret availability (assume secrets exist in test scenarios)
```

**Impact**: Clarifies where and how to test workflow infrastructure changes.

---

### 3. Clarify Graph Update Requirements

**Problem**: Unclear if GitHub Actions workflows need graph entries.

**Proposal**: Add to Process Modeling duty step 8:

```markdown
### Step 8: Update Graph (If Applicable)

**When Graph Updates Are Required**:
- ✅ New duty created
- ✅ New procedure created
- ✅ New kernel operation added
- ✅ New dependency between duties/procedures/kernel
- ❌ GitHub Actions workflow changes (infrastructure, not prompt system)
- ❌ Script changes in `.github/scripts/`
- ❌ Issue template updates (unless they change duty behavior)

Update `.team/model-graph.yaml` only for changes to the prompt system layers.
```

**Impact**: Reduces confusion about when graph updates are needed.

---

### 4. Add Test Archival Guidance

**Problem**: Created 5 test scenarios + results file. Unclear which should be archived vs. kept.

**Proposal**: Add to Process Modeling duty step 9:

```markdown
### Step 9: Archive Test Scenarios (Selective)

**Keep Permanently**:
- Baseline scenarios that document the problem
- Edge case scenarios that prevent regressions
- Any scenario that failed and required workflow changes

**Archive to History**:
- Intermediate test iterations
- Redundant success case scenarios
- Scenarios for transient issues

**For Infrastructure Tests** (GitHub Actions, scripts):
- Keep all scenarios as living documentation
- These tests are lightweight and valuable for future changes
```

**Impact**: Provides clear criteria for test archival decisions.

---

## Summary

The Process Modeling duty worked very well for this straightforward permission fix. The Testing Framework provided excellent structure, and the minimal change principle kept the fix surgical. 

Main improvement needed: Better guidance for testing GitHub Actions workflows vs. duty workflows, and clarification on when graph updates are required for infrastructure changes.

---

## Feedback Submission

This feedback is being included in the PR as a markdown file for human review. The semantic operation `submit_feedback()` would typically post this to a Workflow Feedback Tracker issue.

```python
# Conceptual submission (actual submission via PR comment)
submit_feedback(
    duty="process-modeling",
    work_item_id="current_issue_number",
    worked_well=[
        "Clear error diagnostics from 403 message",
        "Testing Framework tabletop simulation structure",
        "Minimal change principle prevented scope creep",
        "Leak detection automation validated no new issues"
    ],
    didnt_work_well=[
        "Missing DOCUMENT_HYGIENE.md file referenced in duty",
        "Unclear test location convention for GitHub Actions vs duties",
        "Graph update requirements ambiguous for infrastructure changes",
        "No GitHub Actions testing guidance in Testing Framework"
    ],
    improvements=[
        "Create DOCUMENT_HYGIENE.md or update Process Modeling duty references",
        "Add GitHub Actions testing section to Testing Framework",
        "Clarify graph update requirements with infrastructure examples",
        "Add test archival guidance for infrastructure tests"
    ]
)
```
