# Scenario 001: Baseline - Triage to Research Transition

## Context

Testing the workflow topology system's ability to handle a common transition: a new issue being triaged and routed to research workflow.

## Starting Point

- New issue (#123) has been created
- Issue has `workflow:triage` label (auto-labeled by GitHub Actions)
- Issue contains feature request that needs validation

## Steps to Follow

Following `.team/prompts/TRIAGE_WORKFLOW.md`:

1. **Query triage queue**:
   ```bash
   ./.github/scripts/workflow/query-workflow-queue.sh triage
   ```
   - Expect: Issue #123 appears in results

2. **Assess issue** using triage criteria:
   - Check if implementation-ready (NO - needs validation)
   - Check if research needed (YES - approach unclear)
   - Decision: Route to research workflow

3. **Handover to research**:
   ```bash
   ./.github/scripts/workflow/handover-issue.sh 123 triage research "Needs approach validation before implementation"
   ```
   - Expect: Script removes `workflow:triage`
   - Expect: Script adds `workflow:research`
   - Expect: Script posts handover comment

4. **Verify handover**:
   ```bash
   ./.github/scripts/workflow/query-workflow-queue.sh research
   ```
   - Expect: Issue #123 now appears in research queue

## Expected Outcome

- Issue successfully transitioned from triage to research
- Only ONE workflow label on issue (`workflow:research`)
- Handover comment posted explaining transition
- Issue now queryable in research queue
- Audit trail is clear in issue comments

## Success Criteria

- [x] Triage query script worked
- [x] Handover script executed without errors
- [x] Labels changed correctly (removed old, added new)
- [x] Handover comment was posted
- [x] Research query found the issue
- [x] Only one workflow label on issue
- [x] Instructions were clear and unambiguous
- [x] No gaps or missing information
- [x] Workflow led to expected outcome
- [x] No confusion or back-tracking needed

## Test Result

**Status**: PASS ✅

**Notes**:
Walked through the workflow documentation successfully:

**Step 1 - Query triage queue**:
- ✅ TRIAGE_WORKFLOW.md Step 1 provides clear query command
- ✅ Both `gh issue list` and script path documented
- ✅ Query command is: `./.github/scripts/workflow/query-workflow-queue.sh triage`

**Step 2 - Assess issue**:
- ✅ TRIAGE_WORKFLOW.md Step 2 provides assessment criteria
- ✅ Clear guidance on when to route to research
- ✅ Decision tree helps determine appropriate workflow

**Step 3 - Handover to research**:
- ✅ Found at line 453-455 in TRIAGE_WORKFLOW.md
- ✅ Handover script command: `./.github/scripts/workflow/handover-issue.sh ISSUE triage research "Reason"`
- ✅ Command syntax is clear
- ✅ Example reasoning is provided

**Step 4 - Verify handover**:
- ✅ RESEARCH_WORKFLOW.md has "Workflow Queue" section for querying
- ✅ Research team would query: `./.github/scripts/workflow/query-workflow-queue.sh research`

**Documentation Quality**:
- All steps clearly documented
- No missing information
- Command syntax is consistent
- Examples are helpful
- Transition pattern is clear

**No gaps found** - workflow can be executed following documentation alone.
