# Product Backlog System - Regression Test Suite

This file contains all passing scenarios for the product backlog system integration. Use these to verify that future changes don't break existing functionality.

## Overview

The product backlog system integrates with:
- Research Workflow (for research handovers)
- Tech Debt Workflow (for tech debt findings)
- Implementation Workflow (for implementation work)
- Product Team (for prioritization)

## How to Run Regression Tests

For each scenario below:
1. Follow the steps exactly as specified
2. Verify the expected outcome is achieved
3. Mark PASS or FAIL
4. If FAIL, document what broke and update workflows

## Scenario 001: Research Team Creates Backlog Item

**Context**: Research team completes research and needs to handover via product backlog.

**Status**: PASS (as of 2025-11-08)

**Test Steps**:
1. Open `.github/copilot-instructions.md` → Find Research Workflow pointer
2. Open `.team/workflows/RESEARCH_WORKFLOW.md` → Find Phase 5
3. Verify Phase 5 has "Create Product Backlog Item" instructions
4. Verify naming convention explained: `research-YYYY-MM-DD-[name].md`
5. Verify template provided inline
6. Verify handover folder creation explained
7. Verify linking options provided

**Expected Outcome**:
- Clear step-by-step guidance for creating backlog item
- Template is comprehensive
- Handover folder structure is clear
- Reference to `/product/README.md` present

**Success Criteria**:
- ✅ No gaps in instructions
- ✅ Naming convention clear
- ✅ Template complete
- ✅ Integration smooth

---

## Scenario 002: Tech Debt Team Checks and Adds to Backlog

**Context**: Tech debt team discovers finding and needs to check/add to backlog.

**Status**: PASS (as of 2025-11-08)

**Test Steps**:
1. Open `.team/workflows/TECH_DEBT_WORKFLOW.md` → Find Phase 1, Step 3
2. Verify references `/product/backlog/` not `/research/backlog/`
3. Verify search examples use correct paths
4. Verify `techdebt-*.md` filtering explained
5. Open Phase 5 → Verify backlog item template
6. Verify naming convention with `techdebt-` prefix explained

**Expected Outcome**:
- All paths point to product backlog
- Search guidance is correct
- Template is comprehensive
- Both selected and non-selected findings handled

**Success Criteria**:
- ✅ Correct paths throughout
- ✅ Source prefix explained
- ✅ Template complete
- ✅ Clear guidance on update vs create

---

## Scenario 003: Implementation Team Selects From Prioritized Backlog

**Context**: Implementation issue specifies "Next from prioritization list".

**Status**: PASS (as of 2025-11-08)

**Test Steps**:
1. Open `.team/workflows/IMPLEMENTATION_WORKFLOW.md` → Find Step 2
2. Verify "Next from Prioritization" section exists
3. Verify instructions to open `/product/prioritization.md`
4. Verify PAUSE requirement is explicit
5. Verify example comment provided
6. Verify WAIT instruction present
7. Verify backlog reading instructions complete
8. Verify status update and archiving instructions

**Expected Outcome**:
- Clear two-path handling (specified vs next priority)
- PAUSE and WAIT are explicit
- Example comment helps guide action
- Archiving process is clear

**Success Criteria**:
- ✅ Prioritization location clear
- ✅ Priority selection logic explained
- ✅ PAUSE requirement explicit
- ✅ Status management complete
- ✅ Archive process detailed

---

## Scenario 004: Product Team Updates Prioritization

**Context**: Product team needs to review and update priorities.

**Status**: PASS (as of 2025-11-08)

**Test Steps**:
1. Open `/product/README.md` → Find "For Product Team" section
2. Verify prioritization process explained
3. Verify format of prioritization file clear
4. Verify priority levels (1-5) explained
5. Verify max 5 items guideline clear
6. Open `/product/prioritization.md` → Verify table format intuitive

**Expected Outcome**:
- Product team guidance is complete
- Prioritization format is simple
- Priority meanings are clear
- Process is straightforward

**Success Criteria**:
- ✅ Instructions clear
- ✅ Format simple and intuitive
- ✅ Priority levels defined
- ✅ No gaps in workflow

---

## Regression Test Results

### Test Run: 2025-11-08 (Initial)

| Scenario | Result | Notes |
|----------|--------|-------|
| 001 - Research | PASS | All issues from first test resolved |
| 002 - Tech Debt | PASS | All issues from first test resolved |
| 003 - Implementation | PASS | All issues from first test resolved |
| 004 - Product Team | PASS | No issues found in initial test |

**Overall**: ALL PASS ✅

**Issues Found**: None

**Actions Taken**: None needed

---

## Maintenance Notes

- Run these tests when updating any workflow that touches product backlog
- Run these tests when updating product backlog system itself
- Run these tests quarterly to ensure no drift
- Add new scenarios as product backlog system evolves
