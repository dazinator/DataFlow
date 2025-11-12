# Documentation Artifacts System

This document describes the global documentation artifacts system for creating supporting documentation (analysis, design, and architectural decisions) for GitHub issues.

## Overview

The repository uses a centralized `/docs/` structure for formal documentation that supports long-term understanding and decision-making. This is separate from temporary research work which goes in `/research/[topic]/`.

## Documentation Types

### 1. Analysis Documents (`/docs/analysis/`)

**Purpose**: Investigation and discovery

**Use for**:
- Problem root cause analysis
- Performance benchmarks and comparisons
- Technology evaluations
- Exploratory research
- Tech debt investigations

**Structure**:
```
/docs/analysis/<topic>/
  README.md          # Main analysis document
  benchmarks/        # Performance data (if applicable)
  comparisons/       # Comparison tables (if applicable)
  findings/          # Supporting documents
```

**Template**: Use "Analysis Document" issue template

**What to include**:
1. Context and motivation
2. Observations and metrics
3. Alternative approaches
4. Outcome and conclusions
5. Link to design (if analysis leads to solution)

### 2. Design Documents (`/docs/design/`)

**Purpose**: Solution proposals and plans

**Use for**:
- New feature designs
- Remediation plans for tech debt
- Refactoring proposals
- Architecture changes

**Structure**:
```
/docs/design/<topic>/
  README.md          # Main design document
  diagrams/          # Architecture diagrams (if applicable)
  prototypes/        # Code prototypes (if applicable)
  alternatives/      # Alternative approaches
```

**Template**: Use "Design Document" issue template

**What to include**:
1. Objective
2. Constraints
3. Proposed architecture (with diagrams)
4. Impacted components
5. Linked analysis or ADRs

### 3. Architecture Decision Records (`/docs/adr/`)

**Purpose**: Recording architectural decisions

**Use for**:
- Technology choices
- Design pattern decisions
- Major refactoring approaches
- Cross-cutting concerns

**File naming**: `YYYY-MM-DD-short-title.md`

**POC decisions**: `/docs/adr/poc/YYYY-MM-DD-title.md`

**Template**: Use "ADR" issue template or `/docs/adr/TEMPLATE.md`

**What to include**:
1. Date and status
2. Context
3. Options considered
4. Decision and rationale
5. Consequences
6. Related documentation links

## When to Use Each Type

Quick decision guide:

- **Starting investigation?** → Create Analysis document
- **Proposing solution?** → Create Design document
- **Made architectural decision?** → Create ADR

## Structure and Linking

### File Paths

Each document type has its own folder structure:

```
/docs/analysis/<topic>/README.md
/docs/design/<topic>/README.md
/docs/adr/YYYY-MM-DD-title.md
/docs/adr/poc/YYYY-MM-DD-title.md
```

### Cross-Linking Conventions

**From issues to docs**:
```markdown
See analysis: [Topic](../../docs/analysis/<topic>/README.md)
See design: [Feature](../../docs/design/<topic>/README.md)
See ADR: [Decision](../../docs/adr/YYYY-MM-DD-title.md)
```

**From docs to issues**:
```markdown
Related issue: uniun-technology/lib-dataflow#123
```

**Between documentation types**:
- Analysis → Design: `See design: [Name](../design/<topic>/README.md)`
- Design → Analysis: `Based on analysis: [Name](../analysis/<topic>/README.md)`
- Design → ADR: `Decision recorded in ADR: [Title](../adr/YYYY-MM-DD-title.md)`

## Other Documentation Locations

- **Workflow research/temporary work** → `/research/[topic]/`
- **POC-specific implementation guides** → `/poc/docs/guides/`
- **Production user-facing docs** → `/docs/` (root level files)
- **Module/directory READMEs** → In the directory itself

## Detailed Guidance

For complete guidance on each documentation type, see:

- `/docs/analysis/README.md` - Analysis document structure and conventions
- `/docs/design/README.md` - Design document structure and conventions
- `/docs/adr/README.md` - ADR structure and conventions

## Issue Templates

Use the following issue templates to create documentation:

- **Analysis Document** - Creates structure for investigation
- **Design Document** - Creates structure for solution proposal
- **ADR** - Creates structure for architectural decision

These templates are available in `.github/ISSUE_TEMPLATE/`.
