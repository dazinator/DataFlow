# Scenario 012: Configuration Options Design - Baseline (No Guidance)

## Context
Testing current Process Modeling Workflow when agent needs to decide whether to make thresholds/limits configurable or fixed.

## Starting Point
- Copilot agent has chosen MAX_ITEMS=5 and MAX_LINES=500 defaults
- **QUESTION**: Should users be able to override these?
- **QUESTION**: Make them constants, configuration, or per-issue customization?
- Follows Process Modeling Workflow

## Steps to Follow (Current Workflow)
1. Choose threshold values (5 items, 500 lines)
2. **QUESTION**: Fixed or configurable?
3. **NO GUIDANCE** in workflow on:
   - When to make thresholds configurable
   - How to provide sensible defaults
   - Whether to allow per-issue customization
   - How to document defaults for future adjustment

## Expected Outcome (Baseline)
**Arbitrary decisions about configurability:**
- No framework for deciding fixed vs configurable
- Might over-engineer (make everything configurable)
- Or under-engineer (hardcode values that should be adjustable)
- No guidance on where to document defaults
- Inconsistent approaches across similar features

**Considerations (not documented)**:
- 80% rule: defaults should work for 80% of cases
- Per-issue customization: adds complexity but provides flexibility
- Documentation: where do you document the defaults so they can be adjusted later?

## Success Criteria
- [ ] Workflow has configuration design guidance ❌
- [ ] No framework for fixed vs configurable decision ❌
- [ ] No guidance on defaults documentation ❌
- [ ] Inconsistent configurability decisions ❌

## Test Result
**Status**: BASELINE (showing current gap)

**Notes**:
Current workflow doesn't guide agents on configuration design decisions. This creates:

**Decision Points Without Guidance**:
1. **Fixed vs Configurable**: Should thresholds be constants or configurable?
2. **Configuration Level**: Global constants, environment variables, or per-issue parameters?
3. **Defaults Strategy**: How to choose values that work for most cases?
4. **Documentation**: Where to document defaults for future adjustment?

**Actual Implementation Decisions**:
- Made thresholds configurable via issue description (per-issue customization)
- Provided sensible defaults (5 items, 500 lines)
- Defaults work for 80%+ of cases
- Documented in workflow for easy future adjustment

**But These Decisions Were Made Without Framework**:
- Had to infer that optional customization was appropriate
- No guidance on where to document the defaults
- No pattern for "sensible defaults" strategy

This validates need for "Configuration Options Design" guidance, covering:
- When to make configurable (affects multiple use cases differently)
- When to keep fixed (single best value for all cases)
- How to choose defaults (80% rule)
- Where to document (workflow, code comments, README)
- Per-issue vs global configuration tradeoffs
