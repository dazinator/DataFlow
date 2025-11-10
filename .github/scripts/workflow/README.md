# Workflow Helper Scripts

This directory contains helper scripts for the GitHub label-based workflow topology system.

## Overview

The workflow system uses **GitHub MCP (Model Context Protocol) tools** for Copilot agents to interact with issues programmatically. These scripts provide convenient command-line utilities for manual queries and dashboard views.

## Scripts

### query-workflow-queue.sh

Query all issues designated to a specific workflow.

**Usage**:
```bash
./query-workflow-queue.sh <workflow-name>
```

**Example**:
```bash
./query-workflow-queue.sh research
./query-workflow-queue.sh implementation
./query-workflow-queue.sh product-backlog
```

**What it does**:
- Uses GitHub CLI (`gh`) to query issues by workflow label
- Displays issue number, title, URL, and creation date
- Useful for quick manual checks and CI/CD integrations

**Alternative (for Copilot agents)**:
Copilot agents use GitHub MCP tools directly:
```
list_issues(owner="owner", repo="repo", labels=["workflow:research"], state="OPEN")
```

### workflow-dashboard.sh

View high-level state of all workflows and recent transitions.

**Usage**:
```bash
./workflow-dashboard.sh
```

**What it displays**:
- Count of issues in each workflow
- Summary of workflow distribution
- Useful for monitoring workflow health

**Alternative (for Copilot agents)**:
Copilot agents can query each workflow programmatically using GitHub MCP `list_issues` tool with different workflow labels.

## Complete Documentation

See [Workflow Topology System Guide](../../../.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete documentation including:
- Label schema
- GitHub MCP tooling for Copilot agents
- Workflow transition patterns
- Querying workflow queues
- Creating and linking issues
- Troubleshooting

## GitHub MCP Tooling (Primary Approach)

Copilot agents interact with GitHub issues using **GitHub MCP tools** configured via `.github/docs/COPILOT_GITHUB_SETUP.md`.

### Available MCP Tools

**Read Operations**:
- `list_issues` - Query issues by labels, state, filters
- `issue_read` - Get issue details, comments, labels
- `search_issues` - Advanced issue search

**Write Operations**:
- `issue_write` - Create or update issues
- `add_issue_comment` - Add comments to issues
- `update_pull_request` - Update PR details

### Example: Handover Issue

Instead of using scripts, Copilot agents use MCP tools directly:

```python
# Remove old workflow label and add new one
issue_write(
    method="update",
    owner="owner",
    repo="repo",
    issue_number=123,
    labels=["workflow:implementation", "feature"]  # Removes all other workflow labels
)

# Add comment explaining handover
add_issue_comment(
    owner="owner",
    repo="repo",
    issue_number=123,
    body="Handed over from Research to Implementation. Research validated approach. See /research/topic/ for details."
)
```

## Requirements

**For Scripts**:
- GitHub CLI (`gh`) installed and authenticated
- Appropriate repository permissions

**For Copilot Agents** (GitHub MCP):
- Fine-grained Personal Access Token configured
- MCP server setup completed
- See `.github/docs/COPILOT_GITHUB_SETUP.md` for complete setup instructions
