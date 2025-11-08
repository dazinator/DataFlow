# Scenario 003: Simplified - Simple Workflow Improvement Proposal

## Context
Testing the PROPOSED simplified workflow-improvements.md template with a simple, straightforward improvement proposal. Same scenario as 001, but with new template.

## Starting Point
User wants to propose adding a clarification to the Research Workflow about when to create ADRs. They've noticed confusion in past issues about this.

## Steps to Follow

### Step 1: User Creates Issue
User fills out the SIMPLIFIED workflow-improvements.md template.

**Information user has:**
- Problem: Unclear when to create ADRs during research
- Solution: Add a decision tree to RESEARCH_WORKFLOW.md
- Affected workflow: Research Workflow

**Template sections to complete:**
1. Which Workflow Does This Affect? → ✅ Check "Research Workflow"
2. Problem Statement → ✅ "Researchers unsure when ADR is needed vs when design doc is sufficient"
3. Who is affected? → ✅ Check "Copilot agents"
4. Proposed Improvement → ✅ "Add decision tree: If architectural choice affects multiple components → ADR; If documenting implementation approach → Design doc"
5. Expected Benefits → ✅ "Clearer guidance, more consistent documentation"
6. Additional Context → ✅ "Noticed in issue #123 where researcher created both for same decision"

**Time spent:** 3-5 minutes (vs 10-15 with old template)

### Step 2: User Experience
**Smooth flow:**
- ✅ Every section is clearly relevant to their proposal
- ✅ No searching for file paths
- ✅ No listing multiple files
- ✅ No premature investigation work
- ✅ Focus stays on problem and solution
- ✅ Example is optional, user includes it because it's easy and relevant

**User feeling:**
- "This was quick and focused"
- "I could describe the actual problem well"
- "Didn't get bogged down in meta-work"

### Step 3: Copilot Agent Processing
Copilot receives issue and follows Process Modeling workflow:

1. ✅ Reads copilot-instructions.md → Directed to PROCESS_MODELING_WORKFLOW.md
2. ✅ Reads PROCESS_MODELING_WORKFLOW.md → Clear instructions
3. ✅ Creates/updates plan.md → Works
4. ✅ Understands problem and proposed solution clearly
5. ✅ Begins investigation:
   - Discovers `.team/workflows/RESEARCH_WORKFLOW.md` using naming convention
   - Reads current documentation
   - Identifies where decision tree would fit
6. ✅ Creates test scenarios
7. ✅ Executes tabletop simulation
8. ✅ Implements improvement

**Copilot investigation:**
- Uses naming convention `[Name]_WORKFLOW.md` to find file
- Reads current state
- Identifies other potentially affected files (copilot-instructions.md references)
- This discovery work is APPROPRIATE for the workflow

### Step 4: Clarifications (if needed)
If copilot needs clarification:
- Posts comment: "Should the decision tree also cover when to use both ADR and design doc?"
- User responds quickly (focused question, easy answer)
- Workflow continues

## Expected Outcome

**For User:**
- ✅ Fast, focused proposal creation (3-5 min vs 10-15 min)
- ✅ Energy spent on problem/solution description
- ✅ Feels productive, not bureaucratic
- ✅ More likely to propose improvements

**For Copilot:**
- ✅ Clear problem and solution understanding
- ✅ Investigation work done as part of workflow (as designed)
- ✅ May ask 0-1 clarifying questions (acceptable)
- ✅ Successfully completes workflow

**For Process:**
- ✅ Faster cycle time for simple improvements
- ✅ Lower barrier encourages more proposals
- ✅ Workflow used as designed (investigation during execution)
- ✅ Quality of outcome unchanged

## Success Criteria
- [x] User can complete template quickly
- [x] Template feels appropriately sized
- [x] User focuses on problem/solution
- [x] Copilot can discover needed information during workflow
- [x] Copilot can execute workflow successfully
- [x] Process works end-to-end

## Test Result

**Status**: PASS ✅

**Notes:**
Simplified template works well for simple improvements:

1. ✅ **Fast for users**: 3-5 min vs 10-15 min (67% time reduction)
2. ✅ **Better focus**: User describes problem and solution well
3. ✅ **Appropriate discovery**: Copilot does investigation as part of workflow
4. ✅ **Works end-to-end**: No blockers or missing information
5. ✅ **Lower barrier**: User more likely to propose improvements
6. ✅ **Naming convention works**: Copilot finds files using documented patterns

**Key improvements over baseline:**
- User focuses on describing the actual problem, not meta-documentation
- Investigation work happens during workflow execution (appropriate separation)
- Template completion feels productive, not bureaucratic
- Copilot still has clear guidance and can discover what it needs

**Potential concern addressed:**
"Will copilot need to ask more questions?" - In this scenario, no. The problem and solution are clearly described. If clarification were needed, a focused question would be quick to answer. This is better than user doing exhaustive upfront work that may not be needed.
