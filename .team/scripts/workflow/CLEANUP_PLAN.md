# Workflow Scripts Cleanup Plan

## Summary
Transition from `gh` CLI-based scripts to GitHub MCP tooling approach.

## Scripts Analysis

### ✅ KEEP (Still Useful)
- **query-workflow-queue.sh** - Can be updated to use `gh` CLI OR MCP as alternative
- **workflow-dashboard.sh** - Provides useful dashboard view

### ❌ DELETE (Redundant with MCP)
- **handover-issue.sh** - Copilot agents use MCP `issue_write` directly
- **migrate-labels.sh** - One-time script, already executed or not needed
- **migrate-backlog-to-issues.sh** - Migration complete, no longer needed

## Documentation Updates Needed

### WORKFLOW_TOPOLOGY_GUIDE.md
- Remove `gh` CLI commands from handover examples
- Update to show MCP tooling approach (GitHub MCP tools)
- Keep query examples as reference but note MCP alternative
- Update "Helper Scripts" section to reflect remaining scripts only

### COPILOT_GITHUB_SETUP.md
- Already correct - focuses on MCP server setup
- Minor updates to clarify MCP is primary approach

### .team/scripts/workflow/README.md
- Remove documentation for deleted scripts
- Update to reflect MCP-first approach
- Keep query and dashboard scripts

### Workflow Files (all 6 workflows)
- Update handover sections to use MCP tools instead of scripts
- Update queue query sections to show both `gh` and MCP approaches

## Implementation Steps

1. Delete redundant scripts
2. Update WORKFLOW_TOPOLOGY_GUIDE.md
3. Update .team/scripts/workflow/README.md
4. Update workflow files
5. Update PR description
