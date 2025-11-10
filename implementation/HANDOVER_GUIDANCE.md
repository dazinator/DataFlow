# Implementation Handover Guidance

This document defines what research teams must provide when handing off work to implementation teams, ensuring handovers are complete, actionable, and set up for success.

---

## Purpose

Implementation handovers bridge research (validation and exploration) with implementation (production delivery). A well-crafted handover enables the implementation team to work independently without needing to consult the research team.

## Core Handover Requirements

Every implementation handover **MUST** include:

### 1. Problem Context
- **What**: Clear problem statement
- **Why**: Business/technical need
- **Background**: Research that validated the approach

### 2. Implementation Guidance
- **Recommended Approach**: High-level architecture validated by research
- **Key Principles**: Design principles from research
- **API/Interface Design**: Proposed public contracts
- **Component Architecture**: Structure and interactions

### 3. Critical Implementation Considerations
- Points discovered during research that affect implementation
- Edge cases and how to handle them
- Integration points with existing code
- Reusable patterns/code from prototypes

### 4. Test Coverage Requirements
- Unit test scenarios (with setup, expected, and rationale)
- Integration test scenarios
- Edge cases to test
- Performance validation requirements

### 5. Alternatives Explored
- What approaches were considered
- Why they weren't chosen
- Trade-offs understood

### 6. Supporting Documentation
- Links to research findings, design docs, ADRs
- Prototype code (if applicable)
- Benchmark data and methodologies

### 7. Documentation Deliverables Checklist

Specify what documentation the implementation team should create:

- [ ] Usage guide for new utilities/features (with examples)
  - Specify scope: "Comprehensive guide (>10KB)" vs "Quick start guide (<3KB)"
  - Specify audience: End users vs Contributors vs Both
- [ ] Pattern/best practices guide (if applicable)
- [ ] README for new directories/modules
- [ ] Navigation file updates (e.g., `/poc/docs/INDEX.md`)

### 8. Example Tests Guidance

Specify what example/demo tests to include:

- **Minimum number**: "Include 3-5 example tests showing key usage patterns"
- **Focus**: Common patterns rather than exhaustive feature coverage
- **Pattern**: Suggest "before/after comparison" tests to demonstrate improvements
- **Purpose**: Examples should be clear, well-documented, and representative

---

## Multi-Phase Implementation Assessment

**CRITICAL**: Instead of providing time estimates, research teams must assess whether implementation would benefit from a multi-phase approach.

### When to Recommend Multiple Phases

Consider recommending phases when:
- **Volume of changes**: Affects many files (>10-15 files)
- **Size of PRs**: Changes would create very large PR (>500 lines)
- **Logical separation**: Natural break points exist (foundation → migration → cleanup)
- **Risk management**: Staged rollout reduces risk
- **Review complexity**: Smaller PRs are easier to review
- **Dependency sequencing**: Some changes must happen before others
- **Incremental value**: Each phase delivers value independently

### Assessment Format

In the handover document, include:

```markdown
## Multi-Phase Implementation Assessment

**Recommendation**: [ ] Single-Phase  [ ] Multi-Phase

### Rationale
[Explain why single-phase or multi-phase is recommended]

**If Multi-Phase:**

### Suggested Phases

**Phase 1: [Name]** (e.g., Foundation)
- **Objective**: [What this phase accomplishes]
- **Deliverables**: [Key outputs]
- **Files/Areas Affected**: [Scope]
- **Why First**: [Rationale for sequencing]

**Phase 2: [Name]** (e.g., Migration)
- **Objective**: [What this phase accomplishes]
- **Deliverables**: [Key outputs]
- **Files/Areas Affected**: [Scope]
- **Dependencies**: [What must be complete first]

**Phase 3: [Name]** (e.g., Cleanup) - *Optional*
- **Objective**: [What this phase accomplishes]
- **Deliverables**: [Key outputs]
- **Files/Areas Affected**: [Scope]

### Phasing Benefits
- [Benefit 1: e.g., "Each phase is independently reviewable"]
- [Benefit 2: e.g., "Reduces risk by validating foundation before migration"]
- [Benefit 3: e.g., "Allows incremental rollout if issues discovered"]
```

### Single-Phase Indicators

Recommend single-phase when:
- Changes are localized (< 10 files)
- Clear, atomic change
- Low complexity
- Can be reviewed in one PR (< 500 lines)
- No natural break points
- Tight coupling makes separation difficult

**Format**:
```markdown
## Multi-Phase Implementation Assessment

**Recommendation**: [x] Single-Phase  [ ] Multi-Phase

### Rationale
Changes are localized to [area], affecting [N] files with clear, atomic objectives. 
No natural break points exist, and the changes are tightly coupled. Implementation 
should be completed in a single PR for coherence.

**Recommended Approach**: Implement all changes together in one PR, ensuring 
[specific validation/testing] before merge.
```

---

## Assessment Factors

When assessing for multi-phase recommendation, consider:

### Volume Factors
- **File Count**: >10-15 files → likely multi-phase
- **Line Changes**: >500 lines → likely multi-phase
- **Test Impact**: Many test files need updates → consider phases

### Complexity Factors
- **Architectural Changes**: Breaking changes → foundation phase first
- **Migration Required**: Existing code must update → migration phase
- **Deprecation Path**: Old code removal → cleanup phase

### Risk Factors
- **Breaking Changes**: Stage to validate foundation first
- **Performance Impact**: Benchmark between phases
- **Wide Impact**: Many consumers → careful rollout

### Review Factors
- **Cognitive Load**: Can reviewer understand all changes in one PR?
- **Context Switching**: Multiple distinct concerns → separate phases
- **Validation Points**: Natural checkpoints → phase boundaries

---

## Template Integration

### In Research Handover Issues

Add this section after "Implementation Guidance":

```markdown
## Multi-Phase Implementation Assessment

[Use format from "Assessment Format" section above]
```

### In Tech Debt Findings

For each finding in the findings report:

```markdown
**Multi-Phase Recommendation**: [Single/Multi]
**Rationale**: [Brief explanation]
**Suggested Phases** (if Multi): [High-level phase outline]
```

---

## Implementation Team Usage

### For Single-Phase Handovers

1. Read handover document completely
2. Verify you understand all requirements
3. Implement in single PR
4. Reference handover in PR description

### For Multi-Phase Handovers

1. Read handover document completely
2. Create `/implementation/plan.md` based on suggested phases
3. Adjust phases if needed based on implementation insights
4. Implement phase-by-phase
5. Update plan.md as each phase completes
6. Reference handover and plan in each PR

---

## Examples

### Example 1: Single-Phase Handover

```markdown
## Multi-Phase Implementation Assessment

**Recommendation**: [x] Single-Phase  [ ] Multi-Phase

### Rationale
This implementation adds nullable reference warnings support to 3 core files in 
the DataFlow library. Changes are:
- Focused on single concern (null safety)
- Affect only 3 files (~150 lines total)
- Can be validated in one test run
- No breaking changes

The atomic nature of these changes makes a single-phase implementation appropriate.
```

### Example 2: Multi-Phase Handover

```markdown
## Multi-Phase Implementation Assessment

**Recommendation**: [ ] Single-Phase  [x] Multi-Phase

### Rationale
Consolidating plain blocks with actor blocks affects 50+ files across POC and tests. 
The volume of changes, combined with the need for validation between stages, makes 
a phased approach essential for:
- Manageable PR sizes (<500 lines each)
- Independent validation of each stage
- Risk reduction through incremental rollout
- Clear reviewer focus per phase

### Suggested Phases

**Phase 1: Foundation** (No Breaking Changes)
- **Objective**: Add new unified API without breaking existing code
- **Deliverables**:
  - New `ActorBlock<TIn, TOut>` with unified interface
  - Obsolete attributes on old APIs with migration guidance
  - All existing tests still pass
- **Files/Areas Affected**: Core block definitions (5-7 files)
- **Why First**: Validates new API design without risk to existing code

**Phase 2: Test Migration** (Incremental)
- **Objective**: Migrate test suite to new API
- **Deliverables**:
  - All POC tests use new API
  - Obsolete warnings addressed
  - Performance benchmarks migrated
- **Files/Areas Affected**: Test files (20-30 files)
- **Dependencies**: Phase 1 must be complete (new API available)

**Phase 3: Cleanup** (Optional)
- **Objective**: Remove deprecated APIs after validation period
- **Deliverables**:
  - Old block types removed
  - Codebase simplified
- **Files/Areas Affected**: Core implementations (5-7 files)
- **Dependencies**: Phase 2 complete, validation period passed

### Phasing Benefits
- Each phase is independently testable (< 400 lines per PR)
- Foundation validated before migration begins
- Can pause after Phase 2 if issues discovered
- Clearer review focus: "Does foundation work?" then "Did migration work?"
```

---

## Anti-Patterns to Avoid

### ❌ Vague Phasing
**Bad**: "Phase 1: Do some stuff. Phase 2: Do more stuff."
**Good**: Each phase has clear objectives, deliverables, and success criteria

### ❌ Over-Phasing
**Bad**: 10 phases for a 200-line change
**Good**: 2-3 phases maximum unless truly necessary

### ❌ Arbitrary Boundaries
**Bad**: Phase boundaries based on "I worked 4 hours"
**Good**: Phase boundaries based on logical completion points

### ❌ Missing Rationale
**Bad**: Just saying "multi-phase" without explaining why
**Good**: Clear reasoning for why phases help (review, risk, validation, etc.)

---

## Summary

**Research teams provide**:
- Multi-phase assessment (not time estimates)
- Clear rationale for recommendation
- Suggested phases if multi-phase recommended
- All required context and guidance

**Implementation teams use**:
- Assessment to decide whether to create plan.md
- Suggested phases as starting point (adjust as needed)
- Handover guidance for complete context

**Result**: Better handovers that set implementation teams up for success without requiring time estimates that are often inaccurate.

---

**Related Documentation**:
- [Implementation Workflow](/.team/prompts/IMPLEMENTATION_WORKFLOW.md)
- [Research Workflow](/.team/prompts/RESEARCH_WORKFLOW.md)
- [Implementation Issue Template](/research/IMPLEMENTATION_ISSUE_TEMPLATE.md)
