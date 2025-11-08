# Workflow Modeling Test Scenarios

This directory contains test scenarios for tabletop simulation testing of workflow improvements.

## Structure

```
scenarios/
├── research-workflow/          # Scenarios for Research Workflow
├── implementation-workflow/    # Scenarios for Implementation Workflow
├── tech-debt-workflow/         # Scenarios for Tech Debt Workflow
└── process-modeling-workflow/  # Scenarios for Process Modeling Workflow
```

## Creating New Scenarios

Use the scenario template from `PROCESS_MODELING_WORKFLOW.md`:

```markdown
# Scenario: [Description]

## Context
[What situation is this testing?]

## Starting Point
[Where does the copilot agent start?]

## Steps to Follow
1. [Step from copilot-instructions.md]
2. [Next step from workflow documentation]
...

## Expected Outcome
[What should happen if workflow works correctly?]

## Success Criteria
- [ ] Instructions were clear
- [ ] No gaps or missing information
- [ ] Workflow led to expected outcome

## Test Result
**Status**: PASS / FAIL
**Notes**: [Observations]
```

## Running Scenarios

1. Start from `.github/copilot-instructions.md`
2. Follow workflow steps exactly as documented
3. Create test assets as needed (to be reverted after)
4. Document all issues encountered
5. Record PASS/FAIL result with detailed notes
6. Revert all test assets

## Moving to Regression Tests

When scenarios consistently PASS, move them to `/research/workflow-modeling/regression-tests/` for use as regression tests in future workflow changes.
