# Scenario: Baseline - Copilot Agent Uses MCP Tools for Issue Queries

## Context
Testing that current MCP documentation is sufficient for a Copilot agent to query workflow issues without needing bash scripts.

## Starting Point
- Copilot agent assigned to a research issue
- Agent needs to find all research issues to understand backlog
- Agent starts from `.github/copilot-instructions.md`

## Steps to Follow
1. Read `.github/copilot-instructions.md` → Find "Querying Your Workflow Queue" section
2. Follow guidance to query research workflow
3. Should find MCP tool guidance (not bash script guidance as primary)
4. Use `list_issues` MCP tool with workflow label filter
5. Successfully retrieve list of research issues

## Expected Outcome
- Instructions point to MCP tools as primary approach
- WORKFLOW_TOPOLOGY_GUIDE.md provides comprehensive MCP examples
- Bash scripts mentioned only as "alternative for manual use"
- Agent can query issues without confusion

## Success Criteria
- [ ] MCP tools are documented as primary approach
- [ ] Examples are clear and actionable
- [ ] Bash scripts are de-emphasized (secondary/manual only)
- [ ] No workflow leads agent to bash scripts first
- [ ] WORKFLOW_TOPOLOGY_GUIDE.md is comprehensive

## Test Result
**Status**: PASS
**Notes**:
- Copilot-instructions.md now shows MCP tools as primary approach (lines 247-270)
- Examples demonstrate MCP tool usage with `list_issues`, `issue_write`, `add_issue_comment`
- Bash scripts clearly marked as "For Manual/CI Use" (secondary)
- WORKFLOW_TOPOLOGY_GUIDE.md has comprehensive MCP documentation
- All workflow files reference MCP tools first
- Workflows consistently direct to WORKFLOW_TOPOLOGY_GUIDE.md for complete examples
- ✅ MCP tools are documented as primary
- ✅ Examples are clear and actionable
- ✅ Bash scripts are de-emphasized (secondary/manual only)
- ✅ Workflow leads agents to MCP tools first
- ✅ WORKFLOW_TOPOLOGY_GUIDE.md is comprehensive
