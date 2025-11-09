# Workflow Topology System - Test Scenarios

This directory contains tabletop test scenarios for validating the workflow topology system integration.

## Test Scenario Overview

| Scenario | Type | Description | Status |
|----------|------|-------------|--------|
| 001 | Baseline | Triage to Research transition | PENDING |
| 002 | Baseline | Research to Implementation handover | PENDING |
| 003 | Baseline | Implementation discovers Tech Debt | PENDING |
| 004 | Edge Case | Re-triage (implementation back to triage) | PENDING |
| 005 | Baseline | Tech Debt to Product Prioritization | PENDING |
| 006 | Tool Usage | Workflow Dashboard monitoring | PENDING |
| 007 | Verification | Script path verification across all docs | PENDING |

## Testing Approach

These scenarios use **tabletop simulation** - walking through the documented workflows as if executing them, without actually creating GitHub issues.

### Why Tabletop Testing?

- Validates documentation clarity
- Identifies gaps in workflow instructions
- Tests script path accuracy
- Verifies handover patterns are complete
- No actual GitHub issues needed
- Can be done entirely offline

## Scenario Types

### Baseline Scenarios (001, 002, 003, 005)

Test common workflow transitions:
- Triage → Research
- Research → Implementation
- Implementation → Tech Debt
- Tech Debt → Product Prioritization

**Goal**: Ensure most common paths work smoothly

### Edge Case Scenarios (004)

Test unusual but valid transitions:
- Re-triage (going "backwards")

**Goal**: Ensure workflow supports loops and iterations

### Tool Usage Scenarios (006)

Test helper scripts:
- Workflow dashboard
- Queue queries
- State monitoring

**Goal**: Verify scripts are useful and documented

### Verification Scenarios (007)

Test documentation consistency:
- Script paths
- Cross-references
- Command accuracy

**Goal**: Ensure all paths and references are correct

## Running the Scenarios

Each scenario is self-contained in its own markdown file. To run a scenario:

1. Open the scenario file
2. Read the context and starting point
3. Follow the "Steps to Follow" section
4. Check if you can complete each step with the documentation provided
5. Mark success criteria as you go
6. Document the result (PASS/FAIL) and notes

## Success Criteria

A scenario PASSES when:
- All steps can be completed using documented instructions
- No gaps or missing information
- No confusion or unclear guidance
- Expected outcome matches actual outcome
- Success criteria checklist is complete

A scenario FAILS when:
- Steps are unclear or ambiguous
- Information is missing
- Documentation contradicts itself
- Expected outcome doesn't match actual
- Requires knowledge not in documentation

## Test Results Location

Results are documented in each scenario file under "Test Result" section.

Failed scenarios trigger workflow refinement:
1. Document what went wrong
2. Update workflow documentation
3. Re-run scenario
4. Repeat until PASS

## Regression Testing

After initial scenarios PASS:
- Archive these as regression tests (if valuable)
- Re-run when making future workflow changes
- Ensures changes don't break existing patterns

## Notes

- These scenarios test the **documentation**, not the actual GitHub integration
- Actual GitHub labels will be created by reviewer after documentation is validated
- Scripts can be tested with `--help` flag without real labels
- Focus is on workflow clarity and completeness
