# Process Modeling Workflow

---

## ⚠️ CRITICAL: This is a Specialized Research Workflow

**If you're a Copilot agent working on a process modeling issue:**

This workflow is a specialized variant of the Research Workflow for systematically improving and refining team workflows and processes. Like other research workflows, process modeling produces **documentation and refined workflows**, not direct code merges (except for workflow documentation updates).

**⚠️ Comment Prefix Convention:**
- Prefix ALL comments with `[Copilot-Workflow: Process Modeling]` to confirm you're following this workflow
- Example: `[Copilot-Workflow: Process Modeling] I've created test scenarios and am ready to run tabletop simulations...`

### DO (During Process Modeling):
- ✅ **Read [Document Hygiene Guide](../DOCUMENT_HYGIENE.md)** before creating/updating documentation
- ✅ Use the long-lived `/research/workflow-modeling/` folder for all process modeling work
- ✅ Create or update `/research/workflow-modeling/plan.md` to track current work
- ✅ Create test scenarios in `/research/workflow-modeling/scenarios/` for tabletop simulation tests
- ✅ Perform tabletop simulation tests for all workflow changes
- ✅ Document test results (PASS/FAIL) with detailed feedback
- ✅ Build regression test suite from successful scenarios (optional - see guidance below)
- ✅ Refine workflows based on testing feedback
- ✅ Test for verbosity and redundancy - trial removing portions and retest
- ✅ Revert temporary test assets after each simulation (see details below)

### DO (After Testing and Refinement):
- ✅ Update workflow documentation files in `.team/prompts/[Name]_WORKFLOW.md`
- ✅ Update `.github/copilot-instructions.md` if systemic changes are made
- ✅ Update issue templates in `.github/ISSUE_TEMPLATE/` if needed
- ✅ Archive valuable test scenarios as regression tests (optional - default: revert; see archiving guidance)
- ✅ Update plan.md with completion status
- ✅ **Complete self-improvement evaluation** in `.github/workflow-improvements.md`

### DON'T:
- ❌ Skip tabletop simulation testing
- ❌ Make workflow changes without testing them first
- ❌ Keep temporary test assets (mock files, test data) after simulation
- ❌ Skip regression testing when making further changes

### OUTCOME:
Process modeling produces **refined workflow documentation** as the primary output. Test scenarios are created, used for validation, then either:
- **Reverted** (typical): Most scenarios are temporary and get deleted after validation
- **Archived** (optional): Valuable scenarios can be kept in `/regression-tests/` for future use

See [Scenario Lifecycle](#scenario-lifecycle) section for detailed guidance on when to revert vs archive.

---

## Workflow Queue

**Query issues designated to this workflow:**

**For Copilot Agents** (use MCP tools):
```python
list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:process-modeling"],
    state="OPEN"
)
```

**For Manual/CI Use** (GitHub CLI):
```bash
gh issue list \
  --label "workflow:process-modeling" \
  --state open \
  --json number,title,url
```

**Entry Points:**
- From Triage workflow (process improvement identified)
- From backlog-driven mode (processing workflow-improvements.md entries)
- From ad-hoc process improvement requests

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete documentation on querying and handover patterns.

---

## Overview

Process modeling is the systematic process of improving team workflows and processes through iterative testing and refinement. This workflow enables Copilot agents to understand workflow pain points, propose improvements, test them through tabletop simulation, and refine based on feedback.

**📝 Terminology Note**: The improvement tracking file is `.github/workflow-improvements.md` (not "process-improvements.md"). This file tracks suggestions for improving workflows and processes.

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
   
   **Suggestion Context Consideration:**
   - Check when suggestion was made and in what context
   - Implementation-phase feedback may not reflect steady-state experience
   - Context examples:
     * During implementation: May reflect temporary confusion, not long-term issue
     * During first use: May reflect learning curve, not workflow gap
     * After extended use: Likely reflects genuine pain point
   - Consider whether pain point is still relevant
   - Document context assessment in archived plan
   - Still evaluate the suggestion, but be aware context affects relevance
4. **Update** `/research/workflow-modeling/plan.md` with:
   - Selected entry details
   - Which specific improvements you're addressing
   - Expected workflow changes
5. **Follow standard process modeling** (create scenarios, test, refine, etc.)
   
   **Already Implemented Detection:**
   
   When testing reveals an improvement is already implemented:
   1. Document the finding in scenario test notes:
      - Note: "Testing revealed improvement already exists in [file]"
      - Cite specific section/line numbers showing existing implementation
      - Include evidence (quotes from current workflow)
   2. Still count as successful processing of entry:
      - Improvement is addressed (even if by earlier work)
      - Prevents entry from staying in backlog indefinitely
   3. Remove entry from backlog per standard process:
      - Entry is "done" even though no new changes were made
      - Keeps backlog clean and prevents re-suggestions
   4. Note in history that improvement was already present:
      - History entry format: "Verified [improvement] already present (No changes needed)"
      - Documents that suggestion was evaluated, not ignored
   
   **Missing Section Handling:**
   
   If backlog entry references a non-existent section (e.g., "Using Ecosystem Tools"):
   - Find most appropriate existing section for the guidance
   - Document section choice in archived plan with rationale
   - Don't create new top-level sections just to match backlog suggestion
   - Example: Guidance for "Using Ecosystem Tools" could fit in "Implementation Patterns" or "Coding Standards"
   - Rationale: Maintain existing workflow structure, avoid fragmentation

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
- **MAX_LINES**: 500 lines changed (insertions + deletions)
- **REQUEST_APPROVAL**: Optional - request approval before each new item (default: false)

**For @copilot executing smart mode:**

1. **Check issue description** for custom thresholds (if specified):
   - `max_items`: Number (default 5)
   - `max_lines`: Number (default 500)
   - `request_approval`: yes/no (default no)
   
2. **Initialize tracking**:
   - `items_processed` = 0
   - `total_lines_changed` = 0
   - `max_items` = 5 (or custom value)
   - `max_lines` = 500 (or custom value)
   - `request_approval` = false (or true if specified)
   
3. **Update** `/research/workflow-modeling/plan.md` with:
   - Mode: Smart Mode
   - Thresholds: [max_items] items, [max_lines] lines
   - Approval mode: enabled/disabled
   - Running metrics

**For each iteration:**

4. **Check stopping conditions BEFORE processing next item**:
   
   **First, estimate next item's line changes:**
   - **Framework-heavy improvements** (thresholds, patterns, new features): 150-200 lines typical
   - **Clarification improvements** (guidance notes, examples): 80-120 lines typical
   - **Template updates** (issue templates, small edits): 30-50 lines typical
   
   **If an improvement overlaps multiple categories** (e.g., clarification plus template update):
   - Use the higher estimate if both are substantial
   - Or sum the estimates if they're clearly separate components
   - When in doubt, default to the more conservative (higher) estimate
   - Document your classification choice in plan.md for transparency
   
   **Then, apply conservative stopping decision tree:**
   ```
   Calculate: current_lines + estimated_lines + 50 (safety buffer)
   
   Decision rules:
   - Result > MAX_LINES? → STOP (prevents threshold violation)
   - Result < (MAX_LINES - 100)? → CONTINUE (safe margin)
   - In between? → Evaluate item complexity:
     * Simple clarification → CONTINUE
     * Framework/template change → STOP  
     * When uncertain → STOP (conservative default)
   ```
   
   **Final stopping checks:**
   - If `items_processed >= max_items` → **STOP** (reason: max items threshold)
   - If `total_lines_changed >= max_lines` → **STOP** (reason: change volume threshold)
   - If decision tree says STOP → **STOP** (reason: approaching change volume threshold)
   - If no more unaddressed entries → **STOP** (reason: backlog exhausted)
   - **Exception**: Always process at least 1 item, even if it exceeds thresholds
   
   **Optional: Request approval before continuing (if request_approval=true):**
   - If `items_processed >= 1` and stopping conditions not met → **REQUEST APPROVAL**:
     - _Rationale: Approval is not requested before the first item to ensure every run makes at least some progress, even if thresholds would otherwise prevent it. This guarantees the workflow doesn't stall without processing any items._
     1. Read next unaddressed entry from `.github/workflow-improvements.md`
     2. Reply to PR comment with summary:
        - Entry date and Issue/PR reference
        - Brief description of suggested improvements
        - Current metrics (items processed, lines changed)
        - Link to plan.md
     3. Wait for reviewer response
     4. If approved → Continue to step 5
     5. If not approved → **STOP** (reason: approval not granted)
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
   
   **What to count toward line threshold:**
   - ✅ Workflow documentation (`.team/prompts/*.md`)
   - ✅ Issue templates (`.github/ISSUE_TEMPLATE/*.md`)
   - ✅ Copilot instructions (`.github/copilot-instructions.md`)
   - ❌ Test scenario files (`scenarios/*.md`) - these get reverted
   - ❌ Test assets - these get reverted
   
   **Rationale**: Test scenarios are created during testing, used for validation, 
   then reverted after completion. They don't count toward the line threshold but 
   do require time to create. Focus line tracking on permanent workflow documentation 
   changes only. This prevents inflated line counts from temporary test files while 
   still acknowledging the time investment in scenario creation.
   
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

**Scenario Naming Convention:**

Use descriptive filenames that indicate the scenario type and purpose:

- **Format**: `scenario-NNN-[type]-[brief-description].md`
- **Sequential numbering**: 001, 002, 003, etc. for easy ordering
- **Types**:
  - `baseline`: Testing current workflow state without improvements
  - `improved`: Testing workflow with proposed improvements
  - `verify`: Verifying existing feature or implementation
  - `edge-case`: Testing boundary conditions or unusual situations
  - `regression`: Ensuring existing functionality preserved

**Examples**:
- `scenario-001-baseline-research-handover.md`
- `scenario-002-improved-automated-archiving.md`
- `scenario-003-verify-already-implemented.md`
- `scenario-004-edge-case-empty-backlog.md`
- `scenario-005-regression-single-item-mode.md`

**Scenario Template** (`scenario-NNN-[type]-description.md`):
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

**Testing Scope for Verification:**

When determining how many scenarios to create:

- **For "no change" scenarios** (verifying existing state): 
  - Typically 2-3 baseline tests are sufficient
  - Focus: Confirm current workflow handles the use case
  - Example: Checking if suggested feature already exists
  
- **For new features** (implementing improvements):
  - Typically 4-7 scenarios covering baseline + improved + edge cases
  - Baseline (2): Current state without improvement
  - Improved (2-3): With improvement applied
  - Edge cases (1-2): Boundary conditions, error handling
  - Regression (1): Verify existing functionality preserved
  
- **Quality over quantity**:
  - Comprehensive scenarios better than many superficial ones
  - Each scenario should test something distinct
  - Adjust based on complexity and context
  - If gaps emerge during testing, add more scenarios

**Testing Patterns for Specific Scenarios:**

#### Multi-Condition Feature Testing Pattern

When testing features with multiple stopping conditions or decision points (e.g., smart mode with max items OR max lines OR backlog exhausted):

**Scenario Coverage Structure:**

1. **One scenario per stopping condition**
   - Independently test each condition triggers correctly
   - Example: max items reached, max lines reached, queue exhausted

2. **Edge case scenarios** (1-2 scenarios)
   - Minimum values (e.g., minimum 1 item rule)
   - Maximum values (e.g., very large thresholds)
   - Boundary conditions (e.g., exactly at threshold)

3. **Regression scenarios** (1 scenario)
   - Verify existing behavior preserved
   - Ensure backward compatibility
   - Confirm no side effects

**Typical Scenario Count: 4-7 scenarios**
- Simple features (2 conditions): 4-5 scenarios
- Complex features (3+ conditions): 5-7 scenarios
- Quality over quantity: comprehensive better than superficial

**Example for Smart Mode with 3 Stop Conditions:**
- Baseline: Existing single-item mode works
- Condition 1: Stops on max items (5)
- Condition 2: Stops on max lines (500)
- Condition 3: Stops on backlog exhausted
- Edge Case: Minimum 1 item rule
- Edge Case: First item exceeds line threshold
- Regression: All modes (single/multiple/smart) work

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

**Note**: After achieving PASS results, consider using [Verbosity and Redundancy Testing](#verbosity-and-redundancy-testing) to refine and simplify the workflow documentation.

## Regression Testing

When making further changes to a workflow:

1. **Run existing regression tests**: Execute scenarios in `/research/workflow-modeling/regression-tests/`
2. **Verify no regressions**: Ensure previously working scenarios still PASS
3. **Add new scenarios**: If new functionality is added, create new test scenarios

### Regression Test Patterns

Choose the pattern that best fits your testing needs:

#### Pattern 1: Integrated Scenario Files

**When to use:**
- Small number of scenarios (3-5)
- All scenarios test related functionality
- Want to keep test files simple

**Implementation:**
- Each scenario file includes regression test section
- Re-run all scenarios after making changes
- Document PASS/FAIL status directly in scenario

**Example:**
```markdown
# Scenario: Research Workflow Handover

[... regular scenario content ...]

## Regression Test (Added YYYY-MM-DD)
- Re-tested after [change description]
- Status: PASS
- Notes: All steps still work as expected
```

#### Pattern 2: Separate Regression Test File

**When to use:**
- Large number of scenarios (6+)
- Testing complex multi-step workflows
- Want comprehensive regression validation
- Need to track multiple rounds of testing

**Implementation:**
- Create `regression-test.md` in scenarios folder
- Lists all scenarios and their latest test results
- Update after each round of changes
- Provides audit trail of testing history

**Example structure** (`/research/workflow-modeling/scenarios/[workflow-name]/regression-test.md`):
```markdown
# Regression Test Results - [Workflow Name]

## Test Run: YYYY-MM-DD

**Changes made**: [Brief description of what changed]

### Results

| Scenario | Description | Status | Notes |
|----------|-------------|--------|-------|
| 001 | Baseline scenario | PASS | No issues |
| 002 | Improved with changes | PASS | Works as expected |
| 003 | Edge case handling | FAIL | [Issue description] |
| 004 | Regression check | PASS | No regression |

### Failed Scenarios

**Scenario 003**: [Detailed failure analysis and remediation plan]

## Test Run: YYYY-MM-DD

[Previous test results...]
```

**Benefits**:
- Consolidated view of all testing
- Easy to see trends over time
- Good for complex workflows with many scenarios
- Provides clear audit trail

#### Choosing the Right Pattern

- **3-5 scenarios** → Integrated (Pattern 1) - simpler, less overhead
- **6+ scenarios** → Separate file (Pattern 2) - better organization
- **Simple workflows** → Integrated (Pattern 1) - adequate for basic testing
- **Complex workflows** → Separate file (Pattern 2) - comprehensive validation
- **When in doubt** → Start with integrated, switch to separate if it becomes unwieldy

Both patterns valid - choose based on your specific testing needs and workflow complexity.

## Scenario Lifecycle

Test scenarios have different purposes and lifecycles. Here's when to revert vs archive:

#### Revert Scenarios (Typical - Default Approach)

**When to revert:**
- Scenarios validating one-time workflow improvements
- Baseline vs improved comparisons (once improvement is confirmed)
- Edge case testing that's unlikely to regress
- Simple verification scenarios

**Why revert:**
- Keeps repository clean
- Avoids maintenance burden of outdated scenarios
- Most workflow changes are validated once and don't need re-testing
- Scenarios are documented in archived plan for reference

**How to revert:**
```bash
git rm research/workflow-modeling/scenarios/[workflow-name]/*.md
```

**Example:** Scenarios testing "already implemented detection" - once the guidance is added to the workflow and validated, these scenarios serve no ongoing purpose.

#### Archive Scenarios (Selective - For Regression Testing)

**When to archive:**
- Scenarios testing complex decision logic that could regress
- Multi-step workflows with many integration points
- Features used frequently that have high regression risk
- Scenarios that would be time-consuming to recreate

**Why archive:**
- Enables regression testing when making future changes
- Prevents breaking previously working functionality
- Provides examples for future workflow developers

**How to archive:**
```bash
# Move valuable scenarios to regression-tests
mv research/workflow-modeling/scenarios/[workflow-name]/scenario-NNN-*.md \
   research/workflow-modeling/regression-tests/[workflow-name]/
```

**Example:** Scenarios testing smart mode stopping criteria with multiple conditions - these are complex and should be re-tested if smart mode logic changes.

#### Decision Framework

Ask these questions:

1. **Will this workflow logic change again?** 
   - Yes → Consider archiving
   - No → Revert

2. **Is this logic complex with multiple conditions?**
   - Yes → Consider archiving
   - No → Revert

3. **Would recreating this scenario take >30 minutes?**
   - Yes → Consider archiving
   - No → Revert

4. **Is this testing a new pattern we'll use frequently?**
   - Yes → Archive 1-2 representative scenarios
   - No → Revert

**Default:** When in doubt, revert. Archiving creates maintenance overhead. Only archive scenarios with clear ongoing value.

### Archiving Successful Scenarios

When you decide to archive scenarios:

1. Move from `/scenarios/` to `/regression-tests/`
2. Organize by workflow name
3. Use them as regression tests for future changes

**Timing Guidance:**
- Archive scenarios to `/regression-tests/` AFTER all testing complete and improvements implemented
- Don't archive mid-work (keeps `/scenarios/` clean for active work)
- Archive all scenarios from a workflow together (e.g., all implementation-workflow scenarios at once)
- Archive when: Work is complete, all scenarios PASS, plan is being archived

## Design Guidance for Process Improvements

When designing new workflow features or enhancements, use these patterns:

### Workflow File Structure Consideration

For very long workflow files (600+ lines), consider navigation and structure:

**Options:**
- **Table of contents**: Add anchor links at top for navigation within single file
- **Split files**: Separate into multiple files (e.g., WORKFLOW_NAME.md, WORKFLOW_NAME_ADVANCED.md)

**Current Decision**: Keep workflows as single files
- **Rationale**: Single file easier to search (Ctrl+F), better for comprehensive reading, maintains context
- **Trade-off**: Long files (600-800 lines) require scrolling but remain manageable
- **Future review**: Revisit if workflows exceed 1000 lines or user feedback indicates navigation difficulties

**Recommendation**: Use anchor links and good section structure rather than splitting files

### Design Document Template

For complex multi-file changes, consider creating a design document first to think through the solution before implementing.

**When to create a design doc:**
- Changes affecting 3+ files
- New workflow features or modes
- Complex decision logic or algorithms
- System-wide changes affecting multiple workflows

**Design doc template** (create in `/tmp`):

```markdown
# Design: [Brief Name]

## Goal
What problem are you solving?

## Requirements
What constraints must be met?

## Design Decisions
How will you solve it? Why this approach?

## File Changes
Which files will be modified? What changes in each?

## Testing Plan
How will you validate the changes?
```

**Location**: `/tmp/design-[brief-name].md` (temporary, not committed)

**Benefits**:
- Helps think through solution before implementing
- Catches design issues early
- Provides clear reference during implementation
- Can be referenced in archived plan

### Markdown Table Formatting Guidance

When updating workflow documentation or issue templates with markdown tables:

**Formatting Best Practices:**

1. **Content over perfect alignment**
   - Markdown tables auto-format in most viewers
   - Don't spend time perfecting column alignment
   - Focus on clear, accurate content

2. **Provide example rows**
   - Include at least one concrete example row (not just "...")
   - Helps readers understand expected format and detail level
   - Example: `| 2025-11-08 | Process Modeling | Scenario naming convention | Reduces naming confusion (Clear format provided) | 5 scenarios | [#123](https://github.com/uniun-technology/lib-dataflow/pull/123) |`

3. **Keep tables scannable**
   - Use concise content (aim for <80 chars per cell when possible)
   - Break complex content into bullet points if needed
   - Consider splitting very wide tables

4. **Header clarity**
   - Make column headers descriptive but brief
   - Example: "Benefit" better than "Expected Benefit and Rationale"
   - Use description text below table if clarification needed

**Example: Well-Formatted Table**

```markdown
| Date | Area | Improvement | Benefit | PR |
|------|------|-------------|---------|-----|
| 2025-11-08 | Process Modeling | Add scenario naming convention | Reduces confusion (Provides clear format) | [#185](url) |
| 2025-11-07 | Research | Metrics guidance | Better success tracking (Quantifiable criteria) | N/A |
```

**Common Mistakes to Avoid:**
- ❌ Using "..." as placeholder in examples (not helpful)
- ❌ Over-formatting with perfect spacing (wastes time, editors auto-format)
- ❌ Cells too wide (>100 chars) - makes table hard to scan
- ❌ Missing header row or inconsistent column count

**When to Use Tables vs. Lists:**
- **Tables**: Structured data with multiple attributes per item
- **Lists**: Sequential steps, simple collections, narrative content
- **Tables work well for**: History tracking, comparison data, status reports
- **Lists work better for**: Instructions, requirements, long explanations

### Threshold Selection Guidance

When designing features with numeric thresholds (limits, timeouts, counts):

**1. Consider Typical Use Cases**
- What values work for 80% of scenarios?
- Examples: typical PR size, average item count, common file sizes
- Gather data from past examples if available

**2. Balance Restrictive vs Permissive**
- **Too restrictive**: Frustrates users, requires frequent overrides
- **Too permissive**: Creates unwieldy artifacts (large PRs, slow operations)
- **Sweet spot**: Works for most cases, rare need to override

**3. Align with Best Practices**
- PR size: 3-7 files, 200-500 lines (industry best practice)
- Batch size: Consider processing speed vs memory
- Timeout: Balance responsiveness vs reliability

**4. Document Rationale**
- Why this value? What data/reasoning supports it?
- Example: "MAX_ITEMS=5 aligns with typical PR size best practices"
- Include in workflow documentation for future reference

**5. Plan for Monitoring and Adjustment**
- Document where threshold is defined (easy to find and adjust)
- Note that values can be tuned based on empirical usage data
- Consider making configurable if use cases vary widely

**Example:**
```markdown
## Configuration

**MAX_ITEMS**: 5 (default)
- Rationale: Aligns with typical PR size best practices (3-7 files)
- Configurable: Yes, can be overridden per-issue
- Monitoring: Track actual usage and adjust if needed
```

### Configuration Options Design Guidance

When deciding whether to make thresholds or features configurable:

**When to Make Configurable:**
- Use cases vary significantly (different teams, different projects)
- No single "best" value for all scenarios
- Power users need flexibility
- Example: Batch sizes, timeout values, concurrency limits

**When to Keep Fixed:**
- Single best value for all cases
- Configuration would add complexity without benefit
- Value based on fundamental constraints (e.g., API limits)
- Example: File format versions, protocol specifications

**Default Strategy (80% Rule):**
- Choose defaults that work for 80% of cases
- Rare need to override
- Document rationale for chosen defaults
- Make overrides optional, not required

**Configuration Levels:**
1. **Fixed (constants in code)**: When single best value exists
2. **Global configuration**: When applies to all operations
3. **Per-operation parameters**: When varies by use case
4. **Per-issue customization**: When users know their needs best

**Documentation Requirements:**
- Document defaults in workflow/README
- Make easy to find and adjust
- Explain rationale for chosen values
- Example: "MAX_ITEMS=5 (typical PR size), MAX_LINES=500 (reviewable size)"

**Example Decision Process:**
```
Feature: Smart mode thresholds
Question: Configurable or fixed?

Analysis:
- Use cases vary: Some users need more/fewer items
- No single best value: Depends on workflow complexity
- 80% default: MAX_ITEMS=5, MAX_LINES=500
- Optional approval: Some prefer incremental review

Decision: Configurable
Level: Per-issue parameters (optional, with defaults)
Documentation: In Process Modeling Workflow
Rationale: Works for 80%+ cases, approval mode available for incremental control
```

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
- **Area**: Which workflow(s) were improved
  - Single workflow: Use workflow name (e.g., "Implementation", "Research", "Process Modeling")
  - Multiple workflows (2-3): List separated by comma (e.g., "Research, Implementation")
  - Many workflows (4+): Use "Multiple: X, Y, Z" or "Cross-workflow"
- **Improvement**: Brief description (keep under 80 chars if possible)
- **Benefit**: Expected outcome with rationale in parentheses (keep concise)
- **Scenario**: Brief scenario names used for validation
  - If many scenarios (>5): Summarize with count (e.g., "5 scenarios: plan check, artifacts, migrations...")
  - If no scenarios: Use "N/A" or "Direct implementation"
  - Keep concise but informative
- **PR**: Link format `[#XXX](https://github.com/uniun-technology/lib-dataflow/pull/XXX)` or "N/A" if not from PR
  - Always use clickable link format for PRs
  - "TBD" if PR not yet created/merged (update after merge)
  - "N/A" for direct workflow improvements

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

## Identifying When Research Is Needed

Process modeling can handle most workflow changes directly, including updating documentation, creating small helper scripts, and iterating through scenario testing. However, some explorations reveal that **broader dependencies** requiring significant development and testing are needed before workflow changes can be implemented. **When this happens, the work should be handed over to the Research team.**

### What Process Modeling Can Handle Directly

✅ **Process Modeling handles**:
- Updating workflow documentation files (`.team/prompts/*.md`)
- Updating copilot instructions (`.github/copilot-instructions.md`)
- Creating small, single-purpose scripts invoked directly by Copilot during workflows
- Iterating on workflow changes through scenario testing and regression testing
- Making general workflow improvements and documentation updates
- Creating issue templates and workflow patterns

### Decision Framework: When to Create Research Handover

Ask these questions during tabletop simulation and design:

1. **Does this require developing broader dependencies with significant effort?**
   - GitHub Actions workflows (multi-step pipelines, complex automation)
   - Utilities that run within GitHub Actions requiring development and testing
   - Tools requiring significant effort and proper testing cycles (e.g., >4 hours development time, requires multiple testing iterations, or substantial coordination across teams)
   - APIs or integrations with external systems
   - Complex supporting infrastructure

2. **Does this require significant exploration of technical approaches?**
   - Multiple architectural options to prototype and evaluate
   - Performance testing and optimization required
   - Technology stack decisions needing validation

3. **Is the dependency development unclear or complex?**
   - Design requires iteration and prototyping
   - Multiple integration points need investigation
   - Significant edge cases requiring exploration

**If YES to any of the above**: Create a **Research Handover** to develop dependencies first.

**Key distinction**: Small, clear-scope scripts for workflow use → Process Modeling handles. Broader dependencies (GitHub Actions pipelines, complex utilities, tools needing testing cycles) → Research Handover.

### Research Handover Pattern

When broader dependencies requiring research are identified:

**DO (Process Modeling)**:
1. ✅ Complete tabletop simulation and design validation
2. ✅ Create comprehensive handover document for Research team
3. ✅ Explain the problem the workflow is trying to solve
4. ✅ Suggest what dependencies are likely needed
5. ✅ Request exploration and design of dependencies
6. ✅ Document test scenarios that validate the need
7. ✅ Create history entry noting handover to research
8. ✅ Archive the plan with research recommendation

**DO NOT (when creating Research Handover)**:
1. ❌ Update workflow documentation to reference the new dependencies (wait until after research)
2. ❌ Develop the broader dependencies yourself (GitHub Actions pipelines, complex utilities)
3. ❌ Create implementation backlog items for the dependencies (research will create these)

**Note**: Process modeling can still update workflow docs and create small scripts for other improvements. These restrictions apply only when handing over dependency development to research.

**Research Handover Document Template:**

```markdown
# [Feature Name] - Research Handover

## Executive Summary

**Recommendation**: ✅ **PROCEED with research**

[Brief description of what needs to be researched and why]

**⚠️ CRITICAL - Research Scope**:
- **DO**: Develop [broader dependencies: GitHub Actions pipelines, utilities, tools]
- **DO**: Test and validate with [existing systems]
- **DO**: Create product backlog item(s) for any workflow documentation updates needed
- **DO NOT**: Update workflow documentation files (Process Modeling will do this after dependencies are ready)

**Why research is needed**: [Explanation of why these dependencies require significant exploration/development/testing]

## Problem Statement

[Describe the workflow problem being solved and current pain points]

## Suggested Dependencies

[Describe what broader dependencies are likely needed - GitHub Actions workflows, utilities, tools, etc.]

## Technical Design (Suggested)

[Your suggested approach for the dependencies - research team may modify based on exploration]

## Research Phases

[Break down the dependency development work into phases]

## Post-Research: Workflow Integration

**After research completes and dependencies are working**, create a new **Process Modeling issue** to integrate [feature] into existing workflows:

**Scope**: Update workflow documentation to reference the new [dependencies]
**Deliverables**: [List workflow files to update]

**Why separate?**: The dependencies must be developed and tested first. Workflow documentation should only reference working tools.
```

**Why separate?**: The dependencies must be developed and tested first. Workflow documentation should only be updated once proven to work.

## Next Steps for Research Team

1. Review this handover document
2. Create research issue following Research Workflow
3. Develop and test the dependencies
4. Create product backlog item for workflow documentation updates
5. After research complete: Reviewer creates Process Modeling issue for workflow integration
```

### Example: Backlog-to-GitHub Sync

**Process Modeling identified broader dependencies**:
- Needs GitHub Actions workflow (multi-step pipeline, not simple script)
- Needs Python sync script with proper error handling and testing
- Needs tracking mechanism (design exploration required)
- Significant testing and validation cycles required

**Process Modeling created**:
- Research handover document explaining the workflow problem
- Test scenarios validating the need
- Suggested technical design for dependencies
- Request for research to explore and develop the dependencies

**Research will develop**:
- GitHub Actions workflow file (complex automation)
- Sync script with comprehensive testing
- Tracking mechanism and state management
- Validation tests and edge case handling

**Follow-up Process Modeling will**:
- Update workflow documentation to reference the working sync
- Update copilot instructions to mention the sync capability
- Integrate sync into existing workflows

**Contrast with direct Process Modeling**:
- If the need was just a small helper script to reformat a file → Process Modeling creates it directly
- If the need is a complex GitHub Actions pipeline → Research Handover
- If the need is updating workflow docs → Process Modeling does it directly
- If the need is a tested utility requiring development cycles → Research Handover

This separation ensures:
1. ✅ Broader dependencies are properly developed through research cycles
2. ✅ Workflow documentation references working, tested tools
3. ✅ Clear handoff between exploration/development and integration
4. ✅ Process Modeling focuses on workflow design, Research handles dependency development

---

## Completing Process Modeling Work

When work on an issue is complete:

1. **Verify all tests PASS**: Run full regression test suite
2. **Update all affected files**: Workflow docs, copilot-instructions, templates
   
   **Navigation Update Checklist:**
   
   When creating or updating a workflow, check if navigation needs updates:
   - **Copilot Instructions** (`.github/copilot-instructions.md`):
     * Quick Navigation section - should list all major workflows
     * Workflow indicator table (Research vs Implementation vs Process Modeling, etc.)
     * Repository structure section
   - **Prevents workflows from being "hidden"** or hard to discover
   - **Example**: When Process Modeling Workflow was created, it needed to be added to Quick Navigation section
   
   **System-Wide Feature Checklist:**
   
   When adding features that affect multiple workflows:
   1. **Identify affected workflows**: Which workflows need to integrate this feature?
   2. **For each workflow**:
      - Read current version completely
      - Identify integration points (where new feature fits)
      - Update workflow documentation
      - Create test scenario validating integration
   3. **Update copilot-instructions.md**:
      - Repository structure section (if adding new folders/files)
      - Quick Navigation (if adding new workflow/template)
   4. **Update issue templates**: If feature changes how issues are created
   5. **Definition of Done**: All workflows updated, all test scenarios PASS

3. **Add history table row**: Update `/research/workflow-modeling/history.md` with new row including area, benefit, scenario, and PR
   
   **Capturing Benefit and Rationale:**
   
   When creating the history entry, explicitly capture:
   - **Expected Benefit**: What improvement will this provide? Be specific and quantifiable when possible
   - **Rationale**: Why do you expect this benefit? What's the reasoning?
   - Format: "Benefit statement (Rationale in parentheses)"
   - Example: "Significantly reduces scenario creation confusion (Clear conventions prevent reinvention)"
   
   **PR Number Tracking:**
   
   Include PR number in history entry:
   - If work came from PR feedback: Note PR number when starting work in plan.md
   - When completing work: Note PR number that will contain changes
   - Format in history: `[#XXX](https://github.com/uniun-technology/lib-dataflow/pull/XXX)` or "TBD" if not yet merged
   - Update "TBD" to actual PR number after merge
   - Use "N/A" for direct workflow improvements not linked to PR
   
   **Benefit Validation Checkpoint:**
   
   Before marking work complete:
   - Review history entry benefit statement
   - Ask: "Is the benefit clear and specific?"
   - Ask: "Is the rationale explained?"
   - Refine if vague (e.g., avoid "improved workflow" without specifics)
   - Good: "Eliminates 15-30 min troubleshooting per dependency update (Clear patterns for conflicts)"
   - Avoid: "Better workflow" or "Improved process"
   - **When quantifying benefits** (e.g., time savings): Use measured data when available (such as logs, time tracking, or historical records). If only an estimate is possible, clearly indicate it is an estimate and briefly explain the basis (e.g., "Estimated based on typical troubleshooting time in last 3 updates"). This ensures benefit statements are transparent and reproducible.

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

### No Changes Needed - Valid Outcome

Sometimes tabletop simulation reveals the current workflow is working well and proposed improvements wouldn't add value. **This is a VALID outcome.**

**When "no changes needed" is the conclusion:**

1. **Document rationale thoroughly in archived plan:**
   - What was tested (scenarios created)
   - Why current workflow is sufficient
   - Why proposed improvement wouldn't help (or might hurt)
   - Evidence from testing (specific test results)

2. **Still complete full process:**
   - Keep test scenarios as evidence of evaluation
   - Add history entry: "Verified [improvement] already present (No changes needed)" or "Evaluated [suggestion], no changes needed (Current workflow sufficient)"
   - Archive plan with detailed rationale
   - Remove entry from backlog

3. **Value provided:**
   - Prevents future agents from re-evaluating same suggestion
   - Documents that suggestion was thoughtfully considered
   - Shows due diligence in workflow quality
   - Provides evidence for why change was rejected

**Remember**: The goal is workflow quality, not change volume. Sometimes the best improvement is recognizing what's already working well.

## File Replacement vs Incremental Editing

When working with tracking files like `plan.md`, understanding when to replace vs edit is critical to prevent duplicates and maintain consistency.

### File Replacement Pattern (for State Resets)

**When to use:**
- Resetting `plan.md` after completing work
- Updating tracking files to clean initial state
- Any operation that returns a file to a known template state

**How to do it:**
1. Use automation script (recommended): `./reset-plan.sh`
2. Manual: Replace entire file content with template (use `edit` tool with full content)
3. Never: Multiple incremental edits to reset state

**Why this matters:**
- Incremental edits can leave duplicate sections
- Template replacement guarantees clean state
- Automation scripts handle repetitive content (like archive lists)

**Example - CORRECT:**
```python
# Using file editing tool - replace entire content
edit(path="plan.md", 
     old_str="[entire current content]",
     new_str="[complete template with all sections]")
```

**Example - INCORRECT:**
```python
# Multiple incremental edits - can create duplicates
edit(path="plan.md", old_str="## Current Work...", new_str="...")
edit(path="plan.md", old_str="## Recent Completion...", new_str="...")
# Risk: If edit fails midway, file is in inconsistent state
# Risk: Easy to accidentally append instead of replace
```

### Incremental Editing Pattern (for Targeted Updates)

**When to use:**
- Updating specific workflow documentation sections
- Adding new guidance to existing sections
- Fixing typos or clarifying existing content
- Making focused improvements

**How to do it:**
- Use `edit` tool with specific old_str/new_str for targeted change
- Make one logical change per edit operation
- Group related changes if they're in the same section

**Why this works:**
- Changes are precise and reviewable
- Git diffs show exactly what changed
- Reversible if issues occur

### Verification Checklist

Before committing changes to tracking files:

**For plan.md:**
- [ ] Verify structure: Exactly ONE "Current Work" section
- [ ] Verify structure: Exactly ONE "Recent Completion" section
- [ ] Verify structure: Exactly ONE "Archive" section
- [ ] Check for duplicate section headers: `grep -E "^## (Current Work|Recent Completion|Archive)" plan.md | sort | uniq -c`
- [ ] Verify Recent Completion points to correct archive file
- [ ] Verify Archive list is chronological (newest first)

**For workflow documentation:**
- [ ] No duplicate sections or headers
- [ ] All internal links work (if any)
- [ ] Examples are accurate and up-to-date
- [ ] Formatting is consistent with file style

**Quick duplicate check:**
```bash
# Count occurrences of each section header
grep "^## " plan.md | sort | uniq -c
# Should show "1" for each section name
```

### Automated Structure Validation

**Option 1: Simple grep check (lightweight)**
```bash
# Add to completion checklist
duplicate_sections=$(grep "^## " research/workflow-modeling/plan.md | sort | uniq -d)
if [ -n "$duplicate_sections" ]; then
  echo "ERROR: Duplicate sections found:"
  echo "$duplicate_sections"
  exit 1
fi
```

**Option 2: Pre-commit hook (for frequent contributors)**

Create `.git/hooks/pre-commit`:
```bash
#!/bin/bash
# Validate plan.md structure before commit
if git diff --cached --name-only | grep -q "research/workflow-modeling/plan.md"; then
  duplicate_sections=$(grep "^## " research/workflow-modeling/plan.md | sort | uniq -d)
  if [ -n "$duplicate_sections" ]; then
    echo "ERROR: plan.md has duplicate sections:"
    echo "$duplicate_sections"
    echo "Please fix before committing"
    exit 1
  fi
fi
```

**Recommendation**: Start with manual verification checklist. Add automation only if duplicates become recurring issue. Keep checks lightweight - don't over-engineer.

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

### Archived Plan Template

When archiving a plan to `/research/workflow-modeling/archive/YYYY-MM-DD-[name].md`, use this structure for comprehensive record-keeping:

````markdown
# Process Modeling Archived Plan - [Brief Name]

## Summary
[High-level summary of what was accomplished]

## Selected Entry Details
- **Date**: YYYY-MM-DD
- **Issue/PR**: [Reference]
- **Area**: [Workflow names affected]

## Before/After Impact

**Purpose**: Show concrete improvement to help future readers understand the value

**Before** (baseline state):
- [Describe what it was like before the improvement]
- Example: "No guidance on scenario naming - agents had to invent their own conventions"

**After** (improved state):
- [Describe what it's like after the improvement]
- Example: "Clear naming convention provided: scenario-NNN-[type]-description.md with 5 defined types"

**Measured Impact** (if applicable):
- [Quantitative improvements if available]
- Example: "67% reduction in confusion (based on simulation results)"

## Improvements Addressed
[List each improvement with brief description]

1. [Improvement name]: [What was done]
2. [Next improvement]: [What was done]

## Test Results
[Summary of test scenarios and results]
- Scenario 001: [Description] - PASS/FAIL
- Scenario 002: [Description] - PASS/FAIL

## Scenario Statistics

**Created**: [N] scenarios
**Retained**: [N] scenarios (archived to `/research/workflow-modeling/regression-tests/[workflow-name]/`)
**Reverted**: [N] scenarios (temporary validation only)
**Regression Tests Ran**: [N] pre-existing scenarios (if applicable)

**Retention Decision**: [Archive | Revert All]
**Rationale**: [Why scenarios were archived or reverted]

## Files Modified
- `.team/prompts/[NAME]_WORKFLOW.md` - [Changes made]
- `.github/copilot-instructions.md` - [Changes made if applicable]

## Lessons Learned
[Key insights, what worked well, what could be improved for future process modeling work]
````

**Purpose**: Comprehensive record for future reference, maintains institutional knowledge

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

## Handover to Next Workflow

When process modeling work is complete, hand over improved workflows back to the ecosystem.

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete handover patterns and troubleshooting.

### Handover to Triage

**When**: Process improvements complete and workflows updated

**For Copilot Agents** (use MCP tools):
```python
# Update workflow label
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=$ISSUE,
    labels=["workflow:triage"]
)

# Add handover comment
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=$ISSUE,
    body="✅ Process improvements complete. Workflows updated and tested."
)
```

**For Manual Use** (GitHub CLI):
```bash
gh issue edit $ISSUE --remove-label "workflow:process-modeling" --add-label "workflow:triage"
gh issue comment $ISSUE --body "✅ Process improvements complete. Workflows updated and tested."
```

**Note**: Most process modeling issues close after completion rather than handover, since the improvements are already integrated into workflow documentation.

### Close Issue

**When**: Process modeling work complete and all changes deployed

```bash
gh issue close $ISSUE --comment "✅ **Process Modeling Complete**

Process improvements successfully implemented and tested.

**Changes Made**:
- [List workflows updated]
- [List improvements applied]

**Documentation**:
- Plan archived: \`/research/workflow-modeling/archive/[date]-[name].md\`
- History updated: \`/research/workflow-modeling/history.md\`

**Test Results**: All scenarios PASS

See: \`.team/prompts/PROCESS_MODELING_WORKFLOW.md\`"
```

---

**Remember**: The goal is to make workflows clear, effective, and maintainable through systematic testing and refinement.
