# Issue Templates for GitHub Copilot

Use these templates when creating GitHub issues for research or POC work. This helps GitHub Copilot understand the context and follow the appropriate workflow.

## Workflow Selection

Work in this repository follows two workflows:

1. **Research-to-Implementation**: Research validates approach, produces implementation-ready GitHub issue (code reverted at PR review approval)
   - Use for: Researching any code (POC or production) where outcome is specification for implementation
2. **Direct Integration**: Research and implementation together, code integrated into codebase
   - Use for: POC-contained changes where code will be directly merged

Choose based on the issue type (see `.github/copilot-instructions-poc.md` for guidance).

## Template for Research-to-Implementation Issues

Use when research will produce a handoff issue for implementation team (applies to any codebase):

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

## Template for Direct Integration Issues

Use when research and implementation will be directly integrated into POC:

```markdown
## POC Context

⚠️ This is a POC issue. @copilot Please follow the POC workflow guidelines in `.github/copilot-instructions-poc.md`.

**Key Requirements**:
- Read `/poc/README.md` and relevant documentation in `/poc/docs/`
- Create a plan in `/poc/docs/plans/` for this work
- Document research and findings in `/research/`
- Update `/poc/docs/POC_GLOSSARY.md` with new terminology
- Validate through testing and benchmarking
- Track decisions in `/poc/docs/adr/` where appropriate

## Expected Deliverables

- [ ] Plan document in `/poc/docs/plans/`
- [ ] Implementation with tests in POC projects
- [ ] Research/benchmark documentation (if applicable) in `/research/`
- [ ] Glossary updates (if new concepts introduced)
- [ ] ADR (if significant decisions made)

## Issue Details

[Describe the specific problem or feature to implement]
```

## Alternative: Minimal Template

For simpler issues where the full template might be too verbose:

```markdown
⚠️ **POC Issue**: @copilot Follow POC guidelines (`.github/copilot-instructions-poc.md`)

[Your issue description here]
```

## Alternative: Comment-based Trigger

You can also add this as a comment on an existing issue to inform Copilot:

```markdown
@copilot This is a POC issue. Please:
1. Follow the POC workflow in `.github/copilot-instructions-poc.md`
2. Create a plan in `/poc/docs/plans/`
3. Document research in `/research/`
4. Update the glossary as needed
```

## Example: Complete POC Issue

Here's a complete example showing how to write a well-structured POC issue:

```markdown
# Implement Epoch-Based Cache Management

## POC Context

⚠️ This is a POC issue. @copilot Please follow the POC workflow guidelines in `.github/copilot-instructions-poc.md`.

**Key Requirements**:
- Read `/poc/README.md` and epoch-related documentation in `/poc/docs/design/`
- Review `/poc/docs/POC_GLOSSARY.md` for epoch terminology
- Create a plan in `/poc/docs/plans/implement-epoch-cache.md`
- Document research and benchmark results in `/research/`
- Update glossary with any new caching-related terms
- Create ADR for major design decisions

## Problem Statement

The POC needs a caching mechanism that aligns with epoch boundaries to:
- Share cached data across blocks within the same epoch
- Properly dispose/clear cache at epoch completion
- Support cache promotion during epoch merges (similar to DbContext promotion)

## Requirements

1. Cache should be scoped to epoch lifecycle
2. Multiple blocks should be able to access the same epoch cache
3. Cache should be cleared at global epoch alignment
4. Performance should not degrade with cache enabled
5. Memory usage should be bounded and predictable

## Expected Deliverables

- [ ] Plan document in `/poc/docs/plans/implement-epoch-cache.md`
  - Document design alternatives considered
  - Include performance validation milestones
- [ ] Cache implementation with tests in `DataFlow.POC.Tests`
- [ ] Benchmark comparison (with/without cache) in `/research/`
- [ ] Implementation guide in `/poc/docs/guides/using-epoch-cache.md`
- [ ] Update `/poc/docs/POC_GLOSSARY.md` with caching terms
- [ ] ADR for cache design decisions in `/poc/docs/adr/`

## Success Criteria

- All tests pass
- Benchmark shows cache improves performance in test scenario
- Cache memory usage is bounded
- Documentation is complete and clear
```

## Tips

1. **Be explicit about POC context** - Don't assume Copilot will know
2. **Reference the POC guidelines file** - Makes it easy for Copilot to find
3. **List expected deliverables** - Helps Copilot plan the work
4. **Include documentation requirements** - POC emphasizes documentation
5. **Mention validation needs** - Testing and benchmarking are important

## See Also

- **POC Workflow Guidelines**: `.github/copilot-instructions-poc.md`
- **POC Overview**: `/poc/README.md`
- **POC Documentation Structure**: `/poc/docs/POC_DOCUMENTATION_STRUCTURE.md`
