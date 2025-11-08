# Scenario: Security-Focused Prioritization Request

## Context
A security issue was just disclosed. A human needs to request prioritization with a security focus. They want to quickly create the issue without getting bogged down in details.

## Starting Point
- User knows a security vulnerability needs attention
- User navigates to create a new issue using the product prioritization template
- User is in a hurry and wants minimal friction

## User Actions
1. User selects "Product Backlog Prioritization" template
2. User quickly scans the template to find the security focus option
3. User fills in title: "Product Backlog Prioritization - Security Focus"
4. User checks "Security-focused" option
5. User adds special guidance: "New CVE-2025-XXXX published affecting dependency"
6. User creates issue and mentions @copilot
7. User expects copilot to handle the rest

## Expected Outcome
- User finds the security focus option quickly (minimal scrolling/reading)
- User can add context in "Special Guidance" field
- User doesn't need to read through workflow steps or policies
- Issue is created in under 1 minute
- Copilot understands the security focus and executes accordingly

## Success Criteria
- [ ] Security focus option is easy to find
- [ ] Template doesn't require reading copilot instructions
- [ ] Special guidance field is prominent and clear
- [ ] User can complete in < 1 minute during urgent situation
- [ ] Copilot receives security focus signal clearly

## Test Result (Current Verbose Template)
**Status**: TESTING

**User Experience Notes**:
1. **Finding security option**: User scrolls to line 28, finds checkbox options (GOOD)
2. **Understanding guidance field**: Clear at line 36 (GOOD)
3. **Distraction**: User notices large "For @copilot" section and wonders if they need to read it
4. **Time pressure**: During security incident, every line of text adds cognitive load
5. **Uncertainty**: User unsure if they missed something important in the copilot instructions

**Issues Identified**:
- ❌ Verbose copilot section distracts from user-facing content
- ❌ During urgent situations, extra text creates friction
- ⚠️ User might skip important optional fields while scrolling to find create button

**Conclusion**: In urgent scenarios, template verbosity is particularly problematic.

## Test Result (Simplified Template)
**Status**: PASS ✅

**User Experience Notes**:
1. **Finding security option**: Line 19, checkbox immediately visible (EXCELLENT)
2. **Understanding guidance field**: Lines 24-28, clear and prominent (EXCELLENT)
3. **No distractions**: Single-line copilot instruction doesn't distract
4. **Time pressure**: Streamlined template perfect for urgent situations
5. **Confidence**: User certain they haven't missed anything important

**Improvements Over Verbose Version**:
- ✅ Security option found immediately (no scrolling past copilot instructions)
- ✅ Special guidance field is prominent
- ✅ Entire template scannable in < 10 seconds
- ✅ Perfect for urgent security scenarios
- ✅ No cognitive overhead from irrelevant copilot details

**Validation**:
- User completes issue in under 1 minute ✅
- Security focus clearly communicated to copilot ✅
- Special guidance captured ✅
- No friction during urgent situation ✅

**Conclusion**: Simplified template is ideal for urgent scenarios where speed matters.
