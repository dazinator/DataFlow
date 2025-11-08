# Process Modeling Plan

## Current Work

**Issue**: Product prioritisation process
**Started**: 2025-11-08
**Status**: Complete

### Workflows Being Updated
- [x] New: Product Prioritization Workflow
- [x] Issue Templates (add product prioritization option)
- [x] Copilot Instructions (reference to new workflow)

### Proposed Changes

Created a new automated product backlog prioritization workflow that:
- Follows a documented prioritization policy
- Handles security vulnerabilities with risk assessment
- Includes tech debt items (aim for at least 1)
- Supports priority override mechanism
- Maintains max 5 selected items
- Can be invoked via GitHub issue or comment

### Testing Status
- [x] Scenarios created (3 scenarios)
- [x] Initial tabletop simulation complete (scenario 001)
- [x] Refinements based on feedback (all issues addressed)
- [x] Regression tests archived (all 3 scenarios)
- [x] Verbosity/redundancy check complete

### Test Results Summary

All three scenarios PASS:
- **Scenario 001**: Basic prioritization - workflow clear after refinements
- **Scenario 002**: Priority override swap - well-specified
- **Scenario 003**: Security risk assessment - comprehensive

**Key Refinements Made:**
1. Explicit security item identification criteria
2. Concrete "quick win" definition
3. Decision framework for standard selection
4. Clear file update process
5. Comprehensive edge case handling section

### Deliverables Completed

1. ✅ **PRODUCT_PRIORITIZATION_WORKFLOW.md** - Complete workflow with:
   - Prioritization policy (MAX_SELECTED_ITEMS = 5)
   - Security risk assessment (core vs. non-core, CVE criticality)
   - Tech debt policy (at least 1 item)
   - Priority override mechanism
   - Step-by-step process
   - Edge case handling
   - Examples and troubleshooting

2. ✅ **Issue Template** - New `product-prioritization.md` template

3. ✅ **Copilot Instructions Updated** - Added product prioritization to quick nav

4. ✅ **Regression Tests** - All 3 scenarios archived

### Archive This Plan

This work is complete. Archive to `/research/workflow-modeling/archive/2025-11-08-product-prioritization.md`.

---

## How to Start New Work

When a new workflow improvement issue is assigned:

1. Update this section with issue details
2. Create test scenarios in `/scenarios/[workflow-name]/`
3. Execute tabletop simulations
4. Document results and refine workflows
5. Archive this plan when complete

## Archive

Previous work can be found in `/research/workflow-modeling/archive/`:
- `2025-11-08-product-backlog-system.md` - Product backlog system integration
- `2025-11-08-product-prioritization.md` - Product prioritization workflow (this work)
