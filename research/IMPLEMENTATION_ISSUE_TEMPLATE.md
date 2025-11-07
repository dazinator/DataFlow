# Implementation Issue Template

This template is used to create **implementation-ready GitHub issues** after POC research is complete. 

## What Makes an Issue "Implementation-Ready"?

An implementation-ready issue contains **all the context and guidance needed for an engineering team to independently implement the solution** without needing to consult the researcher. It includes:

- Complete problem context and research background
- Clear implementation guidance with architectural approach
- API/interface designs discovered during research
- Comprehensive test scenarios and edge cases
- Performance requirements and validation benchmarks
- All alternatives explored and why they were not chosen
- References to supporting documentation (research, design, ADRs)

**Goal**: A developer should be able to implement the solution by reading the issue and supporting docs alone, understanding both the "what" and the "why."

## When to Use This Template

Use this template when:
- POC research has validated an approach
- Implementation will occur in the main codebase (`src/`)
- You need to hand off comprehensive specifications to an implementation team
- Research documentation and supporting docs are complete

## Template

```markdown
# [Feature/Component Name]

## Context and Objectives

### Problem Statement
[What problem does this solve? What is the business/technical need?]

### Research Background
Research was conducted to validate the approach and inform this implementation.

**Research Documentation**: See research folder (structure defined in `/research/FOLDER_STRUCTURE.md`)
**Key Research Artifacts**:
- Main findings: `/research/[topic]/README.md`
- Design docs: `/research/[topic]/design/`
- ADRs: `/research/[topic]/adr/`
- Benchmarks: `/research/[topic]/benchmarks/`

Key findings from research:
- Finding 1
- Finding 2
- Finding 3

### Objectives
What this implementation should achieve:
- [ ] Objective 1
- [ ] Objective 2
- [ ] Objective 3

## Implementation Guidance

### Recommended Approach
[High-level architectural approach validated during research]

**Key Principles**:
1. Principle 1 (from research)
2. Principle 2 (from research)

### Design References
Supporting documentation created during research:
- **Design Document**: `/poc/docs/design/[design-doc].md`
- **Architecture Decision Record**: `/poc/docs/adr/YYYY-MM-DD-[decision].md`
- **Research Findings**: `/research/[research-doc].md`
- **Implementation Guide**: [If applicable]

### API/Interface Design
[Sketch of proposed APIs, interfaces, or public contracts discovered/validated during research]

```csharp
// Example interface from research
public interface IExample
{
    Task DoSomethingAsync(CancellationToken cancellationToken);
}
```

### Component Architecture
[High-level component structure and interactions]

```
┌─────────────┐
│  Component1 │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Component2 │
└─────────────┘
```

### Key Implementation Considerations
Critical points identified during research:

1. **[Consideration 1]**
   - Details from research
   - Why this matters
   - How to address

2. **[Consideration 2]**
   - Details from research
   - Why this matters
   - How to address

### Reusable Patterns/Code
[Code patterns, algorithms, or approaches that proved valuable during prototyping]

```csharp
// Pattern from research prototype
// This approach handled edge case X effectively
```

**Prototype Code Reference** (if available):
- See `/research/[topic]/handover/prototype/` for reference implementation files
- These files demonstrate key patterns and approaches validated during research
- Use as reference, adapt as needed for production implementation

### Integration Points
[How this integrates with existing codebase]

- **Integration Point 1**: [Description]
- **Integration Point 2**: [Description]

## Testing and Validation

### Test Coverage Required
Test scenarios identified and validated during research:

#### Unit Tests
1. **Test Scenario 1**: [Description]
   - Setup: [How to set up test]
   - Expected: [What should happen]
   - Why: [Why this test matters from research]

2. **Test Scenario 2**: [Description]
   - Setup: [How to set up test]
   - Expected: [What should happen]
   - Why: [Why this test matters from research]

#### Integration Tests
1. **Integration Scenario 1**: [Description]
2. **Integration Scenario 2**: [Description]

#### Example/Demo Tests
Recommended example tests to demonstrate usage and validate functionality:

**Guidance**: Include 3-5 example tests showing key usage patterns
- Focus on common patterns rather than exhaustive feature coverage
- Use "before/after" comparison tests to demonstrate improvements (if applicable)
- Examples should be clear, well-documented, and representative of real usage

**Minimum Examples**:
- [ ] Basic usage scenario
- [ ] Common integration pattern
- [ ] Error handling/edge case
- [ ] [Add specific examples based on feature]

### Performance Validation
Performance requirements based on research benchmarks:

- **Throughput**: [Target based on research]
- **Latency**: [Target based on research]
- **Memory**: [Constraints based on research]

**Benchmark Scenarios**:
1. Scenario 1: [Description and expected results]
2. Scenario 2: [Description and expected results]

**Reference**: See benchmark methodology in `/research/[research-doc].md`

### Edge Cases
Edge cases discovered during research prototyping:

1. **Edge Case 1**: [Description]
   - How to handle: [Approach]
   - Test coverage: [Required tests]

2. **Edge Case 2**: [Description]
   - How to handle: [Approach]
   - Test coverage: [Required tests]

## Constraints and Requirements

### Technical Constraints
- **Constraint 1**: [Description and rationale]
- **Constraint 2**: [Description and rationale]

### Performance Requirements
- Requirement 1: [Based on research]
- Requirement 2: [Based on research]

### Compatibility Requirements
- **Backward Compatibility**: [Requirements]
- **API Stability**: [Guarantees needed]
- **.NET Version**: [Target framework]

### Dependencies
New dependencies (if any) validated during research:
- **Package 1**: [Name, version, purpose]
- **Package 2**: [Name, version, purpose]

## Alternatives Explored

During research, multiple approaches were evaluated:

### Alternative 1: [Approach Name]
**Description**: [Brief description]
**Pros**: 
- Advantage 1
- Advantage 2
**Cons**: 
- Disadvantage 1
- Disadvantage 2
**Why Not Chosen**: [Rationale from research]

### Alternative 2: [Approach Name]
**Description**: [Brief description]
**Pros**: 
- Advantage 1
- Advantage 2
**Cons**: 
- Disadvantage 1
- Disadvantage 2
**Why Not Chosen**: [Rationale from research]

## References and Resources

### Documentation
All supporting documentation created during research:
- **Research Report**: `/research/[research-doc].md`
- **Design Document**: `/poc/docs/design/[design-doc].md`
- **Architecture Decision Record**: `/poc/docs/adr/YYYY-MM-DD-[topic].md`
- **Test Implementation Guide**: [If separate document created]
- **Glossary**: Updated terms in `/poc/docs/POC_GLOSSARY.md`
- **Prototype Code**: `/research/[topic]/handover/prototype/` (if available)

### Prior Work
Related issues and PRs:
- **Research Issue**: #[N] - [Title]
- **Related Issues**: #[N], #[N]
- **Related PRs**: #[N], #[N]

### External References
External documentation or resources referenced during research:
- [Resource 1]
- [Resource 2]

## Implementation Phases (Optional)

If implementation should be phased:

### Phase 1: [Phase Name]
- [ ] Task 1
- [ ] Task 2
- **Goal**: [What this phase achieves]

### Phase 2: [Phase Name]
- [ ] Task 1
- [ ] Task 2
- **Goal**: [What this phase achieves]

## Documentation Deliverables

Documentation to be created/updated as part of this implementation:

### Required Documentation
- [ ] **Usage guide** for new utilities/features (with examples)
  - Scope: [Comprehensive guide (>10KB) vs Quick start guide (<3KB)]
  - Audience: [End users vs Contributors vs Both]
- [ ] **Pattern/best practices guide** (if applicable)
  - Document recommended patterns discovered during research
  - Include decision criteria for when to use each pattern
- [ ] **README** for new directories/modules
  - Overview of purpose and structure
  - Quick start examples
  - Links to detailed documentation

### Navigation Updates
- [ ] Update relevant index/navigation files
  - Examples: `/poc/docs/INDEX.md`, project README files, etc.
  - Ensure new documentation is discoverable

### Documentation Placement
Use the documentation directory decision tree:
- `/docs/` - Production user-facing documentation
- `/poc/docs/guides/` - POC-specific implementation guides
- `/poc/docs/adr/` - Architecture decision records
- `/research/[topic]/` - Research artifacts and analysis

## Success Criteria

This implementation is complete when:

- [ ] All objectives are met
- [ ] Test coverage requirements are satisfied
- [ ] Performance requirements are met (validated with benchmarks)
- [ ] All edge cases are handled
- [ ] **Documentation deliverables completed** (see Documentation Deliverables section)
- [ ] **Navigation files updated** (indices, READMEs)
- [ ] Code review is complete
- [ ] CI/CD pipeline passes

## Questions for Implementation Team

[Any open questions or areas where implementation team should use judgment]

1. Question 1?
2. Question 2?

## Notes

[Any additional context, warnings, or important notes for implementation team]

---

**Created**: YYYY-MM-DD
**Research Issue**: #[N]
**Research PR**: #[N]
```

## Tips for Creating Great Implementation Issues

### Be Comprehensive
- Include all context from research
- Link to all supporting documentation
- Explain the "why" not just the "what"

### Make It Actionable
- Clear objectives with acceptance criteria
- Specific implementation guidance
- Test scenarios with expected behavior

### Provide Context on Alternatives
- Show what was considered
- Explain why the recommended approach was chosen
- Help implementation team understand trade-offs

### Enable Independent Implementation
- Developer should be able to implement by reading issue and docs alone
- No need to ask "why did research choose this approach?"
- All necessary context is present

### Include Test Guidance
- Document valuable test scenarios discovered during research
- Explain edge cases found during prototyping
- Provide benchmark requirements and methodology

### Be Honest About Unknowns
- Note any open questions
- Highlight areas for implementation team judgment
- Don't pretend research answered everything

## Example Usage

See `/poc/docs/plans/IMPLEMENTATION_ISSUE_EXAMPLE.md` for a complete example of this template in use.
