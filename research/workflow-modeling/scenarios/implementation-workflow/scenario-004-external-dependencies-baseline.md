# Scenario 004: External Dependencies in Handover - Baseline (No Guidance)

## Context
Testing current workflow when handover involves sample code that requires external services (databases, OTLP endpoints, etc.) but doesn't document this requirement.

## Starting Point
- Copilot agent implementing sample application changes
- Sample requires OTLP endpoint at localhost:4317
- Handover doesn't mention external dependency
- Agent follows Implementation Workflow

## Steps to Follow (Current Workflow)
1. Read handover (no mention of OTLP requirement)
2. Implement changes
3. Build succeeds
4. Try to run sample: `dotnet run`
5. Connection error: "Failed to connect to localhost:4317"
6. **CONFUSION**: Is this failure related to my changes?
7. **UNCERTAINTY**: Do I need to validate runtime behavior?
8. Spend time investigating the connection error
9. Eventually discover it's external dependency, not code issue

## Expected Outcome (Baseline)
**Wasted time troubleshooting non-issues:**
- Agent confused by external dependency failures
- Time spent investigating whether code changes broke something
- Unclear whether runtime validation required
- No guidance in workflow on external dependencies

**Handover should have said:**
"Sample requires OTLP endpoint. Build-only validation sufficient since this is sample code with dev-only dependency."

## Success Criteria
- [ ] Workflow has no guidance on external dependencies ❌
- [ ] No reminder to document external requirements in handovers ❌
- [ ] Confusion likely when external service unavailable ❌
- [ ] Time wasted troubleshooting non-code-issues ❌

## Test Result
**Status**: BASELINE (showing current gap)

**Notes**:
Neither workflow nor handover creation guidance addresses external dependencies. This leads to:

**For Implementation Agents:**
- Confusion when external services unavailable
- Uncertainty about whether runtime validation required
- Time wasted troubleshooting infrastructure vs code

**For Research Teams Creating Handovers:**
- No reminder to document external dependencies
- No guidance on when runtime validation required vs optional

**Solution Needed:**
Add reminder to handover creation that if sample/code requires external services:
1. Document the requirement explicitly
2. State whether runtime validation required or build-only sufficient
3. Provide alternative validation approach if service optional

This validates the need for "External Dependency Documentation" guidance, both:
- In handover creation process (research workflow)
- In implementation workflow (checking for this in Step 0)
