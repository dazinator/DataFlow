# POC Documentation Structure

## Overview

This document describes the current POC documentation structure. The structure organizes documentation by purpose and audience, making it easy to find relevant information whether you're learning concepts, implementing features, conducting research, or reviewing historical decisions.

**📖 For Research Workflow**: See [/research/RESEARCH_WORKFLOW.md](/research/RESEARCH_WORKFLOW.md) for detailed guidance on conducting research, handling pivots, and managing exploratory code/documentation.

## Current Structure

```
/poc
├── /docs                                # All documentation organized by purpose
│   ├── /design                          # Core design documentation and architecture
│   │   └── /blocks                      # Block-specific design documents
│   ├── /guides                          # How-to guides and implementation patterns
│   ├── /reference                       # API reference and specifications
│   ├── /research                        # Exploratory documentation, investigations, and performance analysis
│   ├── /adr                             # Architecture Decision Records (adopted decisions)
│   ├── /plans                           # Action plans, proposals, and phased plans
│   │   └── /PHASE_X_NAME/               # Individual phase plan folders
│   │       ├── plan.md                  # The plan itself
│   │       ├── /proposed-docs           # Docs staged for integration (if adopted)
│   │       └── /archived                # Dismissed approaches with rationale
│   ├── POC_GLOSSARY.md                  # Terminology reference (adopted terms)
│   ├── RESEARCH_GLOSSARY.md             # Explored but not adopted terms
│   ├── POC_DOCUMENTATION_STRUCTURE.md   # This file - structure guide
│   └── INDEX.md                         # Navigation hub
├── /DataFlow.POC                        # Production-track POC code
│   ├── /Core                            # Production code
│   └── /Exploratory                     # Exploratory implementations (compilable)
│       └── /PhaseXName                  # Phase-specific exploratory code
├── /DataFlow.POC.Tests                  # Tests
│   ├── /Integration                     # Production-track integration tests
│   ├── /Unit                            # Production-track unit tests
│   └── /Exploratory                     # Exploratory tests (marked with [Category("Exploratory")])
│       └── /PhaseXName                  # Phase-specific exploratory tests
└── README.md                            # Quick start and overview (entry point)
```


## Folder Descriptions

### `/docs/design` - Design Documentation
**Purpose**: Document the architecture, design principles, and design decisions for the POC.

**Target Audience**: Developers learning the system architecture and design rationale.

**Characteristics**:
- Stable (updated for breaking design changes only)
- Topic-organized (not phase-organized)
- High-level explanations with diagrams
- Design principles and patterns

**Current Files**:
```
/docs/design
├── /blocks            # Block-specific design documentation
│   └── [block design docs]
└── [architecture and design docs]
```

**Content Scope**:
- Core architecture concepts (epochs, epoch-vectors, global-alignment)
- System design principles
- Block designs and patterns
- Transaction boundaries and lifecycle events
- Merge handling and checkpoint recovery

---

### `/docs/guides` - How-To Guides
**Purpose**: Practical patterns and implementation guidance.

**Target Audience**: Developers implementing features.

**Characteristics**:
- Task-oriented ("How to...")
- Code-heavy with complete examples
- Updated as patterns evolve

**Current Files**:
```
/docs/guides
└── creating-tracking-blocks.md       # EntityTrackingBlock pattern
```

**Content Scope**:
- Implementation patterns
- Integration guides (e.g., EF Core)
- Testing strategies
- Performance optimization techniques
- Common recipes and snippets

---

### `/docs/reference` - API Reference
**Purpose**: Detailed specifications and API documentation.

**Target Audience**: Developers needing precise technical details.

**Characteristics**:
- Exhaustive and precise
- Generated or maintained with code
- Version-aware

**Current Files**:
```
/docs/reference
└── README.md
```

**Content Scope**:
- Interface specifications
- Class documentation
- Pattern specifications
- Performance characteristics
- API contracts

---

### `/docs/research` - Research and Investigations
**Purpose**: Document exploratory work, investigations, performance analysis, and pathfinding activities.

**Target Audience**: Developers and architects understanding exploration history and findings.

**Characteristics**:
- Exploratory in nature
- Documents findings and approaches
- May document paths not taken
- Captures learning from investigations
- Includes performance analysis and benchmarking

**Content Scope**:
- Performance investigations and benchmarking
- Architecture explorations
- Control signal propagation research
- Concurrency scaling investigations
- Alternative approach evaluations
- Proof-of-concept findings
- Benchmark methodologies and results
- Performance comparisons (e.g., .NET vs Python)

---

### `/docs/adr` - Architecture Decision Records
**Purpose**: Document significant architecture and design decisions following ADR format.

**Target Audience**: Developers and architects understanding why decisions were made.

**Characteristics**:
- Follows ADR format (Context, Decision, Consequences)
- Named by date and title
- Immutable once decided (new ADRs supersede old ones)
- Captures alternatives considered

**Naming Convention**: `YYYY-MM-DD-descriptive-title.md`

**Content Scope**:
- Significant architecture decisions
- Design trade-offs and rationale
- Alternatives considered
- Consequences of decisions

---

### `/docs/plans` - Action Plans, Proposals, and Phased Plans
**Purpose**: Document actionable items, proposals for future work, and historical phased development plans.

**Target Audience**: Contributors and stakeholders planning future work or understanding historical development progression.

**Characteristics**:
- Can be refined and referenced from GitHub issues
- Named with descriptive titles
- Encapsulate complete actionable plans
- Includes historical phase-by-phase evolution (immutable once complete)
- Chronological organization for phased plans
- **New**: Individual phase folders with staged documentation workflow

**Content Scope**:
- Action plans and migration plans
- Feature proposals and refactoring plans
- Organizational improvements
- Historical phased development plans (PHASE1-7+)
- Phase summaries and indices

**Phase Folder Structure** (Phase 7+):
```
/docs/plans/PHASE_X_NAME/
├── plan.md                          # The plan itself
├── /proposed-docs/                  # Docs staged for adoption (during research)
│   ├── glossary-additions.md       # Terms to add if adopted
│   ├── adr-*.md                    # ADRs being considered
│   ├── design-*.md                 # Design docs being proposed
│   └── research-findings.md        # Research findings
└── /archived/                       # Dismissed approaches
    ├── README.md                    # Pivot rationale
    ├── adr-*-dismissed.md          # Dismissed ADRs
    └── design-*-dismissed.md       # Dismissed designs
```

**Workflow**: See [/research/RESEARCH_WORKFLOW.md](/research/RESEARCH_WORKFLOW.md) for detailed guidance on:
- How to structure phase folders during research
- Handling pivots and archiving dismissed approaches
- Promoting adopted documentation after PR approval
- Managing exploratory code and tests

**Legacy Files** (Phases 1-6):
```
/docs/plans
├── PHASE2_CONTROL_SIGNAL_INVESTIGATION.md
├── PHASE3_EPOCH_STREAM_SEGMENTATION.md
├── PHASE4_SOURCE_ACTOR_AND_STREAMING_BUFFERS.md
├── PHASE5_EFCORE_ANCHORING_DEMO.md
├── PHASE6_EPOCH_LIFECYCLE.md
└── ... (phase summaries and indices)
```
├── PHASE_INDEX.md
└── README.md
```

**Purpose of Phased Plans**:
- Understand design decisions in historical context
- See what was tried and why in each phase
- Reference for similar future work
- Track progression of development

---

## Documentation Files in `/docs/`

### POC_GLOSSARY.md
**Location**: `/docs/POC_GLOSSARY.md`

**Purpose**: Central terminology reference for **adopted** terms that are part of current POC architecture.

**Status**: Active

**Updates**: Add terms as concepts are adopted into the codebase.

**Format**: Terms marked with ✅ status indicating they are part of current architecture.

---

### RESEARCH_GLOSSARY.md
**Location**: `/docs/RESEARCH_GLOSSARY.md`

**Purpose**: Terminology reference for **explored but not adopted** terms.

**Status**: Active

**Updates**: Add terms for approaches explored during research that were ultimately dismissed.

**Format**: Terms marked with ❌ status, including exploration date, dismissal reason, and what superseded them.

**Rationale**: Preserves exploration history and prevents re-exploration of dismissed approaches while keeping main glossary focused on current architecture.

---

## Repository-Level Documentation

### RESEARCH_WORKFLOW.md
**Location**: `/research/RESEARCH_WORKFLOW.md` (repository root)

**Purpose**: Comprehensive guide for conducting research and exploration across the entire repository (POC or production code).

**Status**: Active

**Content**:
- Research-to-implementation workflow
- How to structure research folders
- Managing exploratory code vs documentation
- Pivot workflow and knowledge preservation
- Code reversion process (reviewer-controlled)
- Creating implementation-ready GitHub issues

**Target Audience**: All contributors conducting research work.

---

### POC_DOCUMENTATION_STRUCTURE.md
**Location**: `/docs/POC_DOCUMENTATION_STRUCTURE.md`

**Purpose**: Documents the current documentation structure and organization.

**Status**: Active (this file)

**Updates**: Update when structure changes or new folders are added.

---

### INDEX.md
**Location**: `/docs/INDEX.md`

**Purpose**: Navigation hub for all documentation.

**Sections**:
- I want to learn design → `/docs/design`
- I want to implement a feature → `/docs/guides`
- I want API details → `/docs/reference`
- I want to understand history → `/docs/plans` (see phased plans)
- I want to see research → `/docs/research`
- I want to review decisions → `/docs/adr`
- Glossary link
- FAQ

---

## Root-Level File

### README.md
**Location**: `/poc/README.md` (root level)

**Purpose**: Quick start, overview, and entry point for the POC.

**Sections**:
- What is this POC?
- Quick start guide
- Link to key documents
- Architecture diagram
- Status and roadmap

**Why at root**: Serves as the primary entry point when browsing the POC folder.

---

## Documentation Maintenance Guidelines

### When to Update Each Type

| Doc Type | Update Trigger | Update Frequency |
|----------|----------------|------------------|
| `/design` | Breaking design change | Rarely (major versions) |
| `/guides` | New pattern discovered | As needed |
| `/reference` | API change | Every PR affecting APIs |
| `/research` | New investigation or benchmark completed | Per investigation/benchmark |
| `/adr` | Significant decision made | Per major decision |
| `/plans` | New proposal or phase completed | As needed / Once per phase (phased plans immutable after) |
| `POC_GLOSSARY.md` | New terminology | As terms are introduced |

---

### Writing Style Guidelines

#### Design Documents
- Start with "What" and "Why"
- Use diagrams extensively
- Avoid code snippets (link to guides instead)
- Explain trade-offs and design principles

#### Guides
- Start with specific goal ("How to track entities across epochs")
- Provide complete, runnable examples
- Include common pitfalls section
- Link to relevant design docs and reference

#### Reference
- Be exhaustive and precise
- Document every parameter, return value, exception
- Include complexity analysis where relevant
- Cross-link related APIs

#### Research
- Document the question being explored
- Describe approaches tried
- Document findings (positive and negative)
- Include recommendations or next steps
- For benchmarks: Document methodology clearly, include environment details, make results reproducible, explain what is being measured

#### ADRs
- Follow standard ADR format
- Document alternatives considered
- Be clear about trade-offs
- Link to related ADRs

#### Plans (including Phased Plans)
- For phased plans: Document decisions made and alternatives considered
- Preserve original reasoning (don't rewrite history)
- Include test results and metrics where applicable
- Link forward to where concepts evolved
- For action plans: Be clear about goals, steps, and success criteria

---

## Content Organization Best Practices

### Avoid Duplication
- Design documents should reference, not duplicate, phased plan documents
- Guides should link to design concepts, not re-explain them
- Research documents can become ADRs when decisions are made

### Cross-Referencing
- Always link between related documents
- Link from stable docs (design/guides) to historical context (plans/research)
- Link from phased plans forward to where concepts evolved

### Versioning
- Add "Last Updated" dates to stable documents
- Consider version tags for major design changes
- Keep old ADRs; create new ones to supersede

---

## Migration Notes

Documents are migrated from root or other locations based on their primary purpose:

- **Conceptual/architectural** → `/docs/design`
- **Investigative/exploratory** → `/docs/research`
- **Decided architecture choices** → `/docs/adr`
- **Performance analysis** → `/docs/research`
- **Implementation patterns** → `/docs/guides`
- **Technical specifications** → `/docs/reference`

When in doubt:
- If exploring or benchmarking → `/docs/research`
- If decided → `/docs/adr` or `/design`
- If teaching → `/docs/guides`

---

**Document Version:** 2.3  
**Last Updated:** 2025-11-03  
**Status:** Current Structure
