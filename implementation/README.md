# Implementation Folder

This folder tracks **in-flight implementations** that span multiple phases or PRs.

## Purpose

When an implementation is too large to complete in a single session (4-8+ hours), we use a phased approach where each phase can be a separate PR. This folder tracks the status of multi-phase implementations.

## Structure

```
/implementation/
├── README.md                    # This file
└── [implementation-name]/       # One folder per implementation
    ├── plan.md                  # Implementation plan with phase tracking
    └── [other artifacts]        # Optional: test reports, analysis docs
```

## How It Works

### For Copilot Agents

**Starting a New Implementation:**
1. Check if `/implementation/[name]/plan.md` exists
2. If not, create it based on the handover document
3. Include phase breakdown with status markers

**Continuing an Existing Implementation:**
1. Check for `/implementation/[name]/plan.md`
2. Read the plan to understand current status
3. Identify the current phase (✅ complete, 🚧 in progress, ⏳ pending)
4. Continue from the current phase
5. Update plan as work progresses

**Commands to Check:**
```bash
# List all in-flight implementations
ls -la /implementation/

# Find a specific implementation plan
cat /implementation/[name]/plan.md
```

### For Developers

**Resuming Work:**
When you want to continue a paused implementation:
1. Reopen or comment on the original issue
2. Say "continue the implementation" or "@copilot please continue"
3. Copilot will check `/implementation/[name]/plan.md` and resume from the current phase

**No Special Setup Required:**
- The plan document contains all the context needed
- References to handover documents and completed work
- Clear instructions for each phase
- Easy to pick up where you left off

## Plan Document Format

Each `plan.md` includes:

- **Implementation Status**: Current phase with status marker
- **Original Handover**: Link to research handover document
- **Phase Breakdown**: Each phase with:
  - Status (✅ complete, 🚧 in progress, ⏳ pending)
  - Objectives and deliverables
  - Files modified/created
  - Success criteria
  - Specific instructions for execution
- **How to Continue**: Clear instructions for resuming work
- **References**: Links to handover docs, migration guides, benchmarks, etc.

## Benefits

1. **Easy Resume**: Clear status tracking makes it easy to pick up where you left off
2. **Context Preservation**: All context in one document, linked to supporting docs
3. **Phased PRs**: Smaller, more reviewable PRs instead of massive changes
4. **Progress Visibility**: Clear phase markers show what's done and what's next
5. **No Lost Work**: Implementation context persists across sessions

## Example Usage

**Scenario**: Large test migration (4-8 hours)

1. **Phase 1 PR**: Mark blocks obsolete, create migration guide (~1 hour)
   - Status: ✅ Complete in plan.md
   
2. **Close PR, reopen issue later**

3. **Phase 2 PR**: User says "continue the implementation"
   - Copilot reads `/implementation/plain-blocks-consolidation/plan.md`
   - Sees Phase 1 complete, Phase 2 ready
   - Continues with test migration

4. **Phase 3 PR**: After Phase 2 merges
   - User says "continue"
   - Copilot sees Phase 2 complete, proceeds to Phase 3
   - Removes obsolete code

## Related Documentation

- Implementation workflow: `/.github/copilot-instructions.md`
- Research workflow: `/research/RESEARCH_WORKFLOW.md`
- Workflow improvements: `/.github/workflow-improvements.md`
