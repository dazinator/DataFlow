# .github Folder Documentation

This folder contains GitHub-specific configuration and documentation.

## GitHub Copilot Instructions

### Main Instructions
**File**: `copilot-instructions.md`

Main guidelines for GitHub Copilot when working on this repository. These instructions are automatically loaded by GitHub Copilot and cover:
- Repository overview and architecture
- Coding standards and conventions
- Testing patterns
- Building and testing procedures
- Common patterns and anti-patterns

### POC-Specific Instructions
**File**: `copilot-instructions-poc.md`

Specialized guidelines for working on POC (Proof-of-Concept) issues in the `/poc` folder. These instructions cover:
- POC documentation structure and organization
- Workflow for POC issues (planning, research, validation)
- Documentation requirements during development
- Testing and benchmarking expectations
- How to maintain plans, research docs, and ADRs

**When to use**: Any issue related to the `/poc` folder should reference these instructions.

### POC Issue Template
**File**: `POC_ISSUE_TEMPLATE.md`

Template and examples for creating GitHub issues that involve POC work. Provides:
- Quick reference templates for POC issues
- Examples of well-structured POC issues
- Ways to trigger POC workflow from issue comments
- Tips for effective POC issue creation

**How to use**: Copy the template when creating POC-related issues, or reference it in issue comments.

## How GitHub Copilot Uses These Files

GitHub Copilot automatically loads `.github/copilot-instructions.md` for context. The POC-specific instructions and templates provide additional context when explicitly referenced.

### For Main Library Work
Copilot automatically follows `copilot-instructions.md` - no additional setup needed.

### For POC Work
Reference the POC guidelines in your issue:
```markdown
@copilot Follow POC guidelines (`.github/copilot-instructions-poc.md`)
```

Or use the template in `POC_ISSUE_TEMPLATE.md` when creating POC issues.

## Other Folders

### `/workflows`
GitHub Actions workflow definitions for CI/CD.

### `/scripts`
Helper scripts for CI/CD and automation.

## See Also

- [POC README](/poc/README.md) - Overview of the POC
- [POC Documentation Structure](/poc/docs/POC_DOCUMENTATION_STRUCTURE.md) - How POC docs are organized
- [Contributing Guide](/CONTRIBUTING.md) - General contribution guidelines (if exists)
