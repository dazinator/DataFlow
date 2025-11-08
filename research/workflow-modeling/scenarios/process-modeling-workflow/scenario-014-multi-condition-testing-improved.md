# Scenario 014: Multi-Condition Testing - Improved (With Guidance)

## Context
Testing improved Process Modeling Workflow with "Multi-Condition Feature Testing" pattern when testing features with multiple stopping conditions.

## Starting Point
- Copilot agent has designed smart mode with 3 stopping conditions
- Needs to create test scenarios to validate the feature
- Follows improved Process Modeling Workflow

## Steps to Follow (Improved Workflow)
1. Read "Tabletop Simulation Testing Process" section
2. See new "Multi-Condition Feature Testing" pattern
3. Follow pattern:
   - **Test each stopping condition independently**: 3 scenarios (one per condition)
   - **Test edge cases**: 1-2 scenarios (minimum values, maximum values)
   - **Test regression**: 1 scenario (existing behavior preserved)
   - **Total**: 4-7 scenarios typical for multi-condition features
4. Create structured test plan:
   - Scenario 1: Baseline (existing single-item mode)
   - Scenario 2: Multiple fixed count (existing behavior)
   - Scenario 3: Stop on max items (condition 1)
   - Scenario 4: Stop on max lines (condition 2)
   - Scenario 5: Stop on backlog exhausted (condition 3)
   - Scenario 6: Edge case - minimum 1 item
   - Scenario 7: Regression - verify all modes work
5. Confident that 7 scenarios provide comprehensive coverage

## Expected Outcome (Improved)
**Structured, sufficient testing:**
- Clear pattern for multi-condition features
- Know exactly what scenarios to create
- Confidence in coverage sufficiency
- Consistent testing approach
- No uncertainty about "how many is enough"

## Success Criteria
- [x] Workflow has multi-condition testing pattern ✅
- [x] Clear scenario count guidance (4-7 typical) ✅
- [x] Structure for covering all conditions ✅
- [x] Confidence in sufficiency ✅

## Test Result
**Status**: PASS (with improved guidance)

**Notes**:
With "Multi-Condition Feature Testing" pattern added:

**Section Added:**
```markdown
### Multi-Condition Feature Testing Pattern

When testing features with multiple stopping conditions or decision points:

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
```

Benefits:
- ✅ Clear structure eliminates uncertainty
- ✅ Know exactly what to test
- ✅ Confidence in coverage (4-7 is sufficient)
- ✅ Comprehensive without over-testing
