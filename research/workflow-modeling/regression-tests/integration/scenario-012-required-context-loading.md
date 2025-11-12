# Integration Test Scenario 012: Required Context Loading

**Purpose**: Verify orchestration Required Context section properly references kernel and procedures  
**Created**: 2025-11-12  
**Status**: PENDING

---

## Context

Agent loads orchestration and must understand the layered architecture from Required Context.

---

## Starting State

**Work Item #1012**:
- **Title**: "Any work item"
- **Labels**: `workflow:implementation`
- **Description**: "Testing Required Context loading"
- **Status**: Open

---

## Procedure to Follow

1. Load [Orchestration](../../../../.github/copilot-instructions.md)
2. Read **Required Context** section
3. Verify references to:
   - Layer 3 (Kernel)
   - Layer 1 (Global Procedures)
   - Layer 0 (Orchestration purpose)
4. Follow references to understand semantic operations
5. Proceed with duty assignment

---

## Expected Behavior

### Required Context Section Present
- ✅ Section titled "Required Context" at top of orchestration
- ✅ Warning: "⚠️ CRITICAL - READ FIRST"
- ✅ Clear explanation of layer structure

### Layer 3: Kernel Reference
- ✅ Link to [Kernel Layer README](../../../../.team/kernel/README.md)
- ✅ Explains purpose: platform abstraction
- ✅ Lists semantic operations
- ✅ Clear when to use: "Never directly - always through semantic operations"

### Layer 1: Global Procedures Reference
- ✅ Link to [Global Procedures README](../../../../.team/procedures/README.md)
- ✅ Lists available procedures:
  - Duty assignment
  - Multi-phase work items
  - Self-improvement
  - Work item creation
  - Handover
  - Comment patterns
- ✅ Clear when to use: "Referenced by duties and orchestration"

### Layer 0: Orchestration Purpose
- ✅ Clear statement of orchestration role:
  1. Load required context
  2. Determine duty
  3. Dispatch to duty
  4. Provide repository-specific guidance

### Navigation Clarity
- ✅ Can find kernel documentation
- ✅ Can find procedure documentation
- ✅ Can find duty documentation
- ✅ Links work and are not broken

---

## Success Criteria

- [ ] Required Context section present and prominent
- [ ] Kernel layer reference clear and accessible
- [ ] Procedures layer reference clear and accessible
- [ ] Orchestration purpose clearly stated
- [ ] All links valid and not broken
- [ ] Agent understands layered architecture from Required Context
- [ ] Agent knows when to use kernel vs procedures vs duties
- [ ] No confusion about abstraction layers

---

## Test Execution

### Layer References Check

**Kernel Layer**:
- [ ] Link present: YES / NO
- [ ] Link works: YES / NO
- [ ] Purpose clear: YES / NO
- [ ] Semantic operations explained: YES / NO

**Procedures Layer**:
- [ ] Link present: YES / NO
- [ ] Link works: YES / NO
- [ ] All 6 procedures listed: YES / NO
- [ ] Purpose clear: YES / NO

**Orchestration Purpose**:
- [ ] 4 responsibilities listed: YES / NO
- [ ] Role as Layer 0 clear: YES / NO

### Observations

**Clarity**:
- How clear is the layered architecture explanation?
- Is there any ambiguity about which layer to use when?

**Completeness**:
- Are all necessary context references present?
- Is anything missing that would help understanding?

**Usability**:
- Can an agent understand the system from this section alone?
- Would you know what to do next after reading Required Context?

---

## Test Result

**Status**: ☐ PASS ☐ FAIL

**Notes**:
