# Documentation Structure

## Overview

This document describes the current documentation structure. The structure organizes documentation by purpose and audience, making it easy to find relevant information whether you're learning concepts, implementing features, conducting research, or reviewing historical decisions.

## Current Structure

```
/[repo root]                             # repository root directory
├── /docs                                # All documentation organized by purpose
│   ├── /public                          # content for a public facing docs site to be published and accessed outside the repository. E.g mkdocs etc. This may not be present if no public facing docs are published for this repo.
│   ├── /domain                          # domain concepts, business logic, domain user-facing problem solution and logic semantics
│   ├── /design                          # Core design documentation and architecture
│   ├── /guides                          # How-to guides and implementation patterns
│   │   └── /[audience]/                 # subfolder for specific audiences e.g "internal" vs "public" or "dev" vs "product" etc. This hierarchy can continue with nested audience subfolders as necessary. 
│   ├── /reference                       # API reference and specifications
│   ├── /research                        # Exploratory documentation, investigations, and performance analysis
│   │   └── /[area_or_topic_name]/       # Individual research folders for different research efforts / areas / work items
│   │   └── RESEARCH_GLOSSARY.md         # Explored but not adopted terms
│   ├── /adr                             # Architecture Decision Records (adopted decisions)
│   ├── /plans                           # Action plans, proposals, and phased plans
│   │   └── /[plan_name]/                # Individual subfolders for folders
│   │       ├── plan.md                  # The plan itself
│   │       ├── /phases/                 # subfolders within here, one per phase, if plan needs to be broken down into multiple phases
│   │       ├── /proposed_docs           # Docs staged for integration (if adopted)
│   │       └── /archived                # Dismissed approaches with rationale
│   ├── GLOSSARY.md                      # Terminology reference (adopted terms)
│   ├── readme.md                        # This file - structure guide
└── README.md                            # Quick start and overview (main repository entry point - points to /docs/readme.md)
```


## Folder Descriptions

### `/docs/design` - Design Documentation
**Purpose**: Document the architecture, design principles, and design decisions.

**Target Audience**: Developers learning the system architecture and design rationale.

**Characteristics**:
- Stable (updated for breaking design changes only)
- Topic-organized (not phase-organized)
- High-level explanations with diagrams
- Design principles and patterns

**Structure**:
```
/docs/design
├── /xyz            # topic specific design documentation
│   └── [xyz design docs]
└── [architecture and design docs]
```

**Content Scope**:
- Specific subsystems - multitenant jwt authentication, ERP inttegrations etc 
- System design principles
- Transaction boundaries and lifecycle events
---

### `/docs/guides` - How-To Guides
**Purpose**: Practical patterns for target audiences that focus on usage or setup, rather than design or implementation

**Target Audience**: Subfolders contain the target audience name. Nesting is allowed e.g "Product/Purchase Orders/setting up config.md"
- Internal developer / contributor focused guides e.g how to build and run the solution etc goes in `/docs/guides/contributors"

**Characteristics**:
- Task-oriented ("How to...")
- Example rich or illustrative
- Updated as new idiomatic patterns evolve

---

### `/docs/reference` - API Reference
**Purpose**: Detailed specifications and API documentation.

**Target Audience**: Developers needing precise technical details.

**Characteristics**:
- Exhaustive and precise
- Generated or maintained with code
- Version-aware


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
- Concurrency scaling investigations
- Alternative approach evaluations
- Proof-of-concept findings
- Benchmark methodologies and results
- Performance comparisons (e.g., .NET vs Python)

**RESEARCH_GLOSSARY.md**
**Location**: `/docs/research/RESEARCH_GLOSSARY.md`
**Purpose**: Terminology reference for **explored but not adopted** terms.
**Status**: Active
**Updates**: Add terms for approaches explored during research that were ultimately dismissed.
**Format**: Terms marked with ❌ status, including exploration date, dismissal reason, and what superseded them.
**Rationale**: Preserves exploration history and prevents re-exploration of dismissed approaches while keeping main glossary focused on current architecture.

---

### `/docs/adr` - Architecture Decision Records
**Purpose**: Document significant architecture and design decisions following ADR format.

**Target Audience**: Developers and architects understanding why decisions were made.

**Characteristics**:
- Follows ADR format (Context, Decision, Consequences)
- Named by date and title
- Immutable once decided (new ADRs supersede old ones)
- Captures alternatives considered

**Naming Convention**: `YYYY_MM_DD_descriptive_title.md`

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

**Phase Folder Structure**:
Some plans are broken into phases for implementation. The /implementation sub folder has phase specific information with a folder per phase to track incremental pahse level objectives:
/PHASE_X_NAME/
```
/docs/plans/PLAN_NAME/
├── plan.md      
├── /phases                         # folder for phases
│   ├── /PHASE_X_NAME                # specific phase folder e.g "Phase 1"
│       ├── plan.md                  # specific plan scoped for this phase, referencing the parent plan
├── /proposed-docs/                  # Docs staged for adoption (during research)
│   ├── glossary_additions.md       # Terms to add if adopted
│   ├── adr_*.md                    # ADRs being considered
│   ├── design_*.md                 # Design docs being proposed
│   └── research_findings.md        # Research findings
└── /archived/                       # Dismissed approaches
    ├── README.md                    # Pivot rationale
    ├── adr_*_dismissed.md          # Dismissed ADRs
    └── design_*_dismissed.md       # Dismissed designs
```

**Legacy Files** (Phases 1-6):
```
/docs/** files not clearly belonging to the structure above

```

**Purpose of Phased Plans**:
- Understand design decisions in historical context
- See what was tried and why in each phase
- Reference for similar future work
- Track progression of development or implementation

---

## Documentation Files in `/docs/`

### GLOSSARY.md
**Location**: `/docs/GLOSSARY.md`

**Purpose**: Central terminology reference for **adopted** terms that are part of current domain or architecture.

**Status**: Active

**Updates**: Add terms as concepts are adopted into the codebase.

**Format**: Terms marked with ✅ status indicating they are part of current architecture.

---

## Repository-Level Documentation

### readme.md
**Location**: `/docs/readme.md`

**Purpose**: Documents the current documentation structure and organization.
**Status**: Active (this file)
**Updates**: Update when structure changes or new folders are added.

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
| `GLOSSARY.md` | New terminology | As terms are introduced |

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

Legacy documents that exist in /docs/** that don't seem to adhere to this structure should be 
migrated from root or other locations based on their primary purpose:

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
