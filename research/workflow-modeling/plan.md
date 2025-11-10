# Process Modeling Plan

## Current Work

**Issue**: Post-Migration Workflow Cleanup
**Started**: 2025-11-09
**Status**: Complete ✅

### Workflows Being Updated
- [x] All workflows (removing bash script references) ✅
- [x] Copilot Instructions (removing /product/backlog references) ✅
- [x] Issue Templates (adding default labels, removing /product/backlog) ✅

### Proposed Changes
Cleanup after GitHub issue migration (PR #220):
1. ✅ Replace bash script references with MCP tool guidance
2. ✅ Evaluate .team/scripts folder (kept for manual/CI use)
3. ✅ Relocate .team/prompts/adr (location is appropriate)
4. ✅ Remove all /product/backlog references (0 references remaining)
5. ✅ Update issue templates with default workflow labels
6. ✅ Add bulk mode documentation with sub-issue patterns

### Testing Status
- [x] Scenarios created ✅
- [x] Initial tabletop simulation complete ✅
- [x] Refinements based on feedback ✅
- [x] All test scenarios PASS ✅
- [x] Verbosity/redundancy check complete ✅

### Test Results Summary
All 5 test scenarios PASS:
- Scenario 001: MCP tools as primary ✅
- Scenario 002: Template labels correct ✅
- Scenario 003: No backlog references ✅
- Scenario 004: ADR location appropriate ✅
- Scenario 005: Bulk mode documented ✅

## Work Complete

**Date Completed**: 2025-11-09

**Changes Made**:
- Updated all 6 workflow files to use MCP tools as primary
- Updated all 5 issue templates with workflow: labels
- Removed 46 references to /product/backlog
- Removed 50 bash script references (replaced with MCP)
- Deleted /product/backlog folder
- Added bulk processing and sub-issue documentation
- All test scenarios validated and passing

**Files Modified**: 16 total (see PR description for complete list)

---

## How to Start New Work

When a new workflow improvement issue is assigned:

1. Update this section with issue details
2. Create test scenarios in `/scenarios/[workflow-name]/`
3. Execute tabletop simulations
4. Document results and refine workflows
5. Archive this plan when complete

## Recent Completion

**Last Completed**: 2025-11-09 - Workflow Topology System Implementation
**See Archive**: `/research/workflow-modeling/archive/2025-11-09-workflow-topology-implementation.md`

## Archive

Previous work can be found in `/research/workflow-modeling/archive/`:
- `2025-11-09-workflow-topology-implementation.md` - Workflow Topology System Implementation
- `2025-11-09-workflow-topology-design.md` - Process Modeling Archived Plan - Centralized Workflow Topology System
- `2025-11-09-smart-mode-bulk-improvements.md` - Process Modeling Archived Plan - Smart Mode Bulk Improvements
- `2025-11-09-tech-debt-workflow-modernization.md` - Process Modeling Archived Plan - Tech Debt Workflow Modernization
- `README.md` - Process Modeling Work Archive
- `2025-11-08-backlog-driven-mode.md` - Process Modeling Plan - Backlog-Driven Mode Enhancement
- `2025-11-08-bulk-improvements-smart-mode.md` - Process Modeling Plan - Bulk Improvements (Smart Mode)
- `2025-11-08-documentation-deliverables-guidance.md` - Archived Plan: Documentation Deliverables and Example Tests Guidance
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

