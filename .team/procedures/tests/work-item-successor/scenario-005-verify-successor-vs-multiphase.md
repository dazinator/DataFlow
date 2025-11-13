# Scenario: Successor vs Multi-Phase Decision

## Context
Implementation duty working on feature implementation. Scope is larger than expected. Need to decide whether to use successor pattern or switch to multi-phase pattern. This tests understanding of when to use each pattern.

## Starting Point
- **Current work item**: #500 (Implement Notification System)
- **Status**: Implementation started, discovered scope is large
- **Current PR**: Has basic email notifications working
- **Remaining scope**: SMS, push notifications, notification preferences, delivery tracking
- **Agent**: Implementation duty

## Steps to Follow

1. **Assess current situation**
   ```python
   current = get_work_item_details(work_item_id="500")
   
   # Current state:
   # - Implementation ALREADY in progress
   # - PR exists with partial work (email notifications)
   # - PR will close this work item when merged
   # - Remaining scope identified: SMS, push, preferences, tracking
   ```

2. **Decision: Can we use multi-phase pattern?**
   ```
   Question: Can we keep #500 open and make it parent of phases?
   Answer: NO - PR will close #500 when merged (Copilot PR behavior)
   
   Question: Can we split PR into multiple smaller PRs before merging?
   Answer: NO - current PR has valuable work ready to merge
   
   Question: Do we know all remaining phases upfront?
   Answer: Partially - we know SMS and push, but preferences/tracking might reveal more
   
   Conclusion: CANNOT use multi-phase pattern - use successor pattern
   ```

3. **Use successor pattern (correct choice)**
   ```python
   # Create successor for SMS notifications
   successor_id = create_work_item(
       type="implementation",
       title="Implement Notification System - SMS Notifications",
       description="""
   **Predecessor**: #500 (read for full context)
   
   ## Continuation from Predecessor
   
   This continues from #500, which completed:
   - Email notification infrastructure
   - Basic notification sending
   - Email template system
   
   ## Remaining Scope (This Successor)
   
   ### Requirements
   - [ ] SMS provider integration
   - [ ] SMS template system
   - [ ] SMS delivery tracking
   - [ ] Tests for SMS notifications
   
   ## Future Work (May Need Additional Successors)
   - Push notifications (might be successor to this)
   - Notification preferences (might be successor to this)
   
   [Rest of description]
   """,
       duty="implementation"
   )
   ```

4. **Update predecessor**
   ```python
   add_work_item_comment(
       work_item_id="500",
       text=f"""
   ✅ **Successor Created**
   
   Remaining work moved to successor: #{successor_number}
   
   **Completed in this PR**: Email notifications
   **Deferred to successor**: SMS notifications
   **Future work**: Push notifications, preferences (may need additional successors)
   
   **Why successor pattern**: Implementation in progress with PR ready. Cannot keep 
   #500 open to convert to multi-phase parent. Using successor pattern for 
   iterative scope discovery.
   """
   )
   ```

5. **Later, when working on successor, may create another successor**
   ```
   Pattern continues: #500 → #501 (SMS) → #502 (Push) → ...
   Lazy discovery of scope boundaries as work progresses
   ```

## Expected Outcome

**Success if**:
- Agent correctly identifies this is NOT a multi-phase situation because:
  - Implementation already in progress
  - PR exists and will close current issue
  - Cannot keep current issue open
  - Don't know all phases upfront (discovering boundaries)
- Agent chooses successor pattern (correct choice)
- Agent creates successor with clear scope
- Agent notes that additional successors may be needed
- Agent understands successor pattern allows lazy scope discovery
- Agent documents why successor pattern chosen over multi-phase

**Failure if**:
- Agent tries to convert #500 to multi-phase parent (wrong - PR will close it)
- Agent tries to create all remaining work items upfront (wrong - don't know all boundaries yet)
- Agent doesn't recognize this is iterative scope discovery scenario

**Key Learning**:
- Multi-phase: BEFORE implementation, know phases upfront, eager planning
- Successor: DURING implementation, discover boundaries, lazy planning
- Cannot convert active PR issue to multi-phase parent
- Successor pattern enables adaptive scoping

## Actual Outcome
[Fill in during test execution - PASS/FAIL with notes]
