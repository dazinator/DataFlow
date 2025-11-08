# Workflow Modeling Regression Tests

This directory contains archived test scenarios that have consistently passed. These scenarios serve as regression tests when making further changes to workflows.

## Purpose

Regression tests ensure that workflow improvements don't break previously working functionality. Before finalizing any workflow changes, run all relevant regression tests to verify no regressions have been introduced.

## Structure

```
regression-tests/
├── research-workflow/
├── implementation-workflow/
├── tech-debt-workflow/
└── process-modeling-workflow/
```

## Running Regression Tests

1. Identify which workflows were changed
2. Run all scenarios in the corresponding subfolder(s)
3. Verify all scenarios still PASS
4. If any FAIL, investigate and fix before proceeding

## Adding New Regression Tests

Scenarios are promoted from `/research/workflow-modeling/scenarios/` to this directory when:
- They have been used successfully multiple times
- They test important workflow functionality
- They would help catch regressions in future changes

## Maintenance

Periodically review regression tests to:
- Remove obsolete scenarios (workflow significantly changed)
- Update scenarios if minor workflow changes require it
- Ensure coverage of critical workflow paths
