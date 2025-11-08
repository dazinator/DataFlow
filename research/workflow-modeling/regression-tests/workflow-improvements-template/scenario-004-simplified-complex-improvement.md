# Scenario 004: Simplified - Complex Multi-Workflow Improvement

## Context
Testing the PROPOSED simplified workflow-improvements.md template with a complex improvement affecting multiple workflows. Same scenario as 002, but with new template.

## Starting Point
User wants to propose standardizing terminology across all workflows. They've noticed that "handover document" is called different things in different workflows, causing confusion.

## Steps to Follow

### Step 1: User Creates Issue
User fills out the SIMPLIFIED workflow-improvements.md template.

**Information user has:**
- Problem: Inconsistent terminology across workflows
- Solution: Standardize on "handover document" term
- Affected workflows: Multiple

**Template sections to complete:**
1. Which Workflow Does This Affect? → ✅ Check "Any/All Workflows (systemic change)"
2. Problem Statement → ✅ "Different workflows use different terms for the same concept (handover doc, transition doc, implementation spec). This creates confusion when reading different workflows."
3. Who is affected? → ✅ Check "All of the above"
4. Proposed Improvement → ✅ "Standardize on 'handover document' terminology across all workflow documentation. Update glossary entries and cross-references to use consistent term."
5. Expected Benefits → ✅ "Consistent terminology reduces confusion. Easier to understand relationships between workflows. Clearer for new contributors."
6. Additional Context → ✅ "Examples: RESEARCH_WORKFLOW.md uses 'handover document', but IMPLEMENTATION_WORKFLOW.md sometimes says 'transition spec'. Noticed this caused confusion in issue #X where implementer wasn't sure if 'transition spec' meant the same thing."

**Time spent:** 5-8 minutes (vs 20-25 with old template)

### Step 2: User Experience
**Smooth flow:**
- ✅ User describes problem clearly without listing files
- ✅ User describes solution without listing every affected file
- ✅ User provides helpful example without exhaustive search
- ✅ No time wasted on grep searches
- ✅ Focus on problem/solution, not file archaeology

**User feeling:**
- "I could explain the real issue without getting lost in details"
- "Didn't need to figure out every file - that's the workflow's job"
- "Actually feels doable to propose systemic improvements now"

**What user DOESN'T do (vs baseline):**
- ❌ Grep search for all files with terminology
- ❌ List 8-10 file paths
- ❌ Predict all affected files
- ❌ Spend 20 minutes on template

### Step 3: Copilot Agent Processing
Copilot receives issue and follows Process Modeling workflow:

1. ✅ Reads copilot-instructions.md → Clear direction
2. ✅ Reads PROCESS_MODELING_WORKFLOW.md → Understands systemic change process
3. ✅ Creates/updates plan.md → Tracks work
4. ✅ Understands problem: terminology inconsistency
5. ✅ Understands solution: standardize on "handover document"
6. ✅ Begins investigation (THIS IS WHERE DISCOVERY HAPPENS):
   ```
   - Search repo for "handover document"
   - Search repo for "transition doc", "transition spec"
   - Search repo for "implementation spec"
   - Identifies all files with the terminology
   - Maps out which workflows are affected
   - Discovers: RESEARCH_WORKFLOW.md, IMPLEMENTATION_WORKFLOW.md, 
     copilot-instructions.md, example templates, issue templates
   ```
7. ✅ Creates comprehensive test scenarios covering all affected files
8. ✅ Executes tabletop simulations
9. ✅ Implements changes across all discovered files

**Copilot work (appropriate for workflow):**
- Systematic search for all occurrences (better suited to agent than human)
- Discovery of edge cases user might have missed
- Comprehensive testing across all affected workflows
- This is EXACTLY what Process Modeling workflow is designed for

### Step 4: Clarifications (if needed)
If copilot needs clarification:
- Posts comment: "Found these terms: 'handover document', 'transition spec', 'implementation spec', 'handover package'. Should all be standardized to 'handover document'?"
- User responds: "Yes, and 'handover package' should become 'handover folder' to match our folder terminology"
- Workflow continues with refined understanding

## Expected Outcome

**For User:**
- ✅ Much faster proposal (5-8 min vs 20-25 min, 70% reduction)
- ✅ Describes problem clearly
- ✅ Doesn't need to do exhaustive discovery
- ✅ Feels empowered to propose systemic changes
- ✅ Template scales - similar effort for simple and complex proposals

**For Copilot:**
- ✅ Clear understanding of problem and solution
- ✅ Does appropriate investigation work:
  - Systematic search (better suited to agent)
  - Comprehensive file discovery
  - Edge case identification
- ✅ May ask 1-2 clarifying questions (valuable refinement)
- ✅ Successfully completes complex systemic change

**For Process:**
- ✅ Template doesn't punish complexity
- ✅ Encourages systemic improvements (lower barrier)
- ✅ Better division of labor: human describes problem, agent does discovery
- ✅ Higher quality outcomes (agent finds cases user would miss)

## Success Criteria
- [x] User can complete template for complex change without excessive effort
- [x] Template scales gracefully (similar effort to simple changes)
- [x] User focuses on problem/solution (not file lists)
- [x] Copilot can discover all affected files during workflow
- [x] Copilot can execute workflow successfully
- [x] Copilot finds cases user might have missed
- [x] Process works end-to-end for systemic changes

## Test Result

**Status**: PASS ✅

**Notes:**
Simplified template excels at complex changes:

1. ✅ **Scales gracefully**: 5-8 min vs 5 min for simple (minimal penalty for complexity)
2. ✅ **Baseline was 20-25 min**: 75% time reduction for complex changes
3. ✅ **Encourages systemic improvements**: Barrier removal is dramatic
4. ✅ **Better division of labor**: 
   - Human: Identifies problem and desired outcome
   - Agent: Comprehensive discovery and implementation
5. ✅ **Higher quality**: Agent's systematic search finds edge cases
6. ✅ **Focused questions**: If clarification needed, questions are specific and valuable

**Key improvements over baseline:**
- **Dramatic** reduction in user burden for complex changes
- User energy goes to problem description, not file archaeology
- Agent does comprehensive discovery (better than human manual search)
- Process encourages rather than discourages systemic improvements
- Template effort doesn't scale with change complexity (good!)

**Critical insight:**
The baseline template was especially painful for complex changes. The simplified template makes complex changes nearly as easy as simple ones. This is exactly what we want - don't punish good ideas that happen to be systemic.

**Validation of approach:**
Complex changes are where the simplified template shows its biggest wins:
- Old: 20-25 min of manual file discovery
- New: 5-8 min describing the problem
- Copilot does better job at discovery anyway (systematic, comprehensive)
