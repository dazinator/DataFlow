# Scenario 008: External Dependencies - Improved (With Guidance)

## Context
Testing improved workflow when handover involves sample code with external service requirements, with guidance for both implementation and handover creation.

## Starting Point
- Copilot agent implementing sample application changes
- Sample requires OTLP endpoint at localhost:4317
- Handover NOW documents this requirement (due to new guidance)
- Agent follows improved Implementation Workflow

## Steps to Follow (Improved Workflow - Implementation Side)
1. Read handover
2. **NEW**: Handover explicitly states:
   ```
   External Dependencies:
   - OTLP endpoint at localhost:4317 (optional for validation)
   - Validation approach: Build-only sufficient (sample/dev-only dependency)
   - Runtime testing optional since external service may not be available
   ```
3. Implement changes
4. Build succeeds
5. Check Step 7 validation guidance
6. See it's sample code with documented external dependency
7. Use build-only validation per handover guidance
8. No confusion about connection failures
9. Complete implementation efficiently

## Steps to Follow (Improved Workflow - Handover Creation Side)
Research team creating handover:
1. Review their sample code changes
2. **NEW**: Check Step 0 guidance for handover creators
3. See reminder: "Document external dependencies"
4. Add section to handover:
   - List external services required
   - State whether runtime validation required or optional
   - Provide alternative validation if service optional

## Expected Outcome (Improved)
**Clear expectations, no wasted time:**
- Implementation agent knows external dependency is expected
- No confusion about connection failures
- Clear on validation approach (build-only acceptable)
- Research team reminded to document dependencies
- Consistent handover quality

## Success Criteria
- [x] Workflow reminds handover creators to document external deps ✅
- [x] Implementation agents see the documentation ✅
- [x] Guidance on when runtime validation required vs optional ✅
- [x] No time wasted on expected infrastructure issues ✅

## Test Result
**Status**: PASS (with improved guidance)

**Notes**:
With "External Dependency Documentation" guidance added:

**Added to RESEARCH_WORKFLOW (handover creation):**
```markdown
### External Dependencies Documentation

If your code/sample requires external services:

**Required in Handover:**
- [ ] List all external dependencies (databases, endpoints, APIs, etc.)
- [ ] Document connection details or requirements
- [ ] State whether runtime validation is required or optional
- [ ] Provide alternative validation approach if service optional

**Example:**
```
## External Dependencies

This sample requires:
- OTLP endpoint at localhost:4317 (OpenTelemetry collector)
- Redis at localhost:6379 (optional, for caching demo)

**Validation Approach:**
- Build verification sufficient (sample/dev-only code)
- Runtime testing optional since external services may not be available
- If services available, verify telemetry export works
```
```

**Added to IMPLEMENTATION_WORKFLOW Step 0 (Handover Review):**
- New checkbox: "If sample code, are external dependencies documented?"

**Added to IMPLEMENTATION_WORKFLOW Step 7:**
- Reference to check handover for external dependency documentation
- If external service unavailable and documented as optional → Skip runtime validation

Benefits:
- ✅ No confusion from expected infrastructure failures
- ✅ Clear validation expectations
- ✅ Consistent handover quality
- ✅ Time saved troubleshooting non-issues
