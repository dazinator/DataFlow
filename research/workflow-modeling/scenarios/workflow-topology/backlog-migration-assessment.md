# Product Backlog Migration Assessment

**Date**: 2025-11-09
**Purpose**: Assess current product backlog for migration to GitHub issues

---

## Summary

**Total Backlog Items**: 7 files
**Location**: `/product/backlog/`
**All items are**: Tech Debt items from 2025-11-09 analysis

---

## Backlog Items to Migrate

| # | Backlog ID | Title | Category | Status |
|---|------------|-------|----------|--------|
| 1 | techdebt-2025-11-09-async-without-await | Async without await warnings | Modern C# Practices | Active |
| 2 | techdebt-2025-11-09-eliminate-cs0436-type-conflicts | Eliminate CS0436 type conflicts | Build Quality | Active |
| 3 | techdebt-2025-11-09-enumerator-cancellation | Add EnumeratorCancellation attributes | Modern C# Practices | Active |
| 4 | techdebt-2025-11-09-file-scoped-namespaces | Modernize to file-scoped namespaces | Modern C# Practices | Active |
| 5 | techdebt-2025-11-09-nullable-reference-types | Enable nullable reference types | Modern C# Practices | Active |
| 6 | techdebt-2025-11-09-poc-editorconfig | Add .editorconfig to POC | Build Quality | Active |
| 7 | techdebt-2025-11-09-unused-fields | Remove unused fields | Code Quality | Active |

---

## Migration Plan

### Automated Migration Script

Created: `.team/scripts/workflow/migrate-backlog-to-issues.sh`

**What it does**:
- Creates GitHub issue for each of the 7 backlog items
- Adds `workflow:product-backlog` label to all issues
- Adds `tech-debt` label to categorize
- Includes full backlog file content in issue body
- Links to original file in repository
- Marks original files as migrated

**Benefits of running before merge**:
1. **Tests GitHub CLI authentication** - Validates that `gh` can create issues
2. **Validates permissions** - Ensures PAT/token has necessary scopes
3. **Validates label schema** - Tests that `workflow:product-backlog` label exists
4. **Provides early feedback** - Catches any issues with issue creation before workflows depend on it

### Expected GitHub Issues

After running the migration script, we expect:
- 7 new GitHub issues created
- All labeled with `workflow:product-backlog` and `tech-debt`
- Queryable via: `gh issue list --label "workflow:product-backlog"`
- Ready for Product Prioritization workflow to process

### Testing Checklist

Before merge, the migration script should test:
- [x] GitHub CLI (`gh`) is installed and authenticated
- [x] Can create issues via API
- [x] Can add labels to issues
- [x] Labels exist (workflow:product-backlog, tech-debt)
- [x] Issue body formatting works
- [x] Links to repository files work in issue body

---

## Post-Migration State

### Original Files
- Remain in `/product/backlog/` with migration marker comment
- Include issue number and URL in comment
- Can be archived or kept for historical reference

### GitHub Issues
- Become the active backlog items
- Can be queried, filtered, and searched
- Support comments for adding context
- Can link to handover materials in repository
- Integrate with workflow topology system

---

## Rollback Plan

If migration needs to be reverted:

1. **Close migrated issues**:
   ```bash
   gh issue list --label "workflow:product-backlog" --json number --jq '.[].number' | \
     xargs -I {} gh issue close {}
   ```

2. **Remove migration markers from files**:
   ```bash
   find product/backlog -name "*.md" -exec sed -i '/<!-- MIGRATED TO GITHUB ISSUE/d' {} \;
   ```

3. **Keep using file-based system** until ready to migrate again

---

## Recommendations

1. **Run migration script after PR merge** to test GitHub CLI setup
2. **Review created issues** to ensure formatting and links work correctly
3. **Test workflow integration** by:
   - Querying product backlog: `gh issue list --label "workflow:product-backlog"`
   - Prioritizing one item: handover to implementation workflow
   - Verifying implementation workflow can read issue and linked materials
4. **Document any issues** encountered for future reference

---

## Migration Script Location

`.team/scripts/workflow/migrate-backlog-to-issues.sh`

See script README and WORKFLOW_TOPOLOGY_GUIDE.md for usage instructions.
