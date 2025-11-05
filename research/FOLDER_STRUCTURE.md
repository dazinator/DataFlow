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
├── adr/                            # Architecture Decision Records
│   └── YYYY-MM-DD-[decision].md
├── benchmarks/                     # Benchmark data and analysis
│   └── benchmark-results.md
└── handover/                       # Implementation handoff materials
    └── github-issue-[description].md
```

## Path Reference Guide

For quick reference when documenting paths:

| Artifact | Path Pattern |
|----------|--------------|
| **Research folder base** | `/research/[topic]/` |
| **Research plan** | `/research/[topic]/research-plan.md` |
| **Main research doc** | `/research/[topic]/README.md` |
| **Design docs** | `/research/[topic]/design/[component].md` |
| **ADRs** | `/research/[topic]/adr/YYYY-MM-DD-[decision].md` |
| **Benchmarks** | `/research/[topic]/benchmarks/` |
| **Implementation issue** | `/research/[topic]/handover/github-issue-[description].md` |

## Example: Distributed Epochs Research

```
/research/distributed-epochs/
├── research-plan.md
├── README.md
├── design/
│   └── distributed-epoch-architecture.md
├── adr/
│   └── 2025-11-04-hybrid-epoch-coordination.md
├── benchmarks/
│   └── coordination-benchmarks.md
└── handover/
    └── github-issue-implement-distributed-epochs.md
```

## Creation Phases

The folder structure is created progressively during research:

- **Phase 1 (Planning)**: `research-plan.md`, `notes/`
- **Phase 3 (Documentation)**: `README.md`, `benchmarks/`
- **Phase 4 (Supporting Docs)**: `design/`, `adr/`
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
