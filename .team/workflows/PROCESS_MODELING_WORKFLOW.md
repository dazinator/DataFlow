# Process Modeling Workflow

---

## ⚠️ CRITICAL: This is a Specialized Research Workflow

**If you're a Copilot agent working on a process modeling issue:**

This workflow is a specialized variant of the Research Workflow for systematically improving and refining team workflows and processes. Like other research workflows, process modeling produces **documentation and refined workflows**, not direct code merges (except for workflow documentation updates).

### DO (During Process Modeling):
- ✅ **Read [Document Hygiene Guide](../DOCUMENT_HYGIENE.md)** before creating/updating documentation
- ✅ Use the long-lived `/research/workflow-modeling/` folder for all process modeling work
- ✅ Create or update `/research/workflow-modeling/plan.md` to track current work
- ✅ Create test scenarios in `/research/workflow-modeling/scenarios/` for tabletop simulation tests
- ✅ Perform tabletop simulation tests for all workflow changes
- ✅ Document test results (PASS/FAIL) with detailed feedback
- ✅ Build regression test suite from successful scenarios
- ✅ Refine workflows based on testing feedback
- ✅ Test for verbosity and redundancy - trial removing portions and retest
- ✅ Revert test assets after each simulation concludes

### DO (After Testing and Refinement):
- ✅ Update workflow documentation files in `.team/workflows/[Name]_WORKFLOW.md`
- ✅ Update `.github/copilot-instructions.md` if systemic changes are made
- ✅ Update issue templates in `.github/ISSUE_TEMPLATE/` if needed
- ✅ Archive successful test scenarios as regression tests
- ✅ Update plan.md with completion status
- ✅ **Complete self-improvement evaluation** in `.github/workflow-improvements.md`

### DON'T:
- ❌ Skip tabletop simulation testing
- ❌ Make workflow changes without testing them first
- ❌ Keep test assets after simulation concludes
- ❌ Skip regression testing when making further changes

### OUTCOME:
Process modeling produces **refined workflow documentation + tested scenarios + regression test suite**. Test assets are created, used for validation, then reverted. Only the improved workflow documentation is kept.

---

## Overview

Process modeling is the systematic process of improving team workflows and processes through iterative testing and refinement. This workflow enables Copilot agents to understand workflow pain points, propose improvements, test them through tabletop simulation, and refine based on feedback.

## When to Use This Workflow

Use this workflow when:
- Proposing improvements to existing team workflows (Research, Implementation, Tech Debt)
- Updating Copilot instructions for better clarity or effectiveness
- Refining issue templates based on usage patterns
- Making systemic changes that affect multiple workflows
- Addressing confusion or inefficiencies in current processes

**This is NOT for**:
- Focused research on specific technical approaches (use Research Workflow)
- Technical debt discovery (use Tech Debt Workflow)
- Direct implementation work (use Implementation Workflow)

## How to Initiate Process Modeling

Process modeling can be initiated in **two modes**:

### Mode 1: Issue-Driven (Direct Proposal)

**Create a GitHub Issue** using the "Workflow Improvements" issue template:

1. Go to GitHub Issues → New Issue
2. Select **"Workflow Improvements"** template
3. Select **"Issue-Driven"** mode
4. Fill in:
   - Which workflow(s) are affected
   - Current state and pain points
   - Proposed improvements
   - Expected benefits
5. Assign to @copilot or mention @copilot in comments

The issue will invoke this Process Modeling Workflow with your specific proposal.

### Mode 2: Backlog-Driven (Process Next Suggestion)

**Create a GitHub Issue** using the "Workflow Improvements" issue template:

1. Go to GitHub Issues → New Issue
2. Select **"Workflow Improvements"** template
3. Select **"Backlog-Driven"** mode
4. Assign to @copilot or mention @copilot in comments

**For @copilot executing backlog-driven mode:**

1. **Read** `.github/workflow-improvements.md`
2. **Select** the top unaddressed entry:
   - Scan sections in order: Research → Implementation → General → POC → Documentation
   - Find first entry with at least one unaddressed improvement (no ✅ marker)
   - If all improvements in an entry are marked ✅, skip to next entry
3. **Extract context** from the selected entry:
   - Date and Issue/PR reference
   - What worked well
   - What didn't work well
   - Suggested improvement(s) - identify which are unaddressed
   - Which workflow(s) affected
4. **Update** `/research/workflow-modeling/plan.md` with:
   - Selected entry details
   - Which specific improvements you're addressing
   - Expected workflow changes
5. **Follow standard process modeling** (create scenarios, test, refine, etc.)
6. **After completion**:
   - Remove the entire entry from `.github/workflow-improvements.md`
   - Add one-line summary to `/research/workflow-modeling/history.md`
   - Archive plan to `/research/workflow-modeling/archive/`

**Entry Removal**: Remove the entire entry even if improvements were not viable. This prevents the queue from getting stuck. Document unsuccessful attempts in history.md.

## Long-Lived Research Folder Structure

Unlike other research workflows that create date-based folders, process modeling uses a **long-lived folder**:

```
/research/workflow-modeling/
├── plan.md                          # Current work tracking
├── scenarios/                       # Test scenarios
│   ├── research-workflow/          # Scenarios for Research Workflow
│   │   ├── scenario-001-basic-research.md
│   │   └── scenario-002-pivot-handling.md
│   ├── implementation-workflow/    # Scenarios for Implementation Workflow
│   └── tech-debt-workflow/         # Scenarios for Tech Debt Workflow
├── regression-tests/               # Archived successful scenarios
│   ├── research-workflow/
│   └── implementation-workflow/
└── archive/                        # Completed work
    └── YYYY-MM-DD-issue-NNN.md    # Archived plans
```

### plan.md Format

The plan.md file tracks current process modeling work:

```markdown
# Process Modeling Plan

## Current Work

**Issue**: #NNN - [Issue Title]
**Started**: YYYY-MM-DD
**Status**: In Progress / Testing / Complete

### Workflows Being Updated
- [ ] Research Workflow
- [ ] Implementation Workflow
- [ ] Tech Debt Workflow
- [ ] Copilot Instructions
- [ ] Issue Templates

### Proposed Changes
[Brief summary of what's being changed and why]

### Testing Status
- [ ] Scenarios created
- [ ] Initial tabletop simulation complete
- [ ] Refinements based on feedback
- [ ] Regression tests passed
- [ ] Verbosity/redundancy check complete

### Test Results Summary
[Summary of test outcomes and key findings]

## How to Continue
[Instructions for resuming work if interrupted]
```

## Tabletop Simulation Testing Process

### 1. Create Test Scenarios

For each workflow change, create realistic scenarios in `/research/workflow-modeling/scenarios/[workflow-name]/`:

**Scenario Template** (`scenario-NNN-description.md`):
```markdown
# Scenario: [Description]

## Context
[What situation is this testing?]

## Starting Point
[Where does the copilot agent start? What's the initial state?]

## Steps to Follow
1. [Step from copilot-instructions.md]
2. [Next step from workflow documentation]
3. [Continue following workflow...]

## Expected Outcome
[What should happen if workflow works correctly?]

## Success Criteria
- [ ] Instructions were clear and unambiguous
- [ ] No gaps or missing information
- [ ] Workflow led to expected outcome
- [ ] No confusion or back-tracking needed

## Test Result
**Status**: PASS / FAIL
**Notes**: [Detailed observations]
```

### 2. Execute Tabletop Simulation

1. **Start from `.github/copilot-instructions.md`**: Begin where a copilot agent would start
2. **Follow the workflow exactly**: Don't fill in gaps with assumptions
3. **Create test assets as needed**: Files, folders, etc. required by the workflow
4. **Document every issue**: Unclear instructions, missing steps, redundant information
5. **Note the result**: Did it work as intended?

### 3. Document Results

Record detailed results in the scenario file:
- **PASS**: Workflow worked as intended, instructions were clear
- **FAIL**: Issues encountered (document what went wrong)

### 4. Revert Test Assets

After the simulation:
- Revert any files, folders, or changes created during the test
- Keep only the scenario documentation and test results

### 5. Refine and Retest

Based on FAIL results:
1. Update workflow documentation to address issues
2. Re-run the scenario to verify fixes
3. Repeat until PASS

## Regression Testing

When making further changes to a workflow:

1. **Run existing regression tests**: Execute scenarios in `/research/workflow-modeling/regression-tests/`
2. **Verify no regressions**: Ensure previously working scenarios still PASS
3. **Add new scenarios**: If new functionality is added, create new test scenarios

### Archiving Successful Scenarios

When scenarios consistently PASS:
1. Move from `/scenarios/` to `/regression-tests/`
2. Use them as regression tests for future changes

## Verbosity and Redundancy Testing

Part of the refinement process is testing whether workflows are overly verbose or contain unused information:

### Testing for Verbosity

1. **Identify potentially verbose sections**: Long explanations, repeated information
2. **Create simplified version**: Trial removing or condensing the section
3. **Run tabletop simulation**: Does it still work?
4. **Compare results**: 
   - If PASS with simpler version → Keep the simplification
   - If FAIL without the detail → Detail was necessary

### Testing for Redundancy

1. **Identify potentially redundant sections**: Information that appears multiple times
2. **Create version with consolidated information**: Combine or reference instead of repeating
3. **Run tabletop simulation**: Does consolidation cause confusion?
4. **Compare results**:
   - If PASS with consolidation → Keep it
   - If FAIL → Redundancy serves a purpose (e.g., different contexts)

### Example Refinement Process

```
Original workflow section: 500 words explaining research folder structure

Test 1: Remove detailed examples → FAIL (confusion about where to put files)
Test 2: Keep examples, remove redundant explanations → PASS
Test 3: Move examples to appendix with reference → FAIL (too much back-and-forth)

Result: Keep examples inline but remove redundant explanations
```

## Self-Improvement Loop Integration

Process modeling directly feeds the self-improvement loop:

1. **During Testing**: Document what works well and what doesn't
2. **In Refinement**: Implement improvements based on feedback
3. **Before Completion**: Add evaluation to `.github/workflow-improvements.md`
4. **Archive Learnings**: Include key insights in plan.md before archiving

## History Tracking

Process modeling maintains a chronological log in `/research/workflow-modeling/history.md` to track completed improvements.

### When to Add History Entries

Add an entry when:
- Completing process modeling work (issue-driven or backlog-driven)
- Workflow documentation has been updated
- Before archiving the plan

### History Entry Format

```markdown
- **YYYY-MM-DD**: Brief description of the workflow improvement made
```

**Guidelines:**
- Keep descriptions to one line (can wrap if needed, but stay concise)
- Focus on the outcome/benefit, not the process details
- Place newest entries first within the year
- Examples:
  - `- **2025-11-08**: Added backlog-driven mode to Process Modeling Workflow`
  - `- **2025-11-08**: Simplified Product Prioritization template (55% reduction)`

### For Backlog-Driven Mode

When processing an entry from workflow-improvements.md:
- Add history entry even if improvement was not viable
- For unsuccessful improvements: note that it was attempted
- Example: `- **2025-11-08**: Attempted X improvement but determined not viable after testing`

## Completing Process Modeling Work

When work on an issue is complete:

1. **Verify all tests PASS**: Run full regression test suite
2. **Update all affected files**: Workflow docs, copilot-instructions, templates
3. **Add history entry**: Update `/research/workflow-modeling/history.md` with one-line summary
4. **For backlog-driven mode**: Remove processed entry from `.github/workflow-improvements.md`
5. **Complete self-improvement evaluation**: Add to workflow-improvements.md (issue-driven) or included in history (backlog-driven)
6. **Archive the plan**: Move current plan to `/research/workflow-modeling/archive/YYYY-MM-DD-issue-NNN.md`
7. **Clear plan.md**: Ready for next process modeling work (or mark as "No active work")

## Example Process Modeling Session

**Issue**: Improve Research Workflow handover template clarity

1. **Create scenario**: "Research team hands over POC exploration results"
2. **Run simulation**: Follow workflow from copilot-instructions
3. **Result**: FAIL - handover template section unclear about prototype code
4. **Refine**: Update RESEARCH_WORKFLOW.md with clearer prototype guidance
5. **Retest**: Run scenario again → PASS
6. **Check verbosity**: Try condensed version → PASS
7. **Archive scenario**: Move to regression-tests
8. **Update plan.md**: Mark complete
9. **Self-improvement**: Add learnings to workflow-improvements.md

## Success Criteria

Process modeling work is successful when:

- ✅ All test scenarios PASS
- ✅ Regression tests continue to PASS
- ✅ Workflow documentation is updated
- ✅ Verbosity/redundancy checks complete
- ✅ Self-improvement evaluation complete
- ✅ Plan archived with clear completion status

---

**Remember**: The goal is to make workflows clear, effective, and maintainable through systematic testing and refinement.
