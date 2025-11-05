# Quick Start: Using Workflow Guidelines with GitHub Copilot

## For Issue Creators

When creating a GitHub issue, choose the appropriate workflow:

### Research Workflow
For investigating/validating approaches:

```markdown
## Research Context

⚠️ This is a research issue. @copilot Please follow the Research workflow in `/research/RESEARCH_WORKFLOW.md`.

**Research Objective**: [What needs to be validated]
**Target Codebase**: [POC / Production / Both]

[Your research questions and approach here]
```

### Implementation Workflow
For implementing based on research handover or direct requirements:

#### Option 1: With Research Handover (Recommended)
```markdown
## Implementation Context

⚠️ **This is an implementation issue.** @copilot Follow Implementation workflow in `.github/copilot-instructions.md`.

**Handover Document Path**: `/research/[topic]/handover/github-issue-[description].md`
**Target**: [POC / Production / Both]

[Brief summary - most context is in handover doc]
```

#### Option 2: Direct Implementation (No research phase)
```markdown
## Implementation Context

⚠️ **This is an implementation issue.** @copilot Follow Implementation workflow.

**Target**: [POC / Production / Both]

[Your complete requirements here]
```

#### Option 3: As a Comment (On existing issues)
```markdown
@copilot This is an implementation issue for POC. Follow the Implementation workflow in `.github/copilot-instructions.md`
```

## What Happens Next

### For Research Issues
When GitHub Copilot sees a research issue:

1. ✅ Creates research folder structure
2. ✅ Writes exploratory code to validate approaches
3. ✅ Documents findings comprehensively
4. ✅ Creates implementation-ready handover issue
5. ✅ Reverts exploratory code (after reviewer approval)

### For Implementation Issues
When GitHub Copilot sees an implementation issue:

1. ✅ Reads handover document (if applicable)
2. ✅ Identifies target codebase (POC vs Production)
3. ✅ Loads appropriate context (POC docs, etc.)
4. ✅ Creates plan (for POC targets)
5. ✅ Implements following guidance
6. ✅ Updates documentation and glossary
7. ✅ Validates through testing

## For More Details

- **Issue Templates**: See `.github/ISSUE_TEMPLATE/`
- **Template Guide**: See `.github/POC_ISSUE_TEMPLATE.md`
- **Workflow Examples**: See `.github/EXAMPLE_WORKFLOWS.md`
- **Main Instructions**: See `.github/copilot-instructions.md`
- **Research Workflow**: See `/research/RESEARCH_WORKFLOW.md`

## Example Implementation Issue

```markdown
# Implement Epoch Caching

## Implementation Context

⚠️ **This is an implementation issue.** @copilot Follow Implementation workflow.

**Handover Document Path**: `/research/epoch-caching/handover/github-issue-implement-epoch-cache.md`
**Target**: [x] POC

## Problem Statement

Research validated approach for epoch-scoped caching. See handover document for complete context.

## Implementation Checklist

- [ ] Implement EpochCache as designed
- [ ] All test scenarios from handover
- [ ] Performance validation
- [ ] Update glossary and documentation
```

That's it! GitHub Copilot will handle the rest following the appropriate workflow.
