# Scenario 015: Configuration Options Design - Improved (With Guidance)

## Context
Testing improved Process Modeling Workflow with "Configuration Options Design" guidance when deciding whether to make thresholds configurable.

## Starting Point
- Copilot agent has chosen MAX_ITEMS=5 and MAX_LINES=500 defaults
- Needs to decide: fixed, configurable, or per-issue customization?
- Follows improved Process Modeling Workflow

## Steps to Follow (Improved Workflow)
1. Choose threshold values (5 items, 500 lines)
2. **NEW**: Check "Configuration Options Design" guidance
3. Follow decision framework:
   - **When to make configurable**: Use cases vary significantly
   - **When to keep fixed**: Single best value for all cases
   - **Default strategy**: Choose values that work for 80% of cases
4. Evaluate for smart mode:
   - Most use cases: 5 items and 500 lines work well
   - Some edge cases: User might want more/fewer items
   - Decision: Make configurable with sensible defaults (80% won't override)
5. Choose configuration level:
   - Global constants: Too rigid
   - Environment variables: Overkill for this
   - Per-issue parameters: ✅ Flexible, with good defaults
6. Document defaults in workflow: "Default MAX_ITEMS=5, MAX_LINES=500"
7. Implementation: Optional parameters in issue template with defaults shown

## Expected Outcome (Improved)
**Well-reasoned configurability decisions:**
- Clear framework for fixed vs configurable
- Default strategy documented (80% rule)
- Configuration level chosen appropriately
- Defaults documented for easy future adjustment
- Consistent approach across features

## Success Criteria
- [x] Workflow has configuration design framework ✅
- [x] Clear guidance on fixed vs configurable ✅
- [x] Default strategy documented (80% rule) ✅
- [x] Documentation location specified ✅

## Test Result
**Status**: PASS (with improved guidance)

**Notes**:
With "Configuration Options Design" guidance added:

**Section Added:**
```markdown
### Configuration Options Design Guidance

When designing features with thresholds or limits, decide on configurability:

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

**Example Decision: Smart Mode Thresholds**
- Configurable: Yes (use cases vary)
- Level: Per-issue parameters (optional, with defaults)
- Defaults: MAX_ITEMS=5, MAX_LINES=500
- Rationale: Works for 80%+ cases, power users can override
- Documentation: In Process Modeling Workflow
```

Benefits:
- ✅ Clear framework for configuration decisions
- ✅ 80% rule guides default selection
- ✅ Appropriate configuration level chosen
- ✅ Defaults documented for future adjustment
- ✅ Consistent approach across similar features
