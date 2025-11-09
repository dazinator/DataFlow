# Scenario: Improved - Bulk Mode with GitHub Issues

## Context
Testing that bulk mode documentation makes sense with new GitHub issue-based workflow, including sub-issues and issue linking.

## Starting Point
- Old system: Create single issue, reference multiple files in /product/backlog
- New system: GitHub issues with labels, sub-issues, and issue linking
- Need clear guidance on how bulk mode works now

## Steps to Follow
1. Read current bulk mode documentation in workflows
2. Understand how agent discovers multiple items to process
3. Test two approaches:
   a. Single issue mentions/links multiple other issues
   b. Single issue has sub-issues linked
4. Verify workflows explain querying for bulk items
5. Confirm sub-issue handling is documented

## Expected Outcome
Bulk mode should work via:
- **Option 1**: Create meta-issue, link/mention other issues in description
- **Option 2**: Create parent issue, add sub-issues with matching workflow labels
- **Option 3**: Agent queries `list_issues` with workflow label to find all items
- Workflows document which approach to use when
- Clear guidance on iteration and processing multiple items

## Success Criteria
- [ ] Bulk mode documented in workflows
- [ ] Multiple discovery methods explained (links, sub-issues, queries)
- [ ] Clear when to use each approach
- [ ] Examples provided for each method
- [ ] Sub-issue processing documented (if applicable)

## Test Result
**Status**: PASS (documentation added)
**Notes**:
- ✅ Bulk mode documented in Process Modeling Workflow (Backlog-Driven modes: Single, Multiple, Smart)
- ✅ Sub-issue support confirmed via `issue_read` MCP tool (can get sub-issues)
- ✅ Added bulk processing guidance to WORKFLOW_TOPOLOGY_GUIDE.md
- Documentation covers:
  1. When bulk processing makes sense
  2. How to use sub-issues for grouping related work
  3. How to query and iterate sub-issues with workflow labels
  4. Examples of bulk processing patterns
- Process Modeling Workflow already has comprehensive bulk mode (lines 112-367)
- Other workflows can reference bulk patterns as needed
- Sub-issue linking available through GitHub MCP tools
