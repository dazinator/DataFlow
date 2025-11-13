# Scenario: Working on Successor (Reading Predecessor Context)

## Context
Implementation duty assigned to work item #457, which is a successor to #456. Need to read predecessor context to understand decisions made, constraints, and design patterns before starting implementation work.

## Starting Point
- **Current work item**: #457 (Session Refresh & Expiration - successor)
- **Predecessor work item**: #456 (Session Management - predecessor)
- **Parent work item**: #455 (Auth System - parent of both)
- **Status**: Just assigned to #457, haven't started implementation yet
- **Agent**: Implementation duty

## Steps to Follow

1. **Read current (successor) work item details**
   ```python
   successor = get_work_item_details(work_item_id="457")
   
   # Extract predecessor reference from description
   # Should find: "**Predecessor**: #456"
   predecessor_number = "456"  # Parsed from description
   ```

2. **Read predecessor work item for context**
   ```python
   predecessor = get_work_item_details(work_item_id="456")
   
   # Review from predecessor:
   # - What was completed (session storage infrastructure, cleanup)
   # - Key decisions made (Redis, GUID IDs, 15-min cleanup)
   # - Constraints identified (backward compatibility)
   # - Design patterns used (check code/PR)
   # - Test patterns established (check tests)
   ```

3. **Check if successor has parent**
   ```python
   is_child = is_multi_phase(work_item_id="457")
   # Should return: True
   
   parent_id = get_parent_work_item(work_item_id="457")
   # Should return: "455"
   ```

4. **Read parent work item for overall context**
   ```python
   parent = get_work_item_details(work_item_id="455")
   
   # Review from parent:
   # - Overall goal (complete auth system)
   # - All phases in plan
   # - Where this successor fits
   # - What came before (#456)
   # - What might come after
   ```

5. **Align implementation approach with predecessor**
   - Use same Redis setup as #456
   - Follow session ID format from #456 (GUID)
   - Maintain backward compatibility requirement
   - Extend existing test patterns
   - Honor constraint: JWT format for refresh tokens

6. **Start implementation** following predecessor patterns

## Expected Outcome

**Success if**:
- Agent successfully identifies predecessor from successor description
- Agent reads predecessor work item before starting
- Agent understands key decisions from predecessor:
  - Redis storage with 24-hour TTL
  - GUID session IDs
  - 15-minute cleanup interval
  - Backward compatibility requirement
  - JWT format for refresh tokens
- Agent reads parent work item for overall context
- Agent aligns implementation with predecessor approach
- No duplicate work from predecessor
- Consistent design patterns maintained

**Verification**:
- Implementation uses same Redis setup
- Session IDs remain GUID format
- Tests extend existing test patterns
- JWT used for refresh tokens

## Actual Outcome
[Fill in during test execution - PASS/FAIL with notes]
