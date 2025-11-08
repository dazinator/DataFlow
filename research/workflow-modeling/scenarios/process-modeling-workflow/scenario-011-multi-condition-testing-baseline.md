# Scenario 011: Multi-Condition Testing - Baseline (No Guidance)

## Context
Testing current Process Modeling Workflow when agent needs to test a feature with multiple stopping conditions (e.g., smart mode that stops on max items OR max lines OR backlog exhausted).

## Starting Point
- Copilot agent has designed smart mode with 3 stopping conditions
- Needs to create test scenarios to validate the feature
- Follows Process Modeling Workflow tabletop simulation guidance

## Steps to Follow (Current Workflow)
1. Read "Tabletop Simulation Testing Process" section
2. See general guidance: "Create realistic scenarios"
3. **QUESTION**: How many scenarios needed?
4. **QUESTION**: Should I test each condition separately?
5. **QUESTION**: What about edge cases?
6. **QUESTION**: Do I need regression tests?
7. Create scenarios based on intuition (created 6 in actual implementation)
8. Wonder: "Is 6 enough? Too many?"

## Expected Outcome (Baseline)
**Uncertainty about test coverage:**
- No specific pattern for multi-condition features
- Agent creates scenarios based on intuition
- Uncertainty about whether coverage is sufficient
- May over-test (waste time) or under-test (miss issues)
- No clear answer to "how many is enough?"

**Current workflow says:**
- "Create realistic scenarios"
- Generic guidance applies to all features
- No specific pattern for features with multiple stopping conditions

## Success Criteria
- [ ] Workflow has pattern for multi-condition testing ❌
- [ ] No guidance on scenario count ❌
- [ ] No structure for covering all conditions ❌
- [ ] Uncertainty about sufficiency ❌

## Test Result
**Status**: BASELINE (showing current gap)

**Notes**:
Current workflow provides general testing guidance but no specific pattern for multi-condition features. This creates:

**Uncertainty**:
- "How many scenarios do I need?" (agent created 6, wondered if more needed)
- "Should I test each condition separately?" (yes, but not documented)
- "Are edge cases required?" (yes, "minimum 1 item" rule was important)

**For Multi-Condition Features (like smart mode)**:
- Ideally test each stopping condition independently
- Test edge cases (minimum/maximum values)
- Include regression test (existing behavior preserved)
- Typically 4-7 scenarios sufficient

**Actual Implementation Created:**
1. Baseline (single-item mode still works)
2. Multiple items with fixed count
3. Smart mode stops on max items
4. Smart mode stops on max lines
5. Smart mode stops on backlog exhausted
6. Edge case: minimum 1 item rule
7. Regression test

This validates need for "Multi-Condition Feature Testing" pattern.
