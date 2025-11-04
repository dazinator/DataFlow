# GitHub Copilot Instructions for POC Work

⚠️ **POC Context**: This document provides guidelines for working on issues related to the Proof-of-Concept (POC) work under the `/poc` folder.

## Quick Reference

When assigned to a POC-related issue, you should:
- Read the POC README and documentation structure first
- Maintain appropriate documentation as you work
- Validate designs through testing and benchmarking
- Track key decisions and findings

## POC Documentation Structure

The POC has a well-organized documentation structure under `/poc/docs/`:

```
/poc
├── /docs                                # All documentation organized by purpose
│   ├── /design                          # Core design documentation and architecture
│   ├── /guides                          # How-to guides and implementation patterns
│   ├── /reference                       # API reference and specifications
│   ├── /research                        # Exploratory documentation, investigations, and performance analysis
│   ├── /adr                             # Architecture Decision Records
│   ├── /plans                           # Action plans, proposals, and phased plans
│   ├── POC_GLOSSARY.md                  # Terminology reference
│   ├── POC_DOCUMENTATION_STRUCTURE.md   # Structure guide
│   └── INDEX.md                         # Navigation hub
└── README.md                            # Quick start and overview (entry point)
```

**Key Documents to Read First**:
- `/poc/README.md` - POC overview and goals
- `/poc/docs/POC_DOCUMENTATION_STRUCTURE.md` - How documentation is organized
- `/poc/docs/POC_GLOSSARY.md` - Key terminology
- `/poc/docs/INDEX.md` - Navigation hub for all documentation

## Workflow for POC Issues

### 1. Initial Understanding
Before starting any POC work:
- Read `/poc/README.md` to understand the POC goals and architecture
- Review `/poc/docs/` to understand existing designs and decisions
- Check `/poc/docs/POC_GLOSSARY.md` for important terminology
- Look at relevant documentation in `/poc/docs/design/` for architectural context

### 2. Planning Phase
Create and maintain a plan document:
- **Location**: `/poc/docs/plans/`
- **Naming**: Use a descriptive name (e.g., `implement-xyz-feature.md`)
- **Content**:
  - Clear objectives and scope
  - Approach and implementation strategy
  - Key assumptions and alternatives
  - Milestones with validation criteria
  - Dependencies and prerequisites
  - Testing and benchmarking requirements

**Plan Template Structure**:
```markdown
# Plan: [Feature/Issue Name]

## Objective
[Clear statement of what needs to be accomplished]

## Approach
[High-level strategy]

## Key Assumptions
[List any assumptions that could affect the approach]

## Alternative Approaches
[Document alternatives considered and why current approach was chosen]

## Milestones
- [ ] Milestone 1 - [Description]
  - Validation: [How to verify completion]
- [ ] Milestone 2 - [Description]
  - Validation: [How to verify completion]

## Testing Strategy
[How this will be tested]

## Benchmarking Requirements
[Performance validation needed, if applicable]
```

### 3. Documentation During Development

As you implement the solution, maintain appropriate documentation:

#### Design Documentation (`/poc/docs/design/`)
- **When**: For new architectural concepts or significant design decisions
- **What**: Architecture, design principles, system concepts
- **Style**: High-level explanations with diagrams, stable documentation
- **Example topics**: New block types, epoch handling patterns, transaction boundaries

#### Research Documentation (`/poc/docs/research/`)
- **When**: Conducting investigations, explorations, or performance analysis
- **What**: Exploratory findings, benchmark results, approaches tried
- **Style**: Document question, approaches, findings, recommendations
- **Examples**: 
  - Performance investigations
  - Alternative approach evaluations
  - Benchmark methodologies and results
  - Proof-of-concept findings

**Benchmark Documentation Requirements**:
- Document methodology clearly
- Include environment details (hardware, .NET version, etc.)
- Make results reproducible (include commands, configuration)
- Explain what is being measured and why
- Save benchmark output in the research folder
- Reference benchmarks from your plan document

#### Implementation Guides (`/poc/docs/guides/`)
- **When**: Creating reusable patterns or "how-to" documentation
- **What**: Practical implementation patterns with complete examples
- **Style**: Task-oriented ("How to..."), code-heavy
- **Examples**: Integration guides, testing strategies, common recipes

#### Architecture Decision Records (`/poc/docs/adr/`)
- **When**: Making significant architecture or design decisions
- **What**: Formal decision record following ADR format
- **Naming**: `YYYY-MM-DD-descriptive-title.md`
- **Structure**:
  - Context: Why is this decision needed?
  - Decision: What was decided?
  - Alternatives Considered: What other options were evaluated?
  - Consequences: What are the implications?

### 4. Glossary Maintenance
Update `/poc/docs/POC_GLOSSARY.md` when:
- Introducing new important terminology
- Defining key concepts central to the POC
- Clarifying ambiguous terms

**Good glossary entries**:
- Clear, concise definitions
- Examples where helpful
- Cross-references to related terms
- Links to detailed documentation

### 5. Validation Requirements

Testing and benchmarking are critical milestones:

#### Testing
- Create tests in POC test projects (`DataFlow.POC.Tests`)
- Validate each milestone as defined in your plan
- Ensure tests cover edge cases and failure scenarios
- Document test results in your plan

#### Benchmarking
- Use POC benchmark projects (`DataFlow.POC.Benchmarks`)
- Run benchmarks for performance-sensitive changes
- Document methodology and environment
- Save benchmark output to `/poc/docs/research/`
- Compare against baseline where applicable
- Reference benchmark results from your plan

### 6. Progress Tracking

Keep your plan document updated:
- Mark milestones as complete when validated
- Document any deviations from original plan
- Note any unexpected findings or issues
- Update with links to research documents, benchmarks, ADRs

## Documentation Organization Guidelines

### Where to Put What

| Content Type | Location | When to Create |
|-------------|----------|----------------|
| Action plans and proposals | `/poc/docs/plans/` | Start of work on any POC issue |
| Architectural concepts | `/poc/docs/design/` | New design patterns or major changes |
| Implementation patterns | `/poc/docs/guides/` | Reusable "how-to" documentation |
| Performance analysis | `/poc/docs/research/` | After benchmarking |
| Investigations/explorations | `/poc/docs/research/` | During discovery/research phase |
| Architecture decisions | `/poc/docs/adr/` | When making significant decisions |
| New terminology | `/poc/docs/POC_GLOSSARY.md` | When introducing new concepts |

### Avoid Duplication
- Link to existing documentation rather than duplicating
- Reference design docs from guides
- Link to research/benchmarks from plans
- Cross-reference related documents

## Best Practices

### Plan-First Approach
1. Create plan document before coding
2. Identify research/benchmarking needs early
3. Document assumptions that need validation
4. Plan for milestone-based validation

### Incremental Documentation
- Document as you go, not after completion
- Update plan with findings during implementation
- Create research documents during investigations
- Write ADRs when decisions are made, not later

### Testing and Validation
- Treat testing/benchmarking as first-class milestones
- Don't skip validation steps
- Document unexpected results
- Keep benchmark output for future reference

### Iterative Refinement
- Plans can evolve based on findings
- Document why you deviated from original plan
- Learning is part of the process - capture it

## Referencing from GitHub Issues

To have GitHub Copilot follow these POC guidelines, you can:

1. **Reference this file directly in issue description**:
   ```markdown
   @copilot Please follow the POC workflow guidelines in `.github/copilot-instructions-poc.md`
   ```

2. **Use shorthand in comments**:
   ```markdown
   @copilot This is a POC issue - please follow the POC guidelines
   ```

3. **Include specific sections**:
   ```markdown
   @copilot Please follow the POC documentation workflow:
   - Create a plan in /poc/docs/plans/
   - Document research in /poc/docs/research/
   - Update the glossary as needed
   ```

GitHub Copilot will automatically read both the main `copilot-instructions.md` and this POC-specific file.

## Example Workflow

Here's a concrete example of working on a POC issue:

**Issue**: "Implement epoch-based caching mechanism"

**Workflow**:
1. **Read** `/poc/README.md`, `/poc/docs/POC_GLOSSARY.md` (understand epochs)
2. **Review** existing design docs in `/poc/docs/design/` related to epochs
3. **Create** `/poc/docs/plans/implement-epoch-caching.md` with:
   - Objectives
   - Approach (considered alternatives)
   - Milestones with validation criteria
   - Testing and benchmarking requirements
4. **Investigate** different caching strategies:
   - Document findings in `/poc/docs/research/caching-strategy-investigation.md`
5. **Benchmark** candidate approaches:
   - Run benchmarks in `DataFlow.POC.Benchmarks`
   - Save results to `/poc/docs/research/epoch-caching-benchmarks.md`
6. **Decide** on approach:
   - Create ADR in `/poc/docs/adr/2025-11-03-epoch-based-cache-design.md`
7. **Implement** with tests in `DataFlow.POC.Tests`
8. **Document** pattern:
   - Create guide in `/poc/docs/guides/using-epoch-cache.md`
9. **Update** glossary if new terms introduced
10. **Complete** plan document with results and references

## Tips for Success

- **Start with the plan** - it guides everything else
- **Document research early** - capture findings while fresh
- **Validate through testing** - don't assume, verify
- **Keep glossary current** - helps everyone understand concepts
- **Link documents together** - create a web of knowledge
- **Benchmarks matter** - POC focuses on design validation
- **ADRs capture decisions** - document the "why" not just the "what"

## See Also

- Main Copilot Instructions: `.github/copilot-instructions.md`
- POC Overview: `/poc/README.md`
- Documentation Structure: `/poc/docs/POC_DOCUMENTATION_STRUCTURE.md`
- Navigation Hub: `/poc/docs/INDEX.md`
