# Scenario: Copilot Starts Implementation Work with Existing Plan

## Context

A copilot agent is assigned to implement Phase 5 of the Plain Blocks Consolidation. There's an existing multi-phase plan in `/implementation/plan.md` that tracks the overall consolidation effort. The copilot needs to understand where to start and what has already been completed.

## Starting Point

- Copilot receives an implementation issue titled "Plain Blocks Consolidation Phase 5"
- The issue mentions this is part of a larger consolidation effort
- `/implementation/plan.md` exists with status of completed phases 1-4
- Copilot has not worked on this repository before (fresh session)

## Steps to Follow

Copilot would:

1. Read `.github/copilot-instructions.md` Quick Navigation section
2. Identify this is an implementation task
3. Navigate to `.team/prompts/IMPLEMENTATION_WORKFLOW.md`
4. Follow "Quick Start" section
5. Look for guidance on checking for existing implementation plans
6. If guidance exists, check `/implementation/plan.md`
7. Read plan to understand context, completed phases, and current phase requirements
8. Continue with implementation based on plan status

## Expected Outcome

**With the improvement:**
- Quick Start section explicitly says to check `/implementation/plan.md` first
- Copilot finds the plan, reads it, understands context
- Copilot knows: phases 1-4 complete, phase 5 is current, what phase 5 entails
- Copilot can resume work efficiently without confusion
- Copilot understands dependencies and previous decisions

**Without the improvement:**
- No explicit guidance to check for plan
- Copilot might start fresh without understanding context
- Copilot might duplicate work or miss important context
- Copilot might not know previous design decisions
- Time wasted re-discovering what's already documented in plan

## Success Criteria

- [ ] Instructions clearly state to check `/implementation/plan.md` before starting
- [ ] Guidance explains what to do if plan exists (read it first)
- [ ] Guidance explains what to do if no plan exists (create if multi-phase)
- [ ] No ambiguity about when to create a plan
- [ ] Clear positioning in workflow (Step 0 or Quick Start)

## Test Result

**Status**: [x] PASS  [ ] FAIL

**Notes**: 
**BASELINE SIMULATION** (2025-11-08):
- Guidance ALREADY EXISTS in IMPLEMENTATION_WORKFLOW.md Quick Start (lines 9-13)
- Explicit instructions to check `/implementation/plan.md` before starting
- Clear guidance on what to do if exists vs not exists
- This improvement appears to have been already implemented
- **Recommendation**: Mark improvement #1 as complete in backlog

**IMPROVED SIMULATION** (2025-11-08):
- ✅ No changes needed - already implemented
- Guidance remains clear and effective
- **Status**: PASS (already implemented)
