# POC Documentation Structure Proposal

## Problem Statement

Currently, POC documentation mixes two distinct purposes:
1. **Historical Progression** - Phase-by-phase evolution (PHASE1-6)
2. **Stable Reference** - End-user concepts, guides, and API docs

This creates challenges:
- Users don't know where to find current, stable information
- Phase documents are tied to specific milestones, not organized by topic
- Usage examples scattered across phase documents
- Hard to maintain as features evolve

## Proposed Structure

### High-Level Organization

```
/poc
├── /concepts          # Stable conceptual documentation
├── /guides            # How-to guides and patterns
├── /reference         # API reference and specifications
├── /phases            # Historical phase progression (archived)
├── POC_GLOSSARY.md    # Terminology reference
├── README.md          # Quick start and overview
└── INDEX.md           # Navigation hub
```

### Detailed Breakdown

#### `/concepts` - Conceptual Documentation
**Purpose**: Explain core concepts and design principles.

**Target Audience**: Developers learning the system.

**Characteristics**:
- Stable (updated for breaking changes only)
- Topic-organized (not phase-organized)
- High-level explanations with diagrams

**Proposed Files**:
```
/concepts
├── epochs.md                    # What are epochs?
├── epoch-vectors.md             # Multi-source coordination
├── global-alignment.md          # Watermark and completion
├── transaction-boundaries.md    # Where to commit safely
├── lifecycle-events.md          # Event model overview
├── merge-handling.md            # Fan-in and context promotion
├── checkpoint-recovery.md       # Checkpoint concepts
└── architecture-overview.md     # High-level architecture
```

**Content Migration**:
- Extract conceptual sections from PHASE3, PHASE5, PHASE6
- Reorganize by topic rather than chronology
- Add cross-references between concepts

#### `/guides` - How-To Guides
**Purpose**: Practical patterns and implementation guidance.

**Target Audience**: Developers implementing features.

**Characteristics**:
- Task-oriented ("How to...")
- Code-heavy with complete examples
- Updated as patterns evolve

**Proposed Files**:
```
/guides
├── creating-tracking-blocks.md       # EntityTrackingBlock pattern
├── handling-merges.md                # Implementing merge logic
├── ef-core-integration.md            # EF Core best practices
├── multi-sink-pipelines.md           # Multiple tracking blocks
├── performance-optimization.md       # Fast paths, profiling
├── lifecycle-participation.md        # Implementing IEpochLifecycleParticipant
├── testing-epoch-flows.md            # Testing strategies
└── common-patterns.md                # Recipes and snippets
```

**Content Migration**:
- Move usage examples from PHASE6_EPOCH_LIFECYCLE.md
- Extract patterns from ACTOR_BLOCK.md
- Consolidate examples from test files

#### `/reference` - API Reference
**Purpose**: Detailed specifications and API documentation.

**Target Audience**: Developers needing precise technical details.

**Characteristics**:
- Exhaustive and precise
- Generated or maintained with code
- Version-aware

**Proposed Files**:
```
/reference
├── interfaces/
│   ├── IEpochLifecycleParticipant.md  # Full API spec
│   ├── IBlockContext.md
│   ├── IEpochStream.md
│   └── ISourceActor.md
├── classes/
│   ├── EpochVector.md                 # Methods, properties, examples
│   ├── EpochLifecycleCoordinator.md
│   └── BlockContext.md
├── patterns/
│   ├── EntityTrackingBlock.md         # Generic pattern spec
│   └── SourceActorBase.md
└── benchmarks.md                      # Performance characteristics
```

**Content Generation**:
- Extract from code XML comments
- Add usage examples
- Document performance characteristics

#### `/phases` - Historical Progression (Archived)
**Purpose**: Preserve phase-by-phase POC evolution for historical reference.

**Target Audience**: Contributors understanding evolution, stakeholders reviewing decisions.

**Characteristics**:
- Immutable (archived once complete)
- Chronological organization
- Decision rationales and explorations

**Proposed Files** (existing files moved here):
```
/phases
├── PHASE1_INITIAL_ARCHITECTURE.md
├── PHASE2_CONTROL_SIGNAL_INVESTIGATION.md
├── PHASE3_EPOCH_STREAM_SEGMENTATION.md
├── PHASE4_SOURCE_ACTOR_AND_STREAMING_BUFFERS.md
├── PHASE5_EFCORE_ANCHORING_DEMO.md
├── PHASE6_EPOCH_LIFECYCLE.md
├── PHASE_INDEX.md                     # Navigation for phases
└── README.md                          # Why phases exist
```

**Purpose of Preservation**:
- Understand design decisions
- See what was tried and why
- Reference for similar future work

## Root-Level Files

### POC_GLOSSARY.md
**Status**: ✅ Created

**Purpose**: Central terminology reference.

**Updates**: Add terms as concepts emerge.

### README.md
**Purpose**: Quick start, overview, and orientation.

**Sections**:
- What is this POC?
- Quick start guide
- Link to key documents
- Architecture diagram
- Status and roadmap

### INDEX.md
**Purpose**: Navigation hub for all documentation.

**Sections**:
- I want to learn concepts → `/concepts`
- I want to implement a feature → `/guides`
- I want API details → `/reference`
- I want to understand history → `/phases`
- Glossary link
- FAQ

## Migration Plan

### Phase 1: Create Structure (Immediate)
1. ✅ Create `/concepts`, `/guides`, `/reference`, `/phases` folders
2. ✅ Create POC_GLOSSARY.md
3. ✅ Create POC_DOCUMENTATION_STRUCTURE.md (this file)

### Phase 2: Migrate Content (Next PR)
1. Extract concepts from phase docs → `/concepts`
2. Extract patterns/examples → `/guides`
3. Generate API reference → `/reference`
4. Move PHASE*.md files → `/phases`
5. Update cross-references

### Phase 3: Establish Maintenance Process (Ongoing)
1. Update `/concepts` only for breaking changes
2. Add new guides as patterns emerge
3. Regenerate `/reference` from code
4. Archive completed phases
5. Keep INDEX.md current

## Content Maintenance Guidelines

### When to Update Each Type

| Doc Type | Update Trigger | Update Frequency |
|----------|----------------|------------------|
| `/concepts` | Breaking conceptual change | Rarely (major versions) |
| `/guides` | New pattern discovered | As needed |
| `/reference` | API change | Every PR affecting APIs |
| `/phases` | New phase complete | Once per phase (immutable after) |
| `POC_GLOSSARY.md` | New terminology | As terms are introduced |

### Writing Style Guidelines

#### Concepts
- Start with "What" and "Why"
- Use diagrams extensively
- Avoid code snippets (link to guides instead)
- Explain trade-offs and design decisions

#### Guides
- Start with specific goal ("How to track entities across epochs")
- Provide complete, runnable examples
- Include common pitfalls section
- Link to relevant concepts and reference

#### Reference
- Be exhaustive and precise
- Document every parameter, return value, exception
- Include complexity analysis where relevant
- Cross-link related APIs

#### Phases
- Document decisions made and alternatives considered
- Preserve original reasoning (don't rewrite history)
- Include test results and metrics
- Link forward to where concepts landed

## Example Content Organization

### Scenario: User wants to implement entity tracking

**Journey**:
1. Start at `INDEX.md` → "I want to implement a feature"
2. Read `/concepts/transaction-boundaries.md` (understand why global alignment matters)
3. Read `/guides/creating-tracking-blocks.md` (see EntityTrackingBlock pattern)
4. Reference `/reference/classes/EpochVector.md` (understand Subsumes method)
5. Check `POC_GLOSSARY.md` for unfamiliar terms
6. Optionally: Read `/phases/PHASE6_EPOCH_LIFECYCLE.md` (understand evolution)

**Key**: Clear path from learning → implementing → reference → deep-dive.

## Benefits

### For New Users
- ✅ Clear learning path
- ✅ Know where to look for what
- ✅ Concepts separated from implementation
- ✅ Glossary for quick lookups

### For Contributors
- ✅ Know where to add new content
- ✅ Avoid redundant documentation
- ✅ Historical context preserved
- ✅ Easy to keep docs synchronized

### For Maintainers
- ✅ Clear ownership boundaries
- ✅ Phases are immutable (less maintenance)
- ✅ Concepts stable (fewer updates needed)
- ✅ Guides flexible (can evolve)

## Open Questions

### 1. Should we version the documentation?
**Proposal**: Add version tags to `/concepts` and `/guides` when breaking changes occur.

**Example**: `epochs.md` could have `<!-- Version: 1.0, Updated: 2024-11 -->`

### 2. How to handle examples that span multiple docs?
**Proposal**: Create `/examples` folder with complete, runnable examples that link to relevant docs.

```
/examples
├── basic-tracking-block/
│   ├── README.md
│   ├── Program.cs
│   └── links: /concepts/epochs.md, /guides/creating-tracking-blocks.md
└── multi-source-merge/
    ├── README.md
    ├── Program.cs
    └── links: /concepts/merge-handling.md, /guides/handling-merges.md
```

### 3. Should phases be in a separate repo branch?
**Decision**: No. Keep in `/phases` for easy access to evolution history.

## Next Steps

### Immediate (This PR)
- [x] Create POC_GLOSSARY.md
- [x] Create POC_DOCUMENTATION_STRUCTURE.md
- [ ] Get feedback on structure proposal

### Next PR (Content Migration)
- [ ] Create folder structure
- [ ] Migrate PHASE6 content to `/concepts`, `/guides`, `/reference`
- [ ] Move PHASE6_EPOCH_LIFECYCLE.md to `/phases`
- [ ] Update INDEX.md
- [ ] Update cross-references

### Future
- [ ] Migrate PHASE1-5 content
- [ ] Generate API reference from code
- [ ] Create `/examples` folder
- [ ] Add version tags to stable docs

---

## Feedback Welcome

This is a proposal. Key questions for review:
1. Does the `/concepts` vs `/guides` distinction make sense?
2. Is `/phases` the right place for historical docs?
3. Should we add `/examples` as a top-level folder?
4. Any other organizational concerns?

**Discussion**: [Link to GitHub discussion or issue]
