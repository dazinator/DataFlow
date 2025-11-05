# Research Folder Structure Reference

This document defines the canonical folder structure for research projects in this repository. All research documentation should reference this document to maintain consistency.

## Standard Research Folder Structure

```
/research/[topic]/
├── research-plan.md                # Research objectives and questions
├── notes/                          # Working notes during research
│   └── exploration-notes.md
├── README.md                       # Final research documentation
├── design/                         # Design documentation
│   └── [component].md
├── benchmarks/                     # Benchmark data and analysis
│   └── benchmark-results.md
└── handover/                       # Implementation handoff materials
    ├── github-issue-[description].md
    └── prototype/                  # (Optional) Reference prototype code
        └── [code-files].cs
```

**Note**: Architecture Decision Records (ADRs) are placed in the codebase documentation folders:
- POC-related decisions: `/poc/docs/adr/YYYY-MM-DD-[decision].md`
- Production code decisions: `/src/docs/adr/YYYY-MM-DD-[decision].md`

ADRs belong with the code they govern, not in research folders.

## Path Reference Guide

For quick reference when documenting paths:

| Artifact | Path Pattern |
|----------|--------------|
| **Research folder base** | `/research/[topic]/` |
| **Research plan** | `/research/[topic]/research-plan.md` |
| **Main research doc** | `/research/[topic]/README.md` |
| **Design docs** | `/research/[topic]/design/[component].md` |
| **ADRs (POC-related)** | `/poc/docs/adr/YYYY-MM-DD-[decision].md` |
| **ADRs (production code)** | `/src/docs/adr/YYYY-MM-DD-[decision].md` |
| **Benchmarks** | `/research/[topic]/benchmarks/` |
| **Implementation issue** | `/research/[topic]/handover/github-issue-[description].md` |
| **Prototype code** (optional) | `/research/[topic]/handover/prototype/` |

## Example: Distributed Epochs Research

```
/research/distributed-epochs/
├── research-plan.md
├── README.md
├── design/
│   └── distributed-epoch-architecture.md
├── benchmarks/
│   └── coordination-benchmarks.md
└── handover/
    ├── github-issue-implement-distributed-epochs.md
    └── prototype/                  # (Optional) Key prototype code files
        ├── CoordinatorPrototype.cs
        └── ConsensusHelper.cs
```

**ADRs for this research** (POC-related, so placed with POC codebase):
```
/poc/docs/adr/
└── 2025-11-04-hybrid-epoch-coordination.md
```

## Creation Phases

The folder structure is created progressively during research:

- **Phase 1 (Planning)**: `research-plan.md`, `notes/`
- **Phase 3 (Documentation)**: `README.md`, `benchmarks/`
- **Phase 4 (Supporting Docs)**: `design/`, **ADRs in `/poc/docs/adr/` or `/src/docs/adr/`**
- **Phase 5 (Handoff)**: `handover/`

## Usage in Documentation

When referencing the research folder structure in other documents:

```markdown
See [Research Folder Structure](FOLDER_STRUCTURE.md) for the complete folder layout.
```

Or for specific paths:

```markdown
Research documentation: See [path reference](FOLDER_STRUCTURE.md#path-reference-guide)
```

## Updating This Document

This is the **single source of truth** for research folder structure. When making changes:

1. Update this document first
2. Update references in other documents
3. Verify all documentation remains consistent

**Related Documents:**
- [Research Workflow](/research/RESEARCH_WORKFLOW.md) - Complete workflow process
- [Implementation Issue Template](/research/IMPLEMENTATION_ISSUE_TEMPLATE.md) - Handoff issue format
- [Implementation Issue Example](/poc/docs/plans/IMPLEMENTATION_ISSUE_EXAMPLE.md) - Complete example
