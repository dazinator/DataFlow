# Archived Documentation

**Archive Date**: 2025-11-12  
**Phase**: Phase 5 - Cleanup & Validation

---

## Overview

This directory contains documentation files that were part of the original `.team/` directory but are no longer needed or have been superseded by newer procedures.

---

## Archived Files

### MULTI_PHASE_ISSUES.md

**Archived**: 2025-11-12  
**Reason**: Superseded by procedure  
**Replacement**: `.team/procedures/multi-phase-work-items.md`

This file provided guidance for managing multi-phase issues. It has been superseded by the `multi-phase-work-items.md` procedure which is part of the global procedures layer and uses semantic operations for platform independence.

**Key Differences**:
- Old file used platform-specific GitHub operations
- New procedure uses semantic operations for cross-platform support
- New procedure integrated into the layered architecture

**Migration**: All references updated to point to `.team/procedures/multi-phase-work-items.md`

### WORKFLOW_MAINTENANCE_GUIDE.md

**Archived**: 2025-11-12  
**Reason**: Outdated - pre-migration workflow system  
**Replacement**: None needed

This file described how to maintain workflow parameters for the old workflow system (pre-Phase 0). It is no longer applicable after the migration to the layered duty-based architecture.

**Status**: No longer referenced anywhere in the codebase

---

## Related Changes

As part of the Phase 5 cleanup, the following files were also moved from `.team/` to more appropriate locations:

### Moved to `/docs/guides/` (DataFlow Library Guides)

- `CENTRAL_PACKAGE_MANAGEMENT.md` → `docs/guides/CENTRAL_PACKAGE_MANAGEMENT.md`
- `NUGET_DEPENDENCY_UPDATES.md` → `docs/guides/NUGET_DEPENDENCY_UPDATES.md`
- `GETTING_STARTED.md` → `docs/guides/GETTING_STARTED.md`

**Reason**: These are DataFlow library development guides, not prompt system procedures.

### Moved to `/docs/` (General Documentation System)

- `DOCUMENTATION_ARTIFACTS.md` → `docs/DOCUMENTATION_ARTIFACTS.md`
- `DOCUMENTATION_SCOPE_GUIDE.md` → `docs/DOCUMENTATION_SCOPE_GUIDE.md`

**Reason**: These describe the general `/docs` structure and should live there.

### Kept in `.team/` (Prompt System Meta-Documentation)

- `DOCUMENT_HYGIENE.md` - Principles for maintainable documentation in the prompt system

**Reason**: Referenced by procedures and duties for documentation standards specific to the prompt architecture.

---

## Impact

After this cleanup, the `.team/` directory contains only:

1. **Kernel Layer** (`.team/kernel/`) - Platform abstraction
2. **Procedures Layer** (`.team/procedures/`) - Global procedures
3. **Duties Layer** (`.team/duties/`) - Specialized duties
4. **Scripts** (`.team/scripts/`) - Validation and graph tools
5. **Graph** (`.team/model-graph.yaml`) - Dependency graph
6. **Meta-Documentation** (`.team/DOCUMENT_HYGIENE.md`) - Prompt system doc standards

This focused scope makes the GitHub Actions validation workflow more efficient - it only monitors changes to files directly related to the prompt architecture model and graph.

---

## References

- **Phase 5 Issue**: #369
- **Parent Issue**: #363 (Multi-phase migration)
- **Validation Report**: `/PHASE_5_VALIDATION_REPORT.md`
- **Migration Guide**: `/MIGRATION_GUIDE.md`
