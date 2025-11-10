# Scenario 001 - Baseline: Implementation Workflow Guidance Gaps

**Type**: Baseline (Current State)
**Issue**: #260 - Implementation Workflow improvements
**Date**: 2025-11-10

## Context

An implementation agent is working on issue #197 to reduce test boilerplate. They encounter several workflow gaps that cause confusion and inefficiency.

## Starting Point

- Implementation Workflow exists at `.team/prompts/IMPLEMENTATION_WORKFLOW.md`
- copilot-instructions.md exists
- Implementation issue template exists
- Agent has not encountered these specific situations before

## Test Scenario Steps

### Situation 1: Backlog Item Estimates

1. Agent reads backlog item that says "~50-60 files" need updating
2. Agent proceeds with work based on this estimate
3. During implementation, discovers only 6 files actually need changes
4. **Question**: Does workflow guide what to do when estimates are wrong?

**Expected Outcome**: FAIL - No guidance exists for validating or updating estimates

### Situation 2: Package Management Discovery

1. Agent needs to add `xunit` package to test project
2. Build fails with message about Directory.Packages.props
3. Agent doesn't understand central package management system
4. **Question**: Does copilot-instructions explain this system?

**Expected Outcome**: FAIL - No explanation of central package management

### Situation 3: Boilerplate Pattern Discovery  

1. Agent needs to find all files with specific boilerplate pattern
2. Runs multiple greps manually: `grep -l "pattern1"`, `grep -l "pattern2"`, etc.
3. Takes 10+ minutes to find all matching files
4. **Question**: Does workflow provide helper grep patterns for common tasks?

**Expected Outcome**: FAIL - No pattern discovery helpers provided

### Situation 4: Issue Template Confusion

1. Agent sees implementation issue template with "[e.g., ...]" placeholders
2. Uncertain if they should replace brackets or keep them
3. Title is generic "Implement something"
4. **Question**: Is template guidance clear about placeholder usage?

**Expected Outcome**: FAIL - Template has confusing placeholders and generic title

## Simulation Results

**Status**: ❌ **FAIL** (0/4 situations have guidance)

**Findings**: 
- No guidance for validating backlog estimates in Implementation Workflow
- No explanation of Directory.Packages.props system in copilot-instructions
- No helper grep patterns provided in workflows
- Issue template has confusing placeholders and generic title

**Detailed Results**: See `/tmp/simulation-results.md`

**Conclusion**: All 4 gaps confirmed. Proceed to Scenario 002 for improved version.

## Validation Checklist

- [ ] Checked Implementation Workflow for estimate validation guidance
- [ ] Checked copilot-instructions.md for package management explanation
- [ ] Checked workflows for pattern discovery helpers
- [ ] Checked issue template for clear placeholder guidance

## Notes

This baseline scenario demonstrates 4 specific gaps identified in feedback #260. Each gap causes agent confusion or wasted time during implementation work.
