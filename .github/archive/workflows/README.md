# Archived Workflow Files

**Archive Date**: 2025-11-12  
**Archived By**: Phase 5 of Multi-Phase Migration (#369)  
**Parent Issue**: #363

---

## Overview

This directory contains the original workflow files that were used before the layered prompt architecture migration. These files have been replaced by the new duty-based system but are preserved here for historical reference.

## Migration Context

Between 2025-11-11 and 2025-11-12, the DataFlow project underwent a major refactoring of its prompt/workflow system, migrating from a flat workflow structure to a **layered, platform-agnostic architecture**.

### What Changed

**Before (Archived Files)**:
- Workflows contained platform-specific operations (GitHub MCP tools)
- Duplicate logic across multiple workflows
- No clear separation between platform and business logic
- Files: `TRIAGE_WORKFLOW.md`, `RESEARCH_WORKFLOW.md`, `IMPLEMENTATION_WORKFLOW.md`, `TECH_DEBT_WORKFLOW.md`, `PRODUCT_PRIORITIZATION_WORKFLOW.md`, `PROCESS_MODELING_WORKFLOW.md`

**After (New Structure)**:
- **Layer 3 (Kernel)**: Platform-specific drivers (`.team/kernel/`)
- **Layer 1 (Procedures)**: Platform-agnostic procedures (`.team/procedures/`)
- **Layer 2 (Duties)**: Specialized responsibilities (`.team/duties/`)
- **Layer 0 (Orchestration)**: Entry point (`.github/copilot-instructions.md`)

### Design Documents

The new architecture is fully documented in:
- [Main Design](/docs/design/prompt-engineering/README.md)
- [Core Concepts](/docs/design/prompt-engineering/concepts.md)
- [Semantic Language](/docs/design/prompt-engineering/semantic-language.md)
- [Testing Framework](/docs/design/prompt-engineering/testing-framework.md)

## Archived Files

| File | Purpose | Replacement |
|------|---------|-------------|
| `TRIAGE_WORKFLOW.md` | Assess and route new issues | `.team/duties/TRIAGE_DUTY.md` |
| `RESEARCH_WORKFLOW.md` | Validate approaches and create specs | `.team/duties/RESEARCH_DUTY.md` |
| `IMPLEMENTATION_WORKFLOW.md` | Implement validated designs | `.team/duties/IMPLEMENTATION_DUTY.md` |
| `TECH_DEBT_WORKFLOW.md` | Discover and address technical debt | `.team/duties/TECH_DEBT_DUTY.md` |
| `PRODUCT_PRIORITIZATION_WORKFLOW.md` | Prioritize backlog items | `.team/duties/PRODUCT_PRIORITIZATION_DUTY.md` |
| `PROCESS_MODELING_WORKFLOW.md` | Improve workflows and processes | `.team/duties/PROCESS_MODELING_DUTY.md` |
| `PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` | Parameters for product prioritization | Merged into duty file |

## Key Improvements

1. **Platform Abstraction**: Semantic operations hide platform details
2. **Reusability**: Common procedures shared across duties
3. **Testability**: Clear testing framework with leak detection
4. **Maintainability**: Changes to one layer don't cascade
5. **Extensibility**: Easy to add Azure DevOps or other platforms

## Migration Phases

The migration was completed across 6 phases:

- **Phase 0** (#364): Process Modeling alignment ✅
- **Phase 1** (#365): Kernel layer foundation ✅
- **Phase 2** (#366): Global procedures ✅
- **Phase 3** (#367): Duty migration (all 7 duties) ✅
- **Phase 4** (#368): Orchestration update ✅
- **Phase 5** (#369): Cleanup & validation ✅

## Why Archive Instead of Delete?

These files represent significant historical work and serve as:
- Reference for understanding evolution of the system
- Comparison point for future improvements
- Educational resource for understanding the migration
- Backup in case information was lost in translation

## For Future Reference

If you need to understand the old system:
1. Review these archived workflow files
2. Compare with new duty files in `.team/duties/`
3. See [Migration Guide](../MIGRATION_GUIDE.md) (if available)
4. Consult design documents in `/docs/design/prompt-engineering/`

---

**Do not use these files** - they are for historical reference only. Use the new duty-based system in `.team/duties/` instead.
