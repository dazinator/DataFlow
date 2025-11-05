# Quick Start: Using POC Guidelines with GitHub Copilot

## For Issue Creators

When creating a GitHub issue for POC work, simply add one of these references:

### Option 1: Full Context (Recommended for complex issues)
```markdown
## POC Context

⚠️ This is a POC issue. @copilot Please follow the POC workflow guidelines in `.github/copilot-instructions-poc.md`.

**Key Requirements**:
- Read `/poc/README.md` and relevant documentation
- Create a plan in `/poc/docs/plans/`
- Document research in `/research/`
- Update `/poc/docs/POC_GLOSSARY.md` with new terminology
- Track decisions in `/poc/docs/adr/`

[Your issue details here]
```

### Option 2: Minimal Reference (For simple issues)
```markdown
⚠️ **POC Issue**: @copilot Follow POC guidelines (`.github/copilot-instructions-poc.md`)

[Your issue details here]
```

### Option 3: As a Comment (On existing issues)
```markdown
@copilot This is a POC issue. Please follow the POC workflow in `.github/copilot-instructions-poc.md`
```

## What Happens Next

When GitHub Copilot sees your POC issue reference:

1. ✅ Loads the POC-specific guidelines
2. ✅ Reads POC documentation structure
3. ✅ Creates a plan in `/poc/docs/plans/`
4. ✅ Documents research and findings
5. ✅ Updates glossary with new terms
6. ✅ Tracks decisions in ADRs
7. ✅ Validates through testing/benchmarking

## For More Details

- **Full Templates**: See `.github/POC_ISSUE_TEMPLATE.md`
- **Complete Example**: See `.github/EXAMPLE_POC_WORKFLOW.md`
- **POC Workflow**: See `.github/copilot-instructions-poc.md`
- **Documentation**: See `.github/README.md`

## Example POC Issue

```markdown
# Implement Epoch Caching

⚠️ **POC Issue**: @copilot Follow POC guidelines (`.github/copilot-instructions-poc.md`)

## Problem
Need caching mechanism aligned with epoch boundaries.

## Requirements
- Cache scoped to epoch lifecycle
- Memory bounded
- Works with epoch merging

## Deliverables
- [ ] Plan in `/poc/docs/plans/implement-epoch-caching.md`
- [ ] Implementation with tests
- [ ] Benchmark results in `/research/`
- [ ] Update glossary if needed
```

That's it! GitHub Copilot will handle the rest following the POC workflow.
