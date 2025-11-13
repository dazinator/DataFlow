# Scenario: Creating Successor with Parent (Happy Path)

## Context
Implementation duty working on Phase 2 of a multi-phase auth system. During implementation, discovers that session management is larger than expected. Current PR has core session storage complete and ready to merge. Needs to defer refresh tokens and expiration to a successor work item.

## Starting Point
- **Current work item**: #456 (Phase 2: Session Management)
- **Parent work item**: #455 (Implement User Authentication System)
- **Current work item is child of parent**: Yes
- **Status**: Implementation in progress, PR ready with partial scope
- **Agent**: Implementation duty

## Steps to Follow

1. **Check for parent work item**
   ```python
   is_child = is_multi_phase(work_item_id="456")
   # Should return: True
   
   parent_id = get_parent_work_item(work_item_id="456")
   # Should return: "455"
   ```

2. **Document scope split decision**
   ```python
   add_work_item_comment(
       work_item_id="456",
       text="""
   ## Scope Split Decision
   
   **Completed in this PR**:
   - [x] Session storage infrastructure
   - [x] Basic session creation/retrieval
   - [x] Session cleanup
   
   **Deferred to successor**:
   - [ ] Refresh token mechanism
   - [ ] Token expiration handling
   - [ ] Session renewal API
   
   **Rationale**: Core storage is complete and testable. Refresh tokens require additional security analysis.
   """
   )
   ```

3. **Create successor as sibling**
   ```python
   parent = get_work_item_details("455")
   current = get_work_item_details("456")
   
   successor_id = create_child_work_item(
       parent_id="455",
       type="implementation",
       title="[Phase 2] User Authentication - Session Refresh & Expiration",
       description="""
   **Predecessor**: #456 (read for full context)
   **Parent**: #455
   
   ## Continuation from Predecessor
   
   This work item continues from #456, which completed:
   - Session storage infrastructure
   - Basic session CRUD operations
   - Session cleanup mechanism
   
   ## Remaining Scope
   
   ### Requirements
   - [ ] Implement refresh token generation
   - [ ] Implement token expiration logic
   - [ ] Add session renewal API endpoint
   - [ ] Handle expired session scenarios
   - [ ] Add tests for refresh and expiration
   
   ## Context from Predecessor
   
   **Key Decisions Made**:
   - Sessions stored in Redis with 24-hour TTL
   - Session IDs use GUID format
   - Cleanup runs every 15 minutes
   
   **Constraints**:
   - Must maintain backward compatibility with existing session API
   - Refresh tokens must use JWT format
   
   ## Success Criteria
   - [ ] Refresh token mechanism working
   - [ ] Expiration handled correctly
   - [ ] All tests passing
   - [ ] Documentation updated
   
   ## References
   - Predecessor: #456
   - Parent: #455
   """,
       duty="implementation"
   )
   ```

4. **Update predecessor work item**
   ```python
   successor_number = get_work_item_details(successor_id)['number']
   
   add_work_item_comment(
       work_item_id="456",
       text=f"""
   ✅ **Successor Created**
   
   Remaining work moved to successor: #{successor_number}
   
   This PR merges core session storage. Refresh tokens and expiration deferred to successor.
   """
   )
   ```

5. **Update parent work item**
   ```python
   add_work_item_comment(
       work_item_id="455",
       text=f"""
   📋 **Work Item Successor Created**
   
   Phase 2 (#456) split into predecessor→successor:
   - **Predecessor**: #456 (core session storage - merging)
   - **Successor**: #{successor_number} (refresh & expiration - next)
   
   **Scope Split**:
   - Predecessor completed: Core session infrastructure
   - Successor will complete: Refresh tokens and expiration handling
   """
   )
   ```

## Expected Outcome

**Success if**:
- Successor work item created as sibling of predecessor (both children of #455)
- Successor has complete description with context from predecessor
- Successor references both predecessor (#456) and parent (#455)
- Predecessor updated with successor reference
- Parent updated to show both predecessor and successor
- Clear scope split documented
- All semantic operations used (no platform-specific code)

**Verification**:
- `list_child_work_items("455")` should return both #456 and successor
- Successor description should reference #456 and #455
- All work items properly linked

## Actual Outcome
[Fill in during test execution - PASS/FAIL with notes]
