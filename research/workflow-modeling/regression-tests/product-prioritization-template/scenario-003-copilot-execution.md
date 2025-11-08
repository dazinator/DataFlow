# Scenario: Copilot Execution Path

## Context
A copilot agent receives a product prioritization issue created by a human. The copilot needs to understand what to do and execute the workflow correctly.

## Starting Point
- Issue has been created using the product prioritization template
- Copilot is mentioned/assigned to the issue
- Copilot reads copilot-instructions.md first (as designed)

## Copilot Actions
1. Copilot reads `.github/copilot-instructions.md` and navigates to workflow
2. Copilot sees "Product Prioritization Workflow" mentioned
3. Copilot opens `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md`
4. Copilot reads the complete workflow documentation (614 lines)
5. Copilot reads the issue template for specific context/guidance
6. Copilot executes the workflow steps

## Expected Outcome
- Copilot should find clear pointer to PRODUCT_PRIORITIZATION_WORKFLOW.md
- Copilot should extract any specific focus/guidance from the issue
- Copilot should NOT need detailed execution steps in the template (workflow has them)
- Copilot should successfully execute prioritization with minimal template content

## Success Criteria
- [ ] Workflow reference is clear in template
- [ ] Copilot can extract focus area (standard/security/tech debt/user requests)
- [ ] Copilot can extract special guidance if provided
- [ ] Template doesn't duplicate workflow content unnecessarily
- [ ] Copilot successfully executes without confusion

## Test Result (Current Verbose Template)
**Status**: TESTING

**Copilot Execution Notes**:
1. **Workflow reference**: Lines 9-13 clearly point to workflow doc ✅
2. **Execution steps**: Lines 44-52 provide detailed steps
3. **Policy compliance**: Lines 54-59 list policy requirements
4. **Success criteria**: Lines 61-67 list success criteria

**Analysis**:
- The execution steps (lines 44-52) duplicate content from PRODUCT_PRIORITIZATION_WORKFLOW.md Step 1-6
- The policy compliance (lines 54-59) duplicates the "Prioritization Policy" section of the workflow
- The success criteria (lines 61-67) duplicate the "Success Criteria" section of the workflow

**Questions**:
- Does copilot need these duplicated sections?
- Would copilot execute correctly with just: "Read workflow, execute prioritization, note any focus/guidance"?

**Hypothesis**: The duplicate content is NOT necessary because:
1. Copilot is already instructed to read the workflow (line 13)
2. The workflow contains ALL execution details
3. Duplication creates maintenance burden (two places to update)
4. Other templates (research.md, implementation.md) successfully use workflow references without duplication

**Conclusion**: Template verbosity does NOT help copilot execution and creates maintenance overhead.

## Test Result (Simplified Template)
**Status**: PASS ✅

**Copilot Execution Notes**:
1. **Workflow reference**: Line 13 clearly points to workflow doc ✅
2. **Focus extraction**: Lines 19-22 provide clear focus area options ✅
3. **Special guidance**: Lines 24-28 allow human to provide context ✅
4. **No duplication**: Execution steps, policy, and success criteria only in workflow ✅

**Execution Test**:
Following the simplified template:
1. Copilot reads copilot-instructions.md ✅
2. Copilot sees workflow reference (line 13) ✅
3. Copilot opens `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md` ✅
4. Copilot executes Step 1: Understands request, notes any focus/guidance from lines 19-28 ✅
5. Copilot executes Step 2-6: Follows workflow (all details in workflow doc) ✅
6. Copilot completes successfully ✅

**Comparison with Verbose Template**:
- **Verbose**: Execution steps in template + in workflow (duplication)
- **Simplified**: Execution steps only in workflow (single source of truth)

**Advantages of Simplified Approach**:
- ✅ Single source of truth (workflow doc)
- ✅ Easier maintenance (update workflow once, not template + workflow)
- ✅ No synchronization issues between template and workflow
- ✅ Template focuses on user input, workflow focuses on execution
- ✅ Follows pattern of other templates (research.md, implementation.md)

**Validation Against Other Templates**:
Compared to research.md and implementation.md templates:
- Both reference workflow docs without duplicating execution steps ✅
- Both provide minimal copilot instructions ✅
- Both focus on user-provided context ✅
- Simplified product-prioritization.md follows same pattern ✅

**Conclusion**: Simplified template provides all necessary information for copilot execution without duplication. Copilot executes workflow correctly using workflow doc as source of truth.
