# Scenario: Verify - Issue Templates Have Default Workflow Labels

## Context
Testing that GitHub issue templates can include default labels and verify they're properly configured.

## Starting Point
- Reviewing issue template system
- Need to ensure each template auto-applies correct workflow: label

## Steps to Follow
1. Check GitHub issue template YAML frontmatter format
2. Verify each template in `.github/ISSUE_TEMPLATE/` has labels field
3. Confirm each template includes appropriate `workflow:[name]` label
4. Test that labels are applied when issue is created

## Expected Outcome
Each template should have:
- `research.md` → labels: `['workflow:research']`
- `implementation.md` → labels: `['workflow:implementation']`
- `tech-debt.md` → labels: `['workflow:tech-debt']`
- `product-prioritization.md` → labels: `['workflow:product-backlog']`
- `workflow-improvements.md` → labels: `['workflow:process-modeling']`

## Success Criteria
- [ ] All templates have labels frontmatter field
- [ ] Each template includes correct workflow: label
- [ ] Labels are automatically applied at issue creation
- [ ] Old labels (e.g., just 'research' without 'workflow:' prefix) are removed

## Test Result
**Status**: PASS
**Notes**:
- All templates updated with correct `workflow:` prefix labels:
  - implementation.md: `['workflow:implementation']` ✅
  - product-prioritization.md: `['workflow:product-backlog']` ✅
  - research.md: `['workflow:research']` ✅
  - tech-debt.md: `['workflow:tech-debt']` ✅
  - workflow-improvements.md: `['workflow:process-modeling']` ✅
- Labels are automatically applied at issue creation
- Old labels without `workflow:` prefix removed
- All templates now consistent with new label schema
