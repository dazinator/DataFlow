# Issue Templates for GitHub Copilot

Use these templates when creating GitHub issues. This helps GitHub Copilot understand the context and follow the appropriate workflow.

## Workflow Selection

Work in this repository follows two workflows:

1. **Research Workflow**: Research validates approach, produces implementation-ready GitHub issue (code reverted at PR review approval)
   - Use for: Researching any code (POC or production) where outcome is specification for implementation
   - Template: `.github/ISSUE_TEMPLATE/research.md`
   
2. **Implementation Workflow**: Implementation based on research handover or direct requirements
   - Use for: Implementing features in POC or production code based on validated designs
   - Template: `.github/ISSUE_TEMPLATE/implementation.md`

Choose based on the issue type (see `.github/copilot-instructions.md` for guidance).

## Template 1: Research Issues

Use when research will produce a handoff issue for implementation team (applies to any codebase).

**GitHub Template**: Use `.github/ISSUE_TEMPLATE/research.md` or copy below:

```markdown
## Research Context

⚠️ This is a research issue. @copilot Please follow the Research-to-Implementation workflow in `/research/RESEARCH_WORKFLOW.md`.

**Research Objective**: [What needs to be validated/explored]

**Target Codebase**: [POC / Production / Both]

**Key Requirements**:
- Read `/poc/README.md` and relevant documentation in `/poc/docs/`
- Create research folder structure - see `/research/FOLDER_STRUCTURE.md`
- Freely explore and validate approaches with code (POC or production)
- Document research findings following the folder structure reference
- Create supporting docs (design, ADRs) in research folder
- Create implementation-ready issue using `/research/IMPLEMENTATION_ISSUE_TEMPLATE.md`
- Update `/poc/docs/POC_GLOSSARY.md` with new terminology
- **Code reversion happens only when PR reviewer approves and requests it**

**Expected Deliverables**:
- [ ] Research folder (see `/research/FOLDER_STRUCTURE.md` for structure)
- [ ] Research plan, documentation, design docs, and ADRs
- [ ] Implementation-ready issue in handover folder
- [ ] Updated glossary (if new concepts)
- [ ] After reviewer approval: All exploratory code changes reverted

**Note**: This research will produce a comprehensive implementation issue for handoff to engineering. Code reversion happens at a specific point when the PR reviewer approves the research findings and explicitly requests it - NOT automatically.

## Issue Details

[Describe the research objectives and scope]

## Success Criteria

- [ ] Approach validated through prototyping
- [ ] Research comprehensively documented
- [ ] Implementation issue contains complete context
- [ ] All supporting documentation created
- [ ] Code changes reverted, only docs remain
```

## Template 2: Implementation Issues

Use when implementing based on research handover or direct requirements.

**GitHub Template**: Use `.github/ISSUE_TEMPLATE/implementation.md` or copy below:

```markdown
## Implementation Context

⚠️ **This is an implementation issue.** @copilot Please follow the Implementation workflow guidelines in `.github/copilot-instructions.md`.

### Handover Document (if from research team)

**Handover Document Path**: [e.g., `/research/flow-composability/handover/github-issue-implement-feature.md`]

If this implementation is based on research team handover, the handover document contains:
- Complete problem context and background
- Implementation guidance and recommended approach
- Test scenarios and performance requirements
- Design references and supporting documentation

**@copilot**: Read the handover document first to understand the complete scope and context.

### Target Codebase

**Target**: [ ] POC (`/poc`) or [ ] Production (`/src`) or [ ] Both

The handover document or context below should clearly indicate which codebase this implementation targets.

**@copilot**: If the target codebase is unclear from the handover document and context, **STOP** and ask the user to clarify before proceeding.

## Problem Statement

[If NOT from research handover: Describe the specific problem or feature to implement]

[If from research handover: Reference the handover document - most context is there]

## Implementation Checklist

Based on the handover document (or requirements below), the implementation should include:

- [ ] Implementation with tests
- [ ] Performance validation (if benchmarks specified)
- [ ] Documentation updates
- [ ] Edge cases handled (as specified in handover)
- [ ] Code review and validation

## Additional Context (if needed)

[Any additional context not in the handover document]

[If this is direct implementation without research handover, provide full requirements here]

## Success Criteria

- [ ] All objectives from handover document met (or requirements met if direct implementation)
- [ ] Tests passing
- [ ] Performance requirements met (if applicable)
- [ ] Documentation updated
```

## Alternative: Minimal Template

For simpler issues where the full template might be too verbose:

```markdown
⚠️ **Implementation Issue**: @copilot Follow implementation guidelines (`.github/copilot-instructions.md`)

**Target**: [POC / Production]

[Your issue description here]
```

## Alternative: Comment-based Trigger

You can also add this as a comment on an existing issue to inform Copilot:

```markdown
@copilot This is an implementation issue for POC. Please:
1. Follow the Implementation workflow in `.github/copilot-instructions.md`
2. Create a plan in `/poc/docs/plans/`
3. Update documentation as needed
```

## Example: Complete Implementation Issue

Here's a complete example showing implementation from research handover:

```markdown
# Implement Epoch-Based Cache Management

## Implementation Context

⚠️ **This is an implementation issue.** @copilot Please follow the Implementation workflow guidelines in `.github/copilot-instructions.md`.

### Handover Document (if from research team)

**Handover Document Path**: `/research/epoch-caching/handover/github-issue-implement-epoch-cache.md`

The handover document contains complete context, design, and requirements.

### Target Codebase

**Target**: [x] POC (`/poc`) [ ] Production (`/src`)

## Problem Statement

Research validated approach for epoch-scoped caching. See handover document for complete context.

## Implementation Checklist

Based on the handover document:

- [ ] Implement EpochCache<TKey, TValue> as designed
- [ ] Handle cache promotion during epoch merges
- [ ] Implement all test scenarios from handover
- [ ] Performance validation with benchmarks
- [ ] Update glossary with caching terminology
- [ ] Create usage guide

## Success Criteria

- [ ] All objectives from handover met
- [ ] Tests passing (including edge cases)
- [ ] Performance requirements met
- [ ] Documentation updated (glossary, guides)
```

## Choosing the Right Template

**Use Research Template when:**
- Exploring/validating new approaches
- Need to compare alternatives
- Outcome is specification for implementation team

**Use Implementation Template when:**
- Implementing based on research handover
- Direct implementation with clear requirements
- Building on validated designs

## Tips

1. **Be explicit about workflow type** - Research vs Implementation
2. **Link handover documents** - For implementation from research
3. **Specify target codebase** - POC vs Production vs Both
4. **List expected deliverables** - Helps Copilot plan the work
5. **Include documentation requirements** - Important for both workflows

## See Also

- **Main Instructions**: `.github/copilot-instructions.md`
- **Research Workflow**: `/research/RESEARCH_WORKFLOW.md`
- **POC Overview**: `/poc/README.md`
- **Workflow Examples**: `.github/EXAMPLE_WORKFLOWS.md`
