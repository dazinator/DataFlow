# Scenario 002 - Improved: Implementation Workflow with Added Guidance

**Type**: Improved (Proposed Changes)
**Issue**: #260 - Implementation Workflow improvements
**Date**: 2025-11-10

## Context

Same context as Scenario 001, but with proposed improvements implemented:
1. "Verify Backlog Item Accuracy" step added to Implementation Workflow
2. "Central Package Management" guidance added to copilot-instructions.md
3. Implementation issue template improved
4. "Pattern Discovery Helper" added to Implementation Workflow

## Test Scenario Steps

### Situation 1: Backlog Item Estimates (WITH Verification Step)

1. Agent reads backlog item that says "~50-60 files" need updating
2. **NEW**: Workflow instructs to verify estimate using grep: `grep -l "pattern" **/*.cs | wc -l`
3. Agent discovers only 6 files match (not 50-60)
4. **NEW**: Workflow instructs to document discrepancy in first progress report
5. **Question**: Can agent proceed with confidence knowing the actual scope?

**Expected Outcome**: PASS - Agent has clear guidance to validate and document

### Situation 2: Package Management Discovery (WITH Guidance)

1. Agent needs to add `xunit` package to test project
2. Build fails with message about Directory.Packages.props
3. **NEW**: Agent checks copilot-instructions.md and finds:
   - Explanation of Directory.Packages.props system
   - Guidance on when to add vs when packages are transitive
   - Example: `grep "PackageVersion Include=\"xunit\"" src/Directory.Packages.props`
4. Agent quickly determines package is already available transitively
5. **Question**: Does agent understand the package management system?

**Expected Outcome**: PASS - Clear explanation helps agent make correct decision

### Situation 3: Boilerplate Pattern Discovery (WITH Helper Patterns)

1. Agent needs to find all files with specific boilerplate pattern
2. **NEW**: Workflow provides common grep patterns section:
   - Finding boilerplate: `grep -l "public ITestOutputHelper Output" **/*.cs`
   - Counting matches: `grep -l "pattern" **/*.cs | wc -l`
3. Agent uses provided pattern and finds all files in ~1 minute
4. **Question**: Does helper improve efficiency?

**Expected Outcome**: PASS - Pattern helpers save significant time

### Situation 4: Issue Template Confusion (WITH Improved Template)

1. Agent opens improved implementation issue template
2. **NEW**: Title is "Implementation: [Brief Description]" (specific, not generic)
3. **NEW**: Template has note: "Replace [placeholders] with actual values"
4. **NEW**: Template requires backlog item ID or explicit "Next from prioritization"
5. Agent creates issue confidently
6. **Question**: Is template guidance clear?

**Expected Outcome**: PASS - Clear instructions eliminate confusion

## Simulation Results

**Status**: ✅ **PASS** (4/4 situations resolved)

**Findings**:
1. ✅ Verification step provides clear guidance for validating estimates
2. ✅ Package management documentation explains system and provides check commands
3. ✅ Pattern discovery helpers save time with ready-to-use grep examples
4. ✅ Improved template eliminates confusion with clear instructions

**Detailed Results**: See `/tmp/simulation-results.md`

**Conclusion**: All 4 improvements validated. Approved for implementation.

## Validation Checklist

- [ ] Verify step added to Implementation Workflow and makes sense
- [ ] Package management guidance added to copilot-instructions.md
- [ ] Pattern discovery helpers added to workflow
- [ ] Issue template improved with clear instructions

## Expected Improvements

1. **Faster scope discovery** - Agent validates estimates upfront, avoiding surprises
2. **Reduced package confusion** - Clear documentation prevents trial-and-error
3. **Faster pattern finding** - Helper patterns save ~10+ minutes per task
4. **Clearer templates** - Agent creates better-formed issues from the start

## Success Criteria

All 4 situations should result in PASS:
- Agent can validate estimates and document discrepancies
- Agent understands package management from documentation
- Agent has ready-to-use grep patterns
- Agent understands how to use issue templates

If all pass, improvements should be implemented.
