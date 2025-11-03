# POC Documentation Migration - Action Plan

## Executive Summary

This document provides a comprehensive action plan for migrating unmigrated POC documentation into the new structure defined in `POC_DOCUMENTATION_STRUCTURE.md`. 

**Current Status:**
- ✅ Structure updated: `/docs/design`, `/docs/research` (includes performance analysis/benchmarks), `/docs/adr`, `/docs/plans` folders created
- ✅ Concept documents migrated to `/docs/design` folder
- ✅ Action plan moved to `/docs/plans` folder
- ✅ Benchmarks consolidated into `/docs/research` (performance analysis is research)
- ✅ All 16 root-level documents migrated
- ✅ SUMMARY.md moved to `/docs/research/edge-first-poc-summary.md`

**Migration Goal:** Complete - all documentation organized according to the new structure (docs/design/, docs/guides/, docs/reference/, docs/research/, docs/adr/, docs/plans/) while preserving historical progression and maintaining discoverability.

---

## Current State Analysis

### Already Migrated ✅
The following structure now exists:

```
/docs/design/             # 7 files - design and architecture (renamed from concepts)
├── /blocks/         # Block-specific design documentation
├── checkpoint-recovery.md
├── epoch-vectors.md
├── epochs.md
├── global-alignment.md
├── lifecycle-events.md
├── merge-handling.md
└── transaction-boundaries.md

/docs/guides/            # 1 file - practical implementation guide
└── creating-tracking-blocks.md

/docs/plans/            # 11 files - historical phase progression
├── PHASE2_*.md (3 files)
├── PHASE3_*.md (4 files)
├── PHASE4_*.md (1 file)
├── PHASE5_*.md (1 file)
├── PHASE6_*.md (1 file)
├── PHASE_INDEX.md
└── README.md

/docs/reference/         # 1 file - placeholder
└── README.md

/docs/research/          # Empty - to be populated  
/docs/adr/               # Empty - to be populated
/docs/plans/             # 1 file - this action plan
└── DOCUMENTATION_MIGRATION_ACTION_PLAN.md
```

### Unmigrated Documents

#### 1. Root-Level POC Documents (16 files)

**Edge-First Design Documents:**
- `SUMMARY.md` - Complete POC summary with test results
- `ARCHITECTURE.md` - Visual diagrams and architecture explanation
- `COMPARISON.md` - Side-by-side comparison with current design
- `ACTOR_BLOCK.md` - ActorBlock guide with DI scope rotation
- `DESIGN_DECISIONS.md` - Design clarifications and alternatives

**Epoch/Control Signal Documents:**
- `ENVELOPE_FRAMEWORK.md` - Envelope and control signal framework
- `CONTROL_SIGNAL_INVESTIGATION_SUMMARY.md` - Control signal architecture investigation
- `CONTROL_SIGNAL_PROPAGATION_EXPLORATION.md` - Control signal propagation exploration
- `EPOCH_CONTROL_PLANE_DESIGN.md` - Out-of-band epoch control plane design
- `SIDE_CHANNEL_IMPLEMENTATION_SUMMARY.md` - Side-channel architecture implementation
- `SIDE_CHANNEL_PERFORMANCE_ANALYSIS.md` - Performance analysis of side-channel

**Benchmark/Performance Documents:**
- `BENCHMARK_RESULTS_ANALYSIS.md` - Control signal strategy benchmark analysis
- `TYPED_CHANNEL_PERFORMANCE.md` - Typed channel performance optimization
- `PYTHON_BENCHMARK_SUMMARY.md` - Python benchmark comparison summary

**Investigation/Summary Documents:**
- `INVESTIGATION_SUMMARY.md` - POC concurrency scaling investigation
- `FUTURE_ENHANCEMENTS.md` - Future enhancement ideas and design considerations

#### 2. DataFlow.POC.Benchmarks/ (15+ files)
- `README.md` - Main benchmark overview
- `BENCHMARK_README.md` - Benchmark strategies documentation
- `BENCHMARK_SUMMARY.md` - Benchmark summary
- `BENCHMARK_MEMORY_ANALYSIS.md` - Memory analysis
- `MICROBENCHMARK_README.md` - Microbenchmark documentation
- `PHASE6_BENCHMARK_RESULTS.md` - Phase 6 benchmark results
- `POC_COMPARISON_IMPLEMENTATION.md` - POC comparison implementation
- `README-COMPARISON.md` - Comparison benchmark documentation
- `benchmark-results/*.md` - 10+ individual benchmark result files

#### 3. EpochAnchoringDemo/ (2 files)
- `README.md` - Demo project overview
- `UNDERSTANDING_ANCHORS_VS_CHECKPOINTS.md` - Conceptual clarification

#### 4. python-benchmarks/ (5 files)
- `README.md` - Python benchmarks overview
- `ARCHITECTURE.md` - Benchmark infrastructure architecture
- `BENCHMARK_ANALYSIS.md` - Analysis methodology
- `DOTNET_VS_PYTHON_RESULTS.md` - Comparison results
- `QUICKSTART.md` - Quick start guide
- `results/*.md` - Individual result files

#### 5. refactoring-logs/ (1 file)
- `2025-10-26-dataflowgraph-refactoring.md` - Refactoring log

---

## Proposed Migration Strategy

### Guiding Principles

1. **Preserve Historical Context** - Don't delete or lose information
2. **Improve Discoverability** - Make it easier to find relevant documentation
3. **Maintain Stability** - Keep stable concepts separate from evolving guides
4. **Reduce Duplication** - Consolidate overlapping content where appropriate
5. **Update Cross-References** - Ensure all links work after migration

### Migration Categories

Documents fall into these categories:

| Category | Target Location | Rationale |
|----------|----------------|-----------|
| **Design/Architecture** | `/docs/design/` | Core design principles and architecture (stable) |
| **Block Designs** | `/docs/design/blocks/` | Block-specific design documentation |
| **How-To Guides** | `/docs/guides/` | Task-oriented implementation patterns |
| **API Reference** | `/docs/reference/` | Technical specifications |
| **Benchmarks** | `/docs/research/` | Benchmark documentation and results |
| **Research/Investigations** | `/docs/research/` | Exploratory work, investigations, pathfinding |
| **Design Decisions** | `/docs/adr/` | Architecture Decision Records (ADR format) |
| **Historical/Phase** | `/docs/plans/` | Phase-specific work and historical progression |
| **Action Plans** | `/docs/plans/` | Proposals and action plans for future work |
| **Project Documentation** | Keep in subfolder | Project-specific docs (code projects like EpochAnchoringDemo) |

---

## Detailed Migration Plan

### Priority 1: Design Documents (Immediate)

These documents contain stable design and architecture concepts.

#### 1.1 Move to `/docs/design/`

| Source | Target | Rationale |
|--------|--------|-----------|
| `ENVELOPE_FRAMEWORK.md` | `/docs/design/envelope-framework.md` | Core design - envelope and control signal system |
| `EPOCH_CONTROL_PLANE_DESIGN.md` | `/docs/design/epoch-control-plane.md` | Core design - out-of-band control plane architecture |
| `ARCHITECTURE.md` | `/docs/design/edge-first-architecture.md` | Core architecture design with diagrams |
| `EpochAnchoringDemo/UNDERSTANDING_ANCHORS_VS_CHECKPOINTS.md` | `/docs/design/anchors-checkpoints-distinction.md` | Important conceptual clarification |

**Actions:**
- [x] Identified documents for migration
- [ ] Move files to `/docs/design/`
- [ ] Add frontmatter with version and last updated date
- [ ] Update cross-references in moved files
- [ ] Update INDEX.md to include new design documents
- [ ] Update README.md to link to design concepts

#### 1.2 Move to `/docs/design/blocks/`

Block-specific design documentation:

| Source | Target | Rationale |
|--------|--------|-----------|
| `ACTOR_BLOCK.md` | `/docs/design/blocks/actor-block.md` | ActorBlock design and patterns |

**Actions:**
- [ ] Move file to `/docs/design/blocks/`
- [ ] Update cross-references
- [ ] Create README.md in blocks/ folder for navigation

---

### Priority 2: Implementation Guides (Immediate)

These are task-oriented guides showing how to implement features.

#### 2.1 Move to `/docs/guides/`

| Source | Target | Rationale |
|--------|--------|-----------|
| `COMPARISON.md` | `/docs/guides/migrating-from-current-design.md` | Migration guide for adopting edge-first design |

**Actions:**
- [ ] Move file to `/docs/guides/`
- [ ] Rename to follow "how-to" naming pattern
- [ ] Update cross-references
- [ ] Add to INDEX.md guide section

**Note:** `ACTOR_BLOCK.md` is now a design document and will move to `/docs/design/blocks/` instead.

---

### Priority 3: Benchmark Documentation (High Priority)

Benchmark documentation, methodologies, and results.

#### 3.1 Move to `/docs/research/`

| Source | Target | Rationale |
|--------|--------|-----------|
| `TYPED_CHANNEL_PERFORMANCE.md` | `/docs/research/typed-channel-performance.md` | Performance analysis |
| `BENCHMARK_RESULTS_ANALYSIS.md` | `/docs/research/control-signal-strategies.md` | Benchmark comparison analysis |
| `SIDE_CHANNEL_PERFORMANCE_ANALYSIS.md` | `/docs/research/side-channel-performance.md` | Performance analysis |
| `PYTHON_BENCHMARK_SUMMARY.md` | `/docs/research/python-comparison.md` | Cross-language benchmark |

**Actions:**
- [ ] Move benchmark documentation files to `/docs/research/`
- [ ] Create `/docs/research/results/` subfolder for timestamped results
- [ ] Create `/docs/research/README.md` for navigation
- [ ] Update INDEX.md benchmark section

#### 3.2 Organize DataFlow.POC.Benchmarks/ Documentation

Keep in project folder but organize better:
- Consolidate multiple README files
- Link from main `/docs/research/` folder
- Leave timestamped results in place

---

### Priority 4: Research and Investigations (Medium Priority)

Documents capturing research, explorations, and investigations.

#### 4.1 Move to `/docs/research/`

| Source | Target | Rationale |
|--------|--------|-----------|
| `INVESTIGATION_SUMMARY.md` | `/docs/research/concurrency-scaling-investigation.md` | POC scaling investigation |
| `CONTROL_SIGNAL_INVESTIGATION_SUMMARY.md` | `/docs/research/control-signal-alternatives.md` | Control signal architecture research |
| `CONTROL_SIGNAL_PROPAGATION_EXPLORATION.md` | `/docs/research/control-signal-propagation.md` | Detailed exploration |
| `SIDE_CHANNEL_IMPLEMENTATION_SUMMARY.md` | `/docs/research/side-channel-implementation.md` | Implementation investigation |

**Actions:**
- [ ] Move investigation documents to `/docs/research/`
- [ ] Create `/docs/research/README.md` for navigation
- [ ] Update cross-references
- [ ] Add research section to INDEX.md

---

### Priority 5: Architecture Decision Records (Medium Priority)

Documents that capture design decisions and rationale.

#### 5.1 Move to `/docs/adr/`

#### 5.1 Move to `/docs/adr/`

| Source | Target | Rationale |
|--------|--------|-----------|
| `DESIGN_DECISIONS.md` | `/docs/adr/2025-11-03-routing-strategies.md` | Design decisions and alternatives |

**Actions:**
- [ ] Move to `/docs/adr/` and rename with ADR naming convention
- [ ] Consider splitting if it contains multiple decisions
- [ ] Format as proper ADR (Context, Decision, Consequences)
- [ ] Create `/docs/adr/README.md` for navigation

#### 5.2 Move to `/docs/plans/` (Future Enhancements)

| Source | Target | Rationale |
|--------|--------|-----------|
| `FUTURE_ENHANCEMENTS.md` | `/docs/plans/future-enhancements.md` | Future work proposals |

**Actions:**
- [ ] Move to `/docs/plans/`
- [ ] Review and update content
- [ ] Link from INDEX.md

---

### Priority 6: Top-Level Summary Documents (Low Priority - Keep at Root)

These documents serve as entry points and should remain at the root level for discoverability.

#### 6.1 Keep at Root (Update Only)

| File | Action | Rationale |
|------|--------|-----------|
| `SUMMARY.md` | Keep, update cross-references | Primary entry point for edge-first POC |
| `README.md` | Keep, update with new structure | Main POC overview |
| `INDEX.md` | Keep, update navigation | Navigation hub |
| `POC_GLOSSARY.md` | Keep | Terminology reference |
| `POC_DOCUMENTATION_STRUCTURE.md` | Keep | Structure definition (meta-document) |

**Actions:**
- [ ] Update cross-references in SUMMARY.md
- [ ] Update navigation sections in README.md
- [ ] Update INDEX.md with all new locations
- [ ] Add "Last Updated" dates

---

### Priority 7: Project-Specific Documentation (Low Priority - Organize)

Documentation specific to subprojects should stay in those folders but be better organized.

#### 7.1 DataFlow.POC.Benchmarks/

**Current Issues:**
- Multiple README files with overlapping content
- Benchmark results scattered
- No clear navigation

**Proposed Structure:**
```
DataFlow.POC.Benchmarks/
├── README.md                          # Main entry (consolidate current READMEs)
├── docs/
│   ├── benchmark-strategies.md        # Consolidate BENCHMARK_README.md
│   ├── memory-analysis.md             # BENCHMARK_MEMORY_ANALYSIS.md
│   ├── comparison-guide.md            # README-COMPARISON.md
│   └── microbenchmarks.md             # MICROBENCHMARK_README.md
└── results/
    ├── README.md                      # Index of results
    ├── phase6/                        # Organize by phase
    │   └── PHASE6_BENCHMARK_RESULTS.md
    └── extended/                      # Organize by type
        └── [timestamped results]
```

**Actions:**
- [ ] Consolidate benchmark documentation
- [ ] Organize results by category
- [ ] Create clear README with navigation
- [ ] Update main poc/README.md to link properly

#### 7.2 EpochAnchoringDemo/

**Current State:** Well-organized, just needs one file moved (already addressed in Priority 1)

**Actions:**
- [ ] Move UNDERSTANDING_ANCHORS_VS_CHECKPOINTS.md (addressed in Priority 1)
- [ ] Update README.md cross-reference

#### 7.3 python-benchmarks/

**Current State:** Well-organized, good structure

**Actions:**
- [ ] Link from main poc/README.md
- [ ] Ensure cross-references to PYTHON_BENCHMARK_SUMMARY.md work after its migration

#### 7.4 refactoring-logs/

**Proposed Action:** Keep as-is, this is an appropriate location for refactoring logs

**Actions:**
- [ ] Add README.md to explain purpose
- [ ] Link from poc/README.md

---

## Migration Phases

### Phase 1: Core Structure (Week 1)
- Priority 1 (Concepts)
- Priority 2 (Guides)
- Priority 3 (Reference)

**Outcome:** All stable, reusable documentation is properly categorized and discoverable.

### Phase 2: Historical Organization (Week 2)
- Priority 4 (Investigations)
- Priority 5 (Design Decisions)

**Outcome:** Historical context is preserved and well-organized.

### Phase 3: Polish & Navigation (Week 3)
- Priority 6 (Update root documents)
- Priority 7 (Organize project docs)
- Update all cross-references
- Validate all links

**Outcome:** Complete, navigable documentation structure with no broken links.

---

## Validation Checklist

After migration, verify:

- [ ] All links work (no broken references)
- [ ] INDEX.md accurately reflects all documentation
- [ ] README.md has clear navigation to all sections
- [ ] POC_GLOSSARY.md is up to date
- [ ] Each subfolder has a README.md for navigation
- [ ] Cross-references between concepts/guides/reference work correctly
- [ ] Phase documents link forward to where concepts landed
- [ ] No duplicate content exists
- [ ] Git history is preserved (use `git mv` for moves)

---

## Stakeholder Feedback Received ✅

The following questions have been answered:

1. **✅ Benchmark Documentation:** Create new `/docs/research` (includes benchmarks) folder with results subfolder. Keep documentation about available benchmarks there.

2. **✅ Investigation Documents:** Create new `/docs/research` folder for exploratory documentation, investigations, and pathfinding activities.

3. **✅ Design Decisions:** Create new `/docs/adr` folder for Architecture Decision Records. Format as standard ADRs named by date and title.

4. **✅ SUMMARY.md:** Keep at root for now (may revisit later).

5. **✅ Benchmark Results:** Leave timestamped benchmark results alone for now.

6. **✅ Folder Naming:** Rename `/concepts` to `/docs/design` to better reflect architectural design documentation.

7. **✅ Block Documentation:** Create `/docs/design/blocks` subfolder for block-specific design documents (e.g., ACTOR_BLOCK.md).

---

## Success Criteria

Migration is complete when:

1. ✅ All documents are categorized into docs/design/guides/reference/benchmarks/research/adr/phases/plans
2. ✅ Navigation is clear and intuitive (INDEX.md, README.md)
3. ✅ No broken links exist
4. ✅ Historical progression is preserved in appropriate locations
5. ✅ Project-specific documentation is well-organized (code projects untouched)
6. ✅ Cross-references are updated and accurate
7. ✅ Stakeholders can easily find what they need
8. ✅ Only markdown documentation is reorganized (code projects remain in place)

---

## Migration Commands Reference

When executing migration, use git to preserve history:

```bash
# Move file preserving history
git mv source.md target.md

# Move to subfolder
git mv FILE.md subfolder/new-name.md

# Create new folder
mkdir -p concepts/architecture

# After changes
git add .
git commit -m "Migrate [DOCUMENT] to [LOCATION]"
```

---

## Conclusion

This action plan provides a clear, phased approach to migrating all remaining POC documentation into the new structure. The migration prioritizes:

1. **Stable concepts first** - Make core architecture easily discoverable
2. **Practical guides second** - Help developers implement features
3. **Reference material third** - Provide technical details on demand
4. **Historical context last** - Preserve progression without cluttering

By following this plan, the POC documentation will be well-organized, maintainable, and easy to navigate while preserving all historical context and decision rationale.

---

**Document Version:** 1.0  
**Created:** 2025-11-03  
**Author:** GitHub Copilot  
**Status:** Awaiting Stakeholder Feedback
