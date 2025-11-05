# .github Folder Documentation

This folder contains GitHub-specific configuration and documentation.

## GitHub Copilot Instructions

### Main Instructions
**File**: `copilot-instructions.md`

Main guidelines for GitHub Copilot when working on this repository. These instructions are automatically loaded by GitHub Copilot and cover:
- Repository overview and architecture
- Coding standards and conventions
- Testing patterns
- Research workflow identification
- **Implementation Team Workflow** (new unified workflow)
- POC work guidelines
- Common patterns and anti-patterns

### Issue Templates
**Folder**: `ISSUE_TEMPLATE/`

GitHub issue templates for creating structured issues:
- `research.md` - For research/investigation issues
- `implementation.md` - For implementation issues (with or without research handover)

### Issue Template Guide
**File**: `POC_ISSUE_TEMPLATE.md`

Comprehensive guide for creating issues with templates and examples:
- Research workflow templates
- Implementation workflow templates
- Examples for each workflow type
- Tips for effective issue creation

**How to use**: Reference when creating issues to understand template usage.

### Workflow Examples
**File**: `EXAMPLE_WORKFLOWS.md`

Complete examples showing how to use workflows:
- Research workflow with example
- Implementation workflow with research handover
- Implementation workflow without handover
- Tips for success

### Quick Start
**File**: `QUICK_START.md`

Quick reference for creating issues with the right workflow:
- Research issue format
- Implementation issue format
- What happens next
- Simple examples

## How GitHub Copilot Uses These Files

GitHub Copilot automatically loads `.github/copilot-instructions.md` for context.

### For Research Work
Create issues using the Research template or reference:
```markdown
⚠️ This is a research issue. @copilot Follow `/research/RESEARCH_WORKFLOW.md`
```

### For Implementation Work
Create issues using the Implementation template or reference:
```markdown
⚠️ This is an implementation issue. @copilot Follow Implementation workflow in `.github/copilot-instructions.md`
```

Specify target codebase (POC / Production / Both) and link handover document if from research.

## Other Folders

### `/workflows`
GitHub Actions workflow definitions for CI/CD.

### `/scripts`
Helper scripts for CI/CD and automation.

## See Also

- [Main Copilot Instructions](.github/copilot-instructions.md) - Comprehensive workflow guidance
- [Research Workflow](/research/RESEARCH_WORKFLOW.md) - Complete research workflow
- [POC README](/poc/README.md) - Overview of the POC
- [POC Documentation Structure](/poc/docs/POC_DOCUMENTATION_STRUCTURE.md) - How POC docs are organized
