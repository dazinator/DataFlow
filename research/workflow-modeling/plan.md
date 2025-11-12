# Process Modeling Plan

## Current Work

**Issue**: #391 - Update Triage Duty to handle unlabeled work items  
**Started**: 2025-11-12  
**Status**: ✅ **COMPLETE**

### Objective

Update the Triage Duty bulk triage procedure to handle work items without workflow labels (unlabeled items needing initial triage).

### Problem

The current bulk triage procedure only queries items with `workflow:triage` label, missing newly created issues without any workflow labels. This was discovered when issue #377 was missed during bulk triage in #388.

### Solution Implemented

1. ✅ Added `query_unlabeled_work_items` semantic operation to kernel layer (13th operation)
2. ✅ Updated Triage Duty bulk triage procedure to query both labeled and unlabeled items
3. ✅ Added deduplication and filtering logic
4. ✅ Updated semantic language specification
5. ✅ Implemented GitHub driver mapping with two variants (basic and efficient)
6. ✅ Added usage examples

### Test Results

**All scenarios PASSED (3/3)** ✅ - 100% pass rate

1. Scenario 001 - Bulk triage with unlabeled items: PASS
2. Scenario 002 - Mixed labeled/unlabeled items: PASS
3. Scenario 003 - Edge case - only unlabeled items: PASS

**Critical Finding**: Scenario 003 proves the old procedure would have missed ALL items when the triage queue contains only unlabeled work items.

### Leak Detection

- ✅ Kernel leak detection: PASSED (zero leaks)
- ✅ Dependency leak detection: PASSED (zero leaks)

### Deliverables Completed

1. ✅ Semantic language specification updated
2. ✅ Kernel README updated (12 → 13 operations)
3. ✅ GitHub driver operations.md updated with new operation
4. ✅ GitHub driver examples.md updated with usage example
5. ✅ Triage Duty TRIAGE_DUTY.md updated (semantic ops list + bulk procedure)
6. ✅ Test scenarios created (3 scenarios in `.team/duties/tests/triage/`)
7. ✅ Graph updated (version 1.5, post-phase 5 update)
8. ✅ History updated

### Success Criteria Met

- [x] New semantic operation added to kernel
- [x] GitHub driver implementation complete
- [x] Triage Duty procedure updated
- [x] All test scenarios pass (100% pass rate)
- [x] Zero kernel leaks
- [x] Zero dependency leaks
- [x] Graph updated
- [x] History updated
- [ ] Self-improvement evaluation completed

### Completion

Issue #391 successfully completed. Triage Duty now finds all work items needing triage, including unlabeled items.

---

## How to Start New Work

When a new workflow improvement issue is assigned:

1. Update this section with issue details
2. Create test scenarios in `/scenarios/[workflow-name]/`
3. Execute tabletop simulations
4. Document results and refine workflows
5. Archive this plan when complete

## Recent Completion

**Last Completed**: 2025-11-11 - Parameter Extraction and Template Simplification
**See Archive**: `/research/workflow-modeling/archive/2025-11-11-parameter-extraction-template-simplification.md`

## Archive

Previous work can be found in `/research/workflow-modeling/archive/`:
- `2025-11-11-parameter-extraction-template-simplification.md` - Parameter Extraction and Template Simplification
- `2025-11-10-bulk-process-template-update.md` - Process Modeling Archived Plan - Bulk Process Modeling Template Update
- `2025-11-10-bulk-triage-improvements.md` - Process Modeling Archived Plan - Bulk Triage Workflow Improvements
- `2025-11-10-bulk-triage-process.md` - Process Modeling Archived Plan - Bulk Triage Process Improvement
- `2025-11-10-condense-workflow-references.md` - Process Modeling Archived Plan - Condense Workflow References
- `2025-11-10-feedback-issues-migration.md` - Process Modeling Archived Plan
- `2025-11-10-multi-phase-issue-management.md` - Process Modeling Archived Plan - Multi-Phase Issue Management
- `2025-11-10-workflow-file-ownership.md` - Process Modeling Archived Plan - Workflow File Ownership Clarification
- `2025-11-10-workflow-template-separation.md` - Process Modeling Archived Plan - Workflow Template Separation
- `2025-11-10-bulk-processing-session.md` - Bulk Processing Session 2025-11-10
- `2025-11-10-bulk-processing-smart-mode.md` - Process Modeling Archived Plan - Bulk Processing Smart Mode
- `README.md` - Process Modeling Work Archive
- `2025-11-08-history-format-enhancement.md` - Process Modeling Plan - History Format Enhancement
- `2025-11-08-history-table-format.md` - Process Modeling Plan - History Table Format Enhancement
- `2025-11-08-implementation-improvements.md` - Process Modeling Plan - Implementation Workflow Improvements
- `2025-11-08-multi-item-processing.md` - Process Modeling Plan - Multi-Item Backlog Processing
- `2025-11-08-product-backlog-system.md` - Process Modeling Plan
- `2025-11-08-product-prioritization.md` - Process Modeling Plan
- `2025-11-08-smart-mode-bulk-improvements.md` - Process Modeling Archived Plan - Smart Mode Bulk Improvements
- `2025-11-08-template-simplification.md` - Process Modeling Plan - Template Simplification
- `2025-11-08-workflow-documentation-improvements.md` - Process Modeling Archived Plan
- `2025-11-08-workflow-improvements-template.md` - Process Modeling Plan
- `2025-11-09-backlog-sync-exploration.md` - Process Modeling Archived Plan - Backlog-to-GitHub Issues Sync
- `2025-11-09-smart-mode-bulk-improvements.md` - Process Modeling Archived Plan - Smart Mode Bulk Improvements
- `2025-11-09-tech-debt-workflow-modernization.md` - Process Modeling Archived Plan - Tech Debt Workflow Modernization
- `2025-11-09-workflow-topology-design.md` - Process Modeling Archived Plan - Centralized Workflow Topology System
- `2025-11-09-workflow-topology-implementation.md` - Process Modeling Archived Plan - Workflow Topology System Implementation
- `2025-11-08-backlog-driven-mode.md` - Process Modeling Plan - Backlog-Driven Mode Enhancement
- `2025-11-08-bulk-improvements-smart-mode.md` - Process Modeling Plan - Bulk Improvements (Smart Mode)
- `2025-11-08-documentation-deliverables-guidance.md` - Archived Plan: Documentation Deliverables and Example Tests Guidance

