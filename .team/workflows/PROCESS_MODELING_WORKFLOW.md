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
3. Select one of the backlog-driven modes:
   - **Backlog-Driven - Single**: Process one entry (default)
   - **Backlog-Driven - Multiple**: Process N entries (specify count)
   - **Backlog-Driven - Smart**: Process multiple with intelligent stopping
4. Assign to @copilot or mention @copilot in comments

**Backlog-Driven Mode Options:**

#### Single Item Mode (Default)

Process exactly one backlog entry then stop.

**For @copilot executing single-item mode:**

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
7. **STOP** - Do not process additional entries

**Entry Removal**: Remove the entire entry even if improvements were not viable. This prevents the queue from getting stuck. Document unsuccessful attempts in history.md.

#### Multiple Items Mode

Process a specific number of backlog entries (user specifies count).

**For @copilot executing multiple-items mode:**

1. **Check issue description** for specified count (e.g., "Process 3 items")
2. **Initialize tracking**:
   - `items_to_process` = [count from issue]
   - `items_processed` = 0
3. **Update** `/research/workflow-modeling/plan.md` with:
   - Mode: Multiple Items (N items)
   - Items to process: [count]
   - Running summary of processed items

**For each iteration (repeat N times or until backlog exhausted):**

4. **Check**: If `items_processed >= items_to_process` → **STOP**
5. **Read** `.github/workflow-improvements.md`
6. **Select** next unaddressed entry (same selection criteria as single-item mode)
7. **Extract context** from the entry
8. **Create scenarios and test** for this improvement
9. **Apply changes** if viable
10. **Update tracking**:
    - `items_processed++`
    - Add summary to plan.md
11. **Update history.md** with new entry for this improvement
12. **Remove entry** from `.github/workflow-improvements.md`
13. **Update PR description** with consolidated summary (see consolidation pattern below)
14. **Check backlog**: If no more unaddressed entries → **STOP**
15. **Loop back** to step 4

**After all iterations:**

16. **Finalize PR description** with complete summary of all improvements
17. **Archive plan** to `/research/workflow-modeling/archive/` with summary of all items
18. **Note in plan**: "Processed [count] items in multiple-items mode"

#### Smart Mode (Recommended for Batch Processing)

Process multiple entries with intelligent stopping criteria. Stops when max items reached, change volume threshold exceeded, or backlog exhausted.

**Default Thresholds** (configurable in issue description):

- **MAX_ITEMS**: 5 items
- **MAX_LINES_THRESHOLD**: 500 lines changed (insertions + deletions)

**For @copilot executing smart mode:**

1. **Check issue description** for custom thresholds (if specified)
   - If not specified, use defaults above
2. **Initialize tracking**:
   - `items_processed` = 0
   - `total_lines_changed` = 0
   - `max_items` = 5 (or custom value)
   - `max_lines` = 500 (or custom value)
3. **Update** `/research/workflow-modeling/plan.md` with:
   - Mode: Smart Mode
   - Thresholds: [max_items] items, [max_lines] lines
   - Running metrics

**For each iteration:**

4. **Check stopping conditions BEFORE processing next item**:
   - If `items_processed >= max_items` → **STOP** (reason: max items threshold)
   - If `total_lines_changed >= max_lines` → **STOP** (reason: change volume threshold)
   - If no more unaddressed entries → **STOP** (reason: backlog exhausted)
   - **Exception**: Always process at least 1 item, even if it exceeds thresholds
5. If not stopping: **Read** `.github/workflow-improvements.md`
6. **Select** next unaddressed entry
7. **Extract context** and process improvement
8. **Apply changes** if viable
9. **Track changes** for this improvement:
   ```bash
   # Get line changes for current improvement
   git diff --stat | tail -1
   # Example output: "3 files changed, 42 insertions(+), 15 deletions(-)"
   # Extract insertions + deletions: 42 + 15 = 57 lines changed
   # Can use: git diff --stat | tail -1 | awk '{print $4 + $6}'
   ```
10. **Update tracking**:
    - `items_processed++`
    - `total_lines_changed +=` (insertions + deletions from git diff)
    - Add metrics to plan.md
11. **Update history.md** with new entry
12. **Remove entry** from `.github/workflow-improvements.md`
13. **Update PR description** with running summary and metrics
14. **Loop back** to step 4

**After stopping:**

15. **Finalize PR description** with:
    - Total items processed
    - Total lines changed
    - Reason for stopping
    - List of all improvements
16. **Archive plan** with complete summary and stopping reason
17. **Note in plan**: "Processed [X] items in smart mode. Stopped due to: [reason]"

**Change Volume Measurement:**

- Use `git diff --stat` to count insertions + deletions
- Include workflow documentation changes
- Include issue template changes
- Exclude test scenario files (those get reverted after testing)

**Stopping Reason Examples:**

- "Stopped after 5 items (max items threshold)"
- "Stopped after 3 items with ~520 lines changed (change volume threshold)"
- "Stopped after 4 items (backlog exhausted - no more unaddressed entries)"

**Edge Case**: If first item alone exceeds line threshold, still process it (minimum 1 item rule). Then stop before processing 2nd item.

## Long-Lived Research Folder Structure

Unlike other research workflows that create date-based folders, process modeling uses a **long-lived folder**:

```
/research/workflow-modeling/
├── plan.md                          # Current work tracking
├── history.md                       # Chronological log of improvements
├── tools/                           # Automation scripts
│   ├── README.md                   # Tool documentation
│   ├── reset-plan.sh               # Script to reset plan.md
│   └── plan-template.md            # Manual template
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

### PR Description Consolidation Pattern (Multi-Item Mode)

When processing multiple backlog items, consolidate findings in the PR description after each iteration. Use this template:

```markdown
# Process Modeling - Multiple Backlog Improvements

## Summary
Processed X backlog improvements in [mode name] mode.

## Items Addressed

### 1. [Improvement Short Name]
- **Area**: [workflow names affected]
- **Benefit**: [expected benefit and rationale]
- **Changes**: [brief description of what was updated]
- **Lines Changed**: ~[count from git diff]
- **Test Scenarios**: [scenario names]

### 2. [Next improvement...]
- **Area**: ...
- **Benefit**: ...
- **Changes**: ...
- **Lines Changed**: ~[count]
- **Test Scenarios**: ...

[Continue for each item processed]

## Cumulative Metrics
- **Total items processed**: X
- **Total lines changed**: ~Y
- **Workflows affected**: [unique list across all items]
- **Stopping reason**: [max items | change volume | backlog exhausted]

## Test Scenarios
All scenarios created in `/research/workflow-modeling/scenarios/[workflow-name]/`

[List all scenario files created across all improvements]

## History Updated
All improvements added to `/research/workflow-modeling/history.md` as individual entries.
```

**Update Pattern:**

- After processing each item, append new section to "Items Addressed"
- Update "Cumulative Metrics" with running totals
- Keep PR description current for transparency
- Use `report_progress` tool to update PR description incrementally

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

History is maintained as a markdown table in `/research/workflow-modeling/history.md`:

```markdown
| Date | Area | Improvement | Benefit | Scenario | PR |
|------|------|-------------|---------|----------|-----|
| YYYY-MM-DD | Workflow(s) | Brief description | Expected benefit (Rationale) | Test scenarios | [#XXX](url) or N/A |
```

**Column Definitions:**
- **Date**: When improvement was completed (YYYY-MM-DD)
- **Area**: Which workflow(s) were improved (e.g., "Implementation", "Research", "Process Modeling", "Multiple: Research, Implementation")
- **Improvement**: Brief description (keep under 80 chars if possible)
- **Benefit**: Expected outcome with rationale in parentheses (keep concise)
- **Scenario**: Brief scenario names used for validation (e.g., "Executive audit, benefit extraction")
- **PR**: Link format `[#XXX](https://github.com/uniun-technology/lib-dataflow/pull/XXX)` or "N/A" if not from PR

**Guidelines:**
- Add new row at top of table (newest first)
- Identify all affected workflow areas clearly
- Include scenario names from regression-tests or archived plan
- Keep benefit concise but capture key value
- Use proper markdown table format

**Example:**
```markdown
| 2025-11-08 | Implementation | Baseline artifacts and bulk migration guidance | Reduces implementation confusion and repetitive manual work (Clear criteria for artifact handling; automation thresholds) | Implementation plan check, baseline artifacts, bulk migrations | [#185](https://github.com/uniun-technology/lib-dataflow/pull/185) |
```

### For Backlog-Driven Mode

When processing an entry from workflow-improvements.md:
- Add history table row even if improvement was not viable
- For unsuccessful improvements: note "Attempted but not viable" in Improvement column
- Include scenario that determined non-viability

## Completing Process Modeling Work

When work on an issue is complete:

1. **Verify all tests PASS**: Run full regression test suite
2. **Update all affected files**: Workflow docs, copilot-instructions, templates
3. **Add history table row**: Update `/research/workflow-modeling/history.md` with new row including area, benefit, scenario, and PR
4. **For backlog-driven mode**: Remove processed entry from `.github/workflow-improvements.md`
5. **Complete self-improvement evaluation**: Add to workflow-improvements.md (issue-driven) or included in history (backlog-driven)
6. **Archive the plan**: Move current plan to `/research/workflow-modeling/archive/YYYY-MM-DD-[name].md`
7. **Reset plan.md to clean state**: Use automation script (recommended) or manual template
   - **Recommended**: Use the reset script for automatic archive scanning
     ```bash
     cd /research/workflow-modeling/tools
     ./reset-plan.sh "YYYY-MM-DD" "Brief Description" "YYYY-MM-DD-[name].md"
     ```
   - **Manual**: Copy from template and update sections manually
     ```bash
     cp /research/workflow-modeling/tools/plan-template.md plan.md
     # Then edit Recent Completion and Archive sections
     ```
   - **CRITICAL**: Replace entire file content, don't append
   - Verify no duplicate sections before committing

### Automated Plan Reset (Recommended)

The `reset-plan.sh` script automates plan.md reset to prevent duplicates and ensure consistency:

**Location**: `/research/workflow-modeling/tools/reset-plan.sh`

**Usage**:
```bash
cd /research/workflow-modeling/tools
./reset-plan.sh <completion_date> <completion_description> <archive_file>
```

**Example**:
```bash
./reset-plan.sh "2025-11-08" "Multi-Item Backlog Processing" "2025-11-08-multi-item-processing.md"
```

**Benefits**:
- Automatically scans all archived plans and populates Archive section
- Ensures only ONE of each section (no duplicates)
- Extracts descriptions from archive files
- Lists archives chronologically (newest first)

See `/research/workflow-modeling/tools/README.md` for complete documentation.

### plan.md Clean State Template (Manual Alternative)

If the script is unavailable, manually reset using this template structure:

```markdown
# Process Modeling Plan

## Current Work

**Status**: No active work

---

## How to Start New Work

When a new workflow improvement issue is assigned:

1. Update this section with issue details
2. Create test scenarios in `/scenarios/[workflow-name]/`
3. Execute tabletop simulations
4. Document results and refine workflows
5. Archive this plan when complete

## Recent Completion

**Last Completed**: YYYY-MM-DD - [Brief Description]
**See Archive**: `/research/workflow-modeling/archive/YYYY-MM-DD-[name].md`

## Archive

Previous work can be found in `/research/workflow-modeling/archive/`:
- `YYYY-MM-DD-[name].md` - [Description]
- `YYYY-MM-DD-[name].md` - [Description]
- ... (list all archived plans chronologically, newest first)
```

**Key Points:**
- Only ONE "Current Work" section
- Only ONE "Recent Completion" section
- Only ONE "Archive" section
- Replace entire file, don't edit incrementally
- Verify structure before committing

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
