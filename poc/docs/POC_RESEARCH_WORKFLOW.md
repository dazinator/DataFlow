# POC Research and Exploration Workflow

This document describes the recommended workflow for conducting research and exploration work within the POC. It addresses how to handle pivots, preserve exploration history, and maintain clean separation between exploratory and production-ready code.

## Problem Statement

During POC research, pivots are common as we explore different approaches and discover issues. We need a methodology that:
1. **Preserves exploration knowledge** - Don't lose what was tried and why
2. **Keeps main docs clean** - Avoid polluting production docs with dismissed approaches
3. **Maintains working exploratory code** - Keep explorations compilable and testable for future reference
4. **Clear separation** - Distinguish exploratory code from production-track code

## Research Workflow

### Phase 1: Create Plan Folder Structure

When starting a new research phase or significant exploration:

```bash
/poc/docs/plans/PHASE_X_DESCRIPTION/
├── plan.md                     # The exploration plan itself
├── proposed-docs/              # Documentation intended for integration if adopted
│   ├── glossary-additions.md  # → Merge to POC_GLOSSARY.md (if adopted)
│   ├── adr-*.md               # → Move to /adr/ folder (if adopted)
│   ├── design-*.md            # → Move to /design/ folder (if adopted)
│   └── research-findings.md   # → Move to /research/ folder (always valuable)
└── archived/                   # Dismissed/pivoted artifacts
    ├── README.md              # Explains what was dismissed and why
    ├── adr-*-dismissed.md     # ADRs for approaches not adopted
    └── design-*-dismissed.md  # Design docs for approaches not adopted
```

### Phase 2: Draft Proposed Documentation

As your plan evolves, create documentation in `proposed-docs/`:

**Glossary Additions** (`glossary-additions.md`):
```markdown
## Terms to Add to Main Glossary
### NewTerm ✅ (RECOMMENDED FOR ADOPTION)
[Definition and details]

## Terms to Add to Research Glossary
### ExploratoryTerm ❌ (Phase X - Not Adopted)
[Definition and dismissal reason]
```

**ADRs** (`adr-approach-name.md`):
- Document significant architectural decisions being considered
- Use standard ADR format
- Keep status as "Proposed" until decision is made

**Design Docs** (`design-component-name.md`):
- Document designs for new components being explored
- Include diagrams, API examples, trade-offs

**Research Findings** (`research-findings.md`):
- Always valuable, documents both approaches explored
- Comparative analysis
- Performance benchmarks
- Lessons learned

### Phase 3: Create Exploratory Code and Tests

Keep exploratory code **functional and separate** from production-track code.

#### Exploratory Tests Location

```
/poc/DataFlow.POC.Tests/
├── Exploratory/                    # All exploratory tests
│   └── Phase7EventChannel/         # Phase-specific exploratory work
│       ├── EventChannelNodeTests.cs
│       └── EventEdgeStrategiesTests.cs
├── Integration/                     # Production-track tests
├── Unit/                           # Production-track tests
└── ...
```

**Mark tests as exploratory**:
```csharp
[Category("Exploratory")]
[Category("Phase7")]
public class EventChannelNodeTests
{
    // Tests for approach being explored
}
```

#### Exploratory Code Location

```
/poc/DataFlow.POC/
├── Core/                           # Production-track code
├── Exploratory/                    # All exploratory implementations
│   └── Phase7EventChannel/         # Phase-specific exploratory work
│       ├── EventChannelNode.cs
│       └── EventEdgeStrategies.cs
└── ...
```

**Benefits**:
- ✅ Exploratory code remains **compilable and testable**
- ✅ Clearly separated from production track
- ✅ Can run exploratory tests independently: `dotnet test --filter Category=Exploratory`
- ✅ Easy to promote to production: just move files out of `Exploratory/`
- ✅ Can exclude from production builds if needed

#### Non-Functional Reference Code

For code that won't compile or is purely for reference:

```
/poc/docs/research/phase-X-exploration/
├── README.md                       # Explains the exploration
├── EventChannelNode.cs             # Reference implementation
└── EventEdgeStrategies.cs          # Reference implementation
```

Use this for:
- Code snippets that don't compile standalone
- Partial implementations
- Pseudo-code
- External reference code

### Phase 4: Handle Pivots

When you decide to pivot from an approach:

1. **Move dismissed docs to archived**:
   ```bash
   mv proposed-docs/adr-approach.md archived/adr-approach-dismissed.md
   mv proposed-docs/design-component.md archived/design-component-dismissed.md
   ```

2. **Add dismissal banner** to archived documents:
   ```markdown
   # ❌ Component Design (DISMISSED)
   
   > **⚠️ ARCHIVED**: This design was explored during Phase X but was NOT ADOPTED.
   > **Pivot Date**: YYYY-MM-DD
   > **Reason**: [Brief reason for dismissal]
   > **Alternative Adopted**: [What was chosen instead]
   > **See**: [Link to archived README for full rationale]
   ```

3. **Create archived/README.md** explaining the pivot:
   - What was explored
   - Why it was dismissed
   - What was adopted instead
   - Where to find related code/docs

4. **Keep exploratory code in place** (don't delete):
   - Remains in `/Exploratory/` folders
   - Still compilable and testable
   - Marked with `[Category("Exploratory")]`
   - Future reference and learning

5. **Update glossary-additions.md**:
   - Move dismissed terms to "Research Glossary" section
   - Mark with ❌ status

### Phase 5: Finalize and Promote (After PR Approval)

Once the PR is approved and approach is adopted:

1. **Promote adopted documentation**:
   ```bash
   # Glossary
   # (Merge glossary-additions.md content into /docs/POC_GLOSSARY.md)
   
   # ADRs
   mv proposed-docs/adr-adopted-approach.md /docs/adr/YYYY-MM-DD-adopted-approach.md
   
   # Design
   mv proposed-docs/design-component.md /docs/design/component.md
   
   # Research findings (always valuable)
   mv proposed-docs/research-findings.md /docs/research/phase-x-findings.md
   ```

2. **Promote adopted code**:
   ```bash
   # Move from Exploratory to production
   mv DataFlow.POC/Exploratory/PhaseX/* DataFlow.POC/Core/
   mv DataFlow.POC.Tests/Exploratory/PhaseX/* DataFlow.POC.Tests/Integration/
   
   # Remove [Category("Exploratory")] attributes
   ```

3. **Update plan status**:
   - Mark plan as complete
   - Document final outcomes
   - Update phase index

4. **Keep archived folder**:
   - Don't delete dismissed exploration artifacts
   - Valuable for understanding why decisions were made
   - Helps avoid repeating past explorations

## Documentation Glossaries

### Main Glossary (`/docs/POC_GLOSSARY.md`)
**Purpose**: Terms that are **adopted** and part of current POC architecture

**Format**:
```markdown
### TermName
**Category**: [Block/Node/Pattern/etc]
**Phase**: X
**Status**: ✅ Adopted

[Definition and usage]
```

### Research Glossary (`/docs/RESEARCH_GLOSSARY.md`)
**Purpose**: Terms that were **explored but not adopted**

**Format**:
```markdown
### TermName ❌
**Explored**: YYYY-MM-DD
**Phase**: X
**Status**: Not Adopted
**Reason**: [Why dismissed]
**Reference**: [Link to archived docs]

[Definition and why it was dismissed]

**Superseded By**: [Alternative that was adopted]
```

## Benefits of This Workflow

1. **Preserves Knowledge**: All exploration work is documented and accessible
2. **Clean Production Docs**: Main documentation only contains adopted approaches
3. **Clear Separation**: Easy to distinguish exploratory from production code
4. **Working Exploratory Code**: Explorations remain compilable and testable
5. **Easy Promotion**: Simple to move adopted work to production locations
6. **Efficient Review**: Reviewers can focus on `proposed-docs/` for what's being added
7. **Historical Context**: Future developers understand why decisions were made
8. **Avoids Repetition**: Team won't re-explore dismissed approaches

## Example: Phase 7 EventChannelNode

**Initial Structure** (during exploration):
```
/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/
├── plan.md
├── proposed-docs/
│   ├── glossary-additions.md
│   ├── adr-event-type-handling.md
│   ├── design-event-channel.md
│   └── research-findings.md
/poc/DataFlow.POC/Exploratory/Phase7EventChannel/
├── EventChannelNode.cs
└── EventEdgeStrategies.cs
/poc/DataFlow.POC.Tests/Exploratory/Phase7EventChannel/
└── EventChannelNodeTests.cs
```

**After Pivot** (channel approach dismissed):
```
/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/
├── plan.md (updated with pivot decision)
├── proposed-docs/
│   ├── glossary-additions.md (updated: EventChannelNode → Research Glossary)
│   └── research-findings.md (documents both approaches)
├── archived/
│   ├── README.md (explains pivot)
│   ├── adr-event-type-handling-dismissed.md
│   └── design-event-channel-dismissed.md
/poc/DataFlow.POC/Exploratory/Phase7EventChannel/
├── EventChannelNode.cs (remains for reference)
└── EventEdgeStrategies.cs (remains for reference)
/poc/DataFlow.POC.Tests/Exploratory/Phase7EventChannel/
└── EventChannelNodeTests.cs (remains for reference)
```

**After PR Approval** (hybrid approach adopted):
```
# Promoted to main docs:
/poc/docs/POC_GLOSSARY.md (+ EpochLifecycleNode term)
/poc/docs/RESEARCH_GLOSSARY.md (+ EventChannelNode term marked ❌)
/poc/docs/research/phase7-lifecycle-event-visibility-findings.md

# Promoted code:
/poc/DataFlow.POC/Core/EpochLifecycleNode.cs (new hybrid approach)
/poc/DataFlow.POC.Tests/Integration/EpochLifecycleNodeTests.cs

# Archived stays in place:
/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/ (preserved)
/poc/DataFlow.POC/Exploratory/Phase7EventChannel/ (preserved for reference)
```

## Integration with GitHub Issues

When creating GitHub issues for POC work, include this workflow guidance:

```markdown
## Documentation Workflow

This task follows the POC Research Workflow. See `/poc/docs/POC_RESEARCH_WORKFLOW.md`.

**During Research**:
1. Create plan folder: `/poc/docs/plans/PHASE_X_DESCRIPTION/`
2. Draft docs in `proposed-docs/` as work evolves
3. Keep exploratory code in `/Exploratory/` folders with `[Category("Exploratory")]`
4. On pivots: move dismissed docs to `archived/` with reasons

**On PR Approval**:
1. Promote adopted docs from `proposed-docs/` to main doc locations
2. Promote adopted code from `/Exploratory/` to production locations
3. Keep archived materials for historical reference

This preserves exploration history while keeping production docs and code clean.
```

## Summary

This workflow solves the "docs pollution from pivots" problem by:
- Staging proposed docs near the plan until PR approval
- Archiving dismissed approaches with clear explanations
- Keeping exploratory code functional but separate
- Making promotion to production explicit and intentional

Result: **Clean production codebase and docs, with full exploration history preserved.**
