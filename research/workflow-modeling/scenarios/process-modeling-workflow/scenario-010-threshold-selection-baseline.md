# Scenario 010: Threshold Selection - Baseline (No Guidance)

## Context
Testing current Process Modeling Workflow when agent needs to choose numeric thresholds for a new feature (e.g., max items to process, line limits, timeout values).

## Starting Point
- Copilot agent implementing smart mode for backlog processing
- Needs to choose default values for MAX_ITEMS and MAX_LINES_THRESHOLD
- Follows Process Modeling Workflow

## Steps to Follow (Current Workflow)
1. Design feature with stopping conditions
2. **QUESTION**: What should MAX_ITEMS default be? 3? 5? 10?
3. **QUESTION**: What should MAX_LINES_THRESHOLD be? 100? 500? 1000?
4. **NO GUIDANCE** in workflow on:
   - How to choose sensible defaults
   - What factors to consider
   - How to balance restrictive vs permissive
   - Whether to document rationale

## Expected Outcome (Baseline)
**Uncertainty and arbitrary choices:**
- Agent uses best judgment without framework
- No documentation of why values were chosen
- Risk of choosing too restrictive (frustrating) or too permissive (unwieldy PRs)
- No plan for monitoring and adjusting values
- Different agents might choose very different defaults

## Success Criteria
- [ ] Workflow provides threshold selection guidance ❌
- [ ] No framework for choosing defaults ❌
- [ ] No guidance on documenting rationale ❌
- [ ] Inconsistent approaches likely ❌

## Test Result
**Status**: BASELINE (showing current gap)

**Notes**:
Current Process Modeling Workflow lacks guidance on numeric threshold selection. Agents must use judgment without:
- Framework for choosing defaults
- Considerations for typical use cases
- Balance between restrictive and permissive
- Rationale documentation requirements

This leads to:
- **Arbitrary decisions**: "5 feels about right"
- **No documentation**: Future agents don't know why values were chosen
- **No adjustment plan**: Values set once and never revisited
- **Inconsistency**: Different agents choose different values for similar features

For this specific case (multi-item processing):
- MAX_ITEMS=5 was chosen to align with "typical PR size" best practices
- MAX_LINES=500 chosen to keep PRs reviewable
- But these rationales weren't documented in the workflow

Validates need for "Threshold Selection Guidance".
