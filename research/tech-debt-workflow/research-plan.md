# Research Plan: Tech Debt Discovery Workflow

**Research Period**: November 2025  
**Target Codebase**: POC (DataFlow)  
**Research Issue**: Research a tech debt workflow

## Research Objective

Design and validate a systematic workflow for discovering, documenting, and prioritizing technical debt in the codebase. The workflow should enable Copilot agents to analyze code for improvement opportunities, report findings for review, and hand off selected items to implementation teams while archiving non-selected items for future consideration.

## Background

The existing repository has two established workflows:
1. **Research Workflow** - Validates approaches through exploration, produces implementation-ready issues
2. **Implementation Workflow** - Implements validated designs from research handovers

This research aims to create a third workflow focused specifically on **tech debt discovery and improvement analysis**, which shares characteristics with the research workflow (exploratory code, reversion before merge) but has a unique focus on systematic codebase analysis and selective handover.

## Research Questions

### 1. Workflow Structure
- How should the tech debt workflow relate to existing research workflow?
- What are the key differences between general research and tech debt analysis?
- Should tech debt analysis reuse `/research/` folder structure or have its own?

### 2. Discovery Process
- What exploration areas should guide tech debt discovery? (e.g., compiler warnings, modern practices, developer experience)
- How can we ensure comprehensive coverage of potential tech debt areas?
- What structured challenges/scenarios should agents use to discover issues?
- Should we have different tech debt discovery "modes" (e.g., performance, maintainability, DX)?

### 3. Reporting and Review
- How should findings be reported in the PR for reviewer evaluation?
- What format makes it easy for reviewers to select items for implementation?
- How should findings be categorized/prioritized?
- What information is needed to make informed selection decisions?

### 4. Handover Process
- How should selected items be handed over to implementation teams?
- What template should tech debt handover issues follow?
- How does this differ from research handover issues?
- Should multiple related tech debt items be combined into one issue?

### 5. Backlog Management
- What structure should `/research/backlog/` follow?
- What naming convention enables easy discovery and review? (date + short-name)
- What information should backlog items contain?
- How can we make backlog items actionable in the future?

### 6. Exploration Scenarios
The issue suggests specific exploration approaches:
- "Imagine you are a developer seeing this git repo for the first time" scenario
- Building solution and running tests as a new developer
- Looking for compiler warnings
- Evaluating modern coding practices
- Identifying what worked well and what didn't in the development experience

How can these be systematized into a repeatable workflow?

## Success Metrics

### Quantitative
- **Workflow documentation completeness**: All questions above answered with concrete guidance
- **Exploration coverage**: At least 5 distinct tech debt discovery areas documented
- **Finding categories**: Clear categorization scheme for different types of tech debt
- **Backlog structure**: File naming convention and folder structure established
- **Template creation**: At least 2 templates created (tech debt handover issue, backlog item)

### Qualitative
- **Ease of use**: Workflow is clear enough for an agent to follow independently
- **Actionability**: Findings format makes selection decisions straightforward
- **Reusability**: Workflow can be repeated on different codebases
- **Integration**: Fits naturally with existing research and implementation workflows
- **Value**: Produces genuinely useful tech debt insights (validated through POC analysis)

### Baseline
- Current state: No systematic tech debt discovery process
- Tech debt discoveries are ad-hoc during other work
- No structured handover for improvement opportunities
- No backlog management for deferred improvements

### Validation
- Apply the workflow to POC codebase
- Generate real tech debt findings
- Create example handover and backlog items
- Demonstrate the complete cycle from discovery to categorization

## Validation Approach

### Phase 1: Workflow Design (Research)
1. Analyze existing research workflow for reusable patterns
2. Design tech debt discovery workflow structure
3. Define exploration areas and scenarios
4. Create templates for reporting and handover
5. Document backlog management process

### Phase 2: Validation (Exploratory Code)
1. Apply tech debt workflow to POC codebase
2. Execute the "new developer" scenario
3. Build solution, run tests, identify issues
4. Analyze compiler warnings
5. Evaluate coding practices and developer experience
6. Document findings using proposed format

### Phase 3: Handover Creation
1. Categorize findings
2. Create example PR report format
3. Simulate reviewer selection process
4. Create example handover issues for "selected" items
5. Create example backlog items for "non-selected" items

## Expected Outcomes

### Documentation
- `/research/tech-debt-workflow/README.md` - Complete research findings
- `/research/tech-debt-workflow/design/tech-debt-workflow.md` - Workflow specification
- Tech debt workflow document in `.team/workflows/TECH_DEBT_WORKFLOW.md`
- Templates for tech debt discovery

### Handover
- `/research/tech-debt-workflow/handover/github-issue-tech-debt-workflow.md` - Implementation issue
- Example tech debt handover issues demonstrating the process
- Example backlog items in `/research/backlog/`

### Supporting Documentation
- ADR in `/poc/docs/adr/` documenting workflow decisions
- Updated workflow improvements file with learnings

### Exploratory Code (To Be Reverted)
- Any exploratory code written during POC analysis
- Test code for validating discoveries
- Example implementations (to be copied to handover/prototype/ before reversion)

## Exploration Areas for POC Analysis

When applying this workflow to POC codebase, investigate:

1. **New Developer Experience**
   - Clone repo, build solution, run tests
   - Document friction points, unclear areas
   - Identify missing documentation or guidance

2. **Compiler Warnings**
   - Catalog all warnings (currently 620 warnings in build)
   - Categorize by severity and actionability
   - Identify which should be addressed vs suppressed

3. **Modern C# Practices**
   - File-scoped namespaces
   - Primary constructors
   - Collection expressions
   - Required properties
   - Init-only properties

4. **Test Quality**
   - Test organization and discoverability
   - Test naming conventions
   - Duplicate test code
   - Missing test coverage areas

5. **Code Organization**
   - Namespace structure
   - File organization
   - Separation of concerns
   - Public API surface

6. **Documentation**
   - README completeness
   - API documentation
   - Example code quality
   - Migration guides

7. **Performance Opportunities**
   - Obvious performance improvements
   - Memory allocation patterns
   - Async/await usage

8. **Developer Tooling**
   - Build scripts
   - Test runners
   - Code formatters
   - Analyzers

## Timeline

- **Phase 1 (Design)**: 2-3 hours - Create workflow documentation and templates
- **Phase 2 (Validation)**: 3-4 hours - Apply to POC codebase, gather findings
- **Phase 3 (Handover)**: 1-2 hours - Create example handover and backlog items

**Total Estimated**: 6-9 hours

## Notes

- This workflow complements rather than replaces existing research workflow
- Focus on systematic discovery, not deep investigation (that's for regular research)
- Backlog items should be lightweight enough to create many of them
- Handover items should follow standard implementation issue quality
- The workflow should be repeatable and consistent
