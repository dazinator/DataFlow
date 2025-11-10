# Scenario: Verify - No /product/backlog References in Active Workflows

## Context
Testing that all references to old `/product/backlog` file-based system are removed and replaced with GitHub issue-based workflow.

## Starting Point
- Old system used `/product/backlog/*.md` files
- New system uses GitHub issues with `workflow:product-backlog` label
- Need to verify all documentation updated

## Steps to Follow
1. Check all workflow files for `/product/backlog` references
2. Check copilot instructions for `/product/backlog` references  
3. Check issue templates for `/product/backlog` references
4. Verify replacement guidance points to GitHub issues
5. Confirm `/product/backlog` folder can be deleted

## Expected Outcome
- No active references to `/product/backlog` path
- All workflows reference GitHub issues with labels
- Templates updated to mention workflow: labels
- Old backlog folder can be safely deleted

## Success Criteria
- [ ] No grep matches for `/product/backlog` in .team/prompts/
- [ ] No grep matches for `/product/backlog` in .github/copilot-instructions.md
- [ ] No grep matches for `/product/backlog` in .github/ISSUE_TEMPLATE/
- [ ] Workflows reference `workflow:product-backlog` label instead
- [ ] Old folder is empty or contains only migrated items

## Test Result
**Status**: PASS
**Notes**:
- ✅ 0 active references to `/product/backlog` (except 1 deprecation notice in RESEARCH_WORKFLOW)
- ✅ All workflows updated to use GitHub issues with `workflow:product-backlog` label
- ✅ Copilot-instructions.md updated (removed /product/ folder from structure)
- ✅ TECH_DEBT_WORKFLOW.md updated (comprehensive changes to backlog creation and review)
- ✅ RESEARCH_WORKFLOW.md updated (backlog creation now creates GitHub issues)
- ✅ IMPLEMENTATION_WORKFLOW.md updated (reads backlog GitHub issues)
- ✅ PRODUCT_PRIORITIZATION_WORKFLOW.md updated (queries GitHub issues)
- ✅ NUGET_DEPENDENCY_UPDATES.md updated (removed template references)
- ✅ Implementation issue template updated (references GitHub issues)
- ✅ `/product/backlog` folder deleted
- Handover assets now stored in `/research/[topic]/handover/` folders
