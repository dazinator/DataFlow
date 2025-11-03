# Historical Phase Documentation

> **Note on Terminology:** The "phases" in this folder (Phase 2-6) refer to **POC development milestones** on the path to production readiness. This is distinct from the "Phase 2" documentation migration task that organized this documentation structure. These POC phases represent the evolution of the epoch-based coordination system itself.

## Purpose

This folder contains the historical phase-by-phase evolution of the POC's epoch-based coordination system toward production readiness. These documents are **archived** and represent the chronological development journey, design decisions, and explorations that led to the current architecture.

## Why Preserve Phase History?

### For Contributors
- **Understand design rationale** - See why certain approaches were chosen over alternatives
- **Learn from exploration** - Understand what was tried and what didn't work
- **Reference for future work** - Similar problems may benefit from past investigations

### For Stakeholders
- **Track progress** - See how the system evolved over time
- **Review decisions** - Understand the thought process behind key architectural choices
- **Assess maturity** - Gauge the depth of exploration and testing

## Phase Organization

> **Important:** These phases document the POC's evolution toward production readiness. They are **not** related to the documentation reorganization phases.

Phases are organized chronologically and represent discrete milestones in the POC's development toward production:

| Phase | Focus | Status |
|-------|-------|--------|
| Phase 2 | Control signal investigation | ✅ Archived |
| Phase 3 | Epoch stream segmentation | ✅ Archived |
| Phase 4 | Source actor and streaming buffers | ✅ Archived |
| Phase 5 | EF Core anchoring demo | ✅ Archived |
| Phase 6 | Epoch lifecycle model | ✅ Archived |

See [PHASE_INDEX.md](./PHASE_INDEX.md) for detailed navigation.

## Content vs. Concepts

### Phase Documents (This Folder)
- **Purpose:** Historical record of evolution and decisions
- **Organization:** Chronological (phase-by-phase)
- **Audience:** Contributors understanding history
- **Update Policy:** Immutable once archived
- **Content:** Design explorations, alternatives considered, test results, rationales

### Concept Documents (`/concepts`)
- **Purpose:** Stable reference for current architecture
- **Organization:** Topical (by concept)
- **Audience:** Developers learning the system
- **Update Policy:** Updated for breaking changes only
- **Content:** Core concepts, principles, architecture

### Guide Documents (`/guides`)
- **Purpose:** Practical implementation patterns
- **Organization:** Task-oriented (by use case)
- **Audience:** Developers implementing features
- **Update Policy:** Updated as patterns evolve
- **Content:** How-to guides, code examples, best practices

## How to Use This Folder

### Learning the Current System
**Start with concepts and guides, not phases:**
1. Read `/concepts` to understand core ideas
2. Read `/guides` for implementation patterns
3. Read phases only if you need historical context

### Understanding a Design Decision
If you're wondering "why was it designed this way?":
1. Check `/concepts` for the current rationale
2. Search phases for the original decision point
3. Read the phase document to see alternatives considered

### Working on Similar Problems
If you're tackling a problem similar to what phases explored:
1. Search phases for related investigations
2. Review what was tried and the outcomes
3. Learn from the experiments and decisions made

## Immutability Policy

Phase documents are **immutable** once archived:
- ✅ Preserve original content and reasoning
- ✅ Keep as historical record
- ❌ Don't update to reflect current implementation
- ❌ Don't rewrite history with hindsight

If a phase's concepts evolved:
- Update `/concepts` with the current understanding
- Leave phase document unchanged
- Add forward references if helpful: "This concept evolved into [concept/epochs.md]"

## Navigation

For detailed phase content and navigation, see [PHASE_INDEX.md](./PHASE_INDEX.md).

For current, stable documentation, see:
- [/concepts](../concepts/) - Core conceptual documentation
- [/guides](../guides/) - How-to guides and patterns
- [/reference](../reference/) - API reference and specifications
- [INDEX.md](../INDEX.md) - Main navigation hub
