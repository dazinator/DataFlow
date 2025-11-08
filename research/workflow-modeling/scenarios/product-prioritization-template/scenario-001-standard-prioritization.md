# Scenario: Standard Prioritization Request

## Context
A human wants to request a standard monthly product backlog prioritization. They navigate to GitHub Issues → New Issue and select the "Product Backlog Prioritization" template.

## Starting Point
- User is at the GitHub issue template selection page
- They select "Product Backlog Prioritization" template
- The template content appears in the issue creation form

## User Actions
1. User reads the template to understand what information is needed
2. User fills in the title: "Product Backlog Prioritization - November 2025"
3. User reviews the template sections to decide what to fill in
4. User leaves "Prioritization Focus" as "Standard" (default)
5. User leaves "Special Guidance" blank (no special considerations)
6. User creates the issue
7. User mentions @copilot to trigger the workflow

## Expected Outcome
- User should easily understand what to do with minimal cognitive load
- Template should be clear about what's required vs optional
- User should not be overwhelmed with workflow details or copilot instructions
- Issue creation should feel lightweight and frictionless
- Copilot should have sufficient context to execute prioritization

## Success Criteria
- [ ] Template is concise and easy to scan
- [ ] Required information is clear
- [ ] Optional fields are clearly marked
- [ ] No duplication of workflow documentation
- [ ] User can create issue in < 2 minutes
- [ ] Copilot receives adequate instruction to execute workflow

## Test Result (Current Verbose Template)
**Status**: TESTING

**User Experience Notes**:
1. **Reading the template**: User sees large "For @copilot" section with 7 execution steps
2. **Understanding requirements**: User sees "Prioritization Focus" and "Special Guidance" sections (good)
3. **Cognitive load**: User must read through detailed execution steps, policy compliance checklist, and success criteria
4. **Time to complete**: Estimated 3-5 minutes due to reading through copilot instructions
5. **Confusion points**: 
   - Is the "Execution Steps" section for the user or copilot?
   - Does user need to verify the "Success Criteria" checkboxes?
   - Why is there so much detail if copilot will read the workflow doc anyway?

**Issues Identified**:
- ❌ Verbose: Contains ~60 lines when ~25 would suffice
- ❌ Redundant: Execution steps duplicate PRODUCT_PRIORITIZATION_WORKFLOW.md
- ❌ Confusing: Success criteria section makes users wonder if they need to check these
- ❌ High friction: Too much text to scan before understanding what's needed

**Conclusion**: Template creates unnecessary friction for a simple request.

## Test Result (Simplified Template)
**Status**: PASS ✅

**User Experience Notes**:
1. **Reading the template**: User sees concise 30-line template (vs 67 lines)
2. **Understanding requirements**: Clear sections: Request, Focus (Optional), Special Guidance (Optional)
3. **Cognitive load**: Minimal - only user-relevant content visible
4. **Time to complete**: Estimated < 1 minute
5. **Clarity**: 
   - Copilot instruction is single line pointing to workflow (line 13)
   - Focus options are clear checkboxes (lines 19-22)
   - Special guidance field is self-explanatory (lines 24-28)

**Improvements Over Verbose Version**:
- ✅ Reduced from 67 lines to 30 lines (55% reduction)
- ✅ Removed duplicate execution steps
- ✅ Removed duplicate policy compliance checklist
- ✅ Removed duplicate success criteria
- ✅ Kept only essential user-facing content
- ✅ Copilot instruction is single clear reference to workflow

**Validation**:
- User can scan entire template in seconds
- All necessary information is present (focus area, special guidance)
- No confusion about what user vs copilot should do
- Template is frictionless for standard use case

**Conclusion**: Simplified template achieves goal of reducing friction while maintaining effectiveness.
