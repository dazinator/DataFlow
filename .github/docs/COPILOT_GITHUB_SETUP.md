# GitHub Copilot Agent Setup Guide

This guide explains how to configure GitHub Copilot agents to interact with GitHub issues via the MCP (Model Context Protocol) server.

## Overview

GitHub Copilot agents need write access to GitHub issues to:
- Create new issues from workflow processes
- Update issue labels for workflow transitions (handovers)
- Add comments to link related issues and artifacts
- Manage product backlog items as GitHub issues

This is accomplished by configuring a Personal Access Token (PAT) and the GitHub MCP server.

## Prerequisites

- Repository admin access
- Ability to create fine-grained Personal Access Tokens

## Setup Steps

### 1. Create Fine-Grained Personal Access Token

1. **Navigate to GitHub Settings**:
   - Go to https://github.com/settings/tokens?type=beta
   - Click "Generate new token" → "Fine-grained token"

2. **Configure Token Settings**:
   - **Token name**: `copilot-agent-workflow-access`
   - **Description**: `Allows Copilot agents to create and manage issues for workflow topology system`
   - **Expiration**: Choose appropriate expiration (90 days recommended)
   - **Repository access**: Select "Only select repositories"
     - Choose: `uniun-technology/lib-dataflow`

3. **Set Repository Permissions**:
   Select the following permissions with **Read and write** access:
   - **Issues**: Read and write
   - **Pull requests**: Read and write
   - **Contents**: Read and write
   - **Metadata**: Read-only (automatically selected)
   - **Workflows**: Read and write (if agents need to trigger workflows)

4. **Generate Token**:
   - Click "Generate token"
   - **IMPORTANT**: Copy the token immediately - you won't see it again
   - Store securely in password manager

### 2. Configure Repository Environment

1. **Navigate to Repository Settings**:
   - Go to repository → Settings → Environments
   - Click "New environment"

2. **Create Copilot Environment**:
   - **Name**: `copilot`
   - Click "Configure environment"

3. **Add Environment Secret**:
   - Under "Environment secrets", click "Add secret"
   - **Name**: `COPILOT_MCP_GITHUB_PERSONAL_ACCESS_TOKEN`
   - **Value**: Paste the PAT token created in step 1
   - Click "Add secret"

### 3. Configure MCP Server for Copilot Agents

1. **Navigate to Copilot Settings**:
   - Organization settings → Copilot → Agent settings
   - Or: Repository settings → Copilot (if repo-level configuration is available)

2. **Add MCP Server Configuration**:

Add the following JSON configuration to enable the GitHub MCP server:

```json
{
  "mcpServers": {
    "github-mcp-server": {
      "type": "http",
      "url": "https://api.githubcopilot.com/mcp",
      "tools": ["*"],
      "headers": {
        "Authorization": "Bearer ${COPILOT_MCP_GITHUB_PERSONAL_ACCESS_TOKEN}"
      }
    }
  }
}
```

**Configuration Details**:
- `type`: `http` - Uses HTTP protocol for MCP communication
- `url`: GitHub's MCP server endpoint
- `tools`: `["*"]` - Enables all available tools (read and write operations)
- `headers.Authorization`: References the environment secret created in step 2

3. **Save Configuration**:
   - Save the MCP server configuration
   - Copilot agents will now have access to GitHub write operations

### 4. Create Required GitHub Labels

Create the workflow labels that the topology system uses:

```bash
# Workflow state labels
gh label create "workflow:triage" \
  --description "New issues awaiting assessment and routing" \
  --color "0E8A16" --force

gh label create "workflow:research" \
  --description "Research and approach validation workflow" \
  --color "1D76DB" --force

gh label create "workflow:implementation" \
  --description "Active implementation workflow" \
  --color "0052CC" --force

gh label create "workflow:tech-debt" \
  --description "Technical debt discovery and analysis workflow" \
  --color "FBCA04" --force

gh label create "workflow:product-backlog" \
  --description "Product backlog items awaiting prioritization" \
  --color "D93F0B" --force

gh label create "workflow:process-modeling" \
  --description "Process improvement and workflow modeling" \
  --color "5319E7" --force

# Category labels
gh label create "tech-debt" \
  --description "Technical debt item" \
  --color "FEF2C0" --force

gh label create "feature" \
  --description "New feature or enhancement" \
  --color "A2EEEF" --force

gh label create "bug" \
  --description "Bug or defect" \
  --color "D73A4A" --force
```

### 5. Create Required Parent Issues

Create parent tracker issues that workflows depend on:

#### Workflow Feedback Tracker

**Complete Documentation**: See [Workflow Feedback Tracker Guide](./WORKFLOW_FEEDBACK_TRACKER.md) for comprehensive details on purpose, usage, and integration.

**Required for**: Process Modeling workflow (bulk processing mode)

Create an issue with the following details:

- **Title**: `[Workflow Feedback] Tracker` (exact - workflows search by this title)
- **Label**: `workflow:process-modeling`
- **Body**: See template below

**Why this is required**: The Process Modeling workflow queries sub-issues from this parent to find feedback items. The title is used for dynamic lookup, making the system resilient to issue deletion/recreation.

**Template**:
```markdown
# Workflow Feedback Tracker

This issue tracks feedback and improvement suggestions for all workflows.

Each suggestion is tracked as a child issue below. **Open children = not yet addressed**.

## How This Works

1. **Workflow agents** create child feedback issues under this parent after completing work
2. **Process Modeling workflow** processes open children using standard process modeling methodology
3. **Closed children** = implemented improvements

See [Process Modeling Workflow](/.team/prompts/PROCESS_MODELING_WORKFLOW.md) for details.

## Providing Feedback

When completing work on an issue:

1. Search for this parent issue: `[Workflow Feedback] Tracker`
2. Create child issue with your feedback
3. Link child to this parent using `sub_issue_write` MCP tool

## Query Open Feedback

**For Process Modeling**:

```python
# Find parent tracker by title
tracker_results = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query='"[Workflow Feedback] Tracker" in:title state:open'
)

# Get open children from parent
if tracker_results and len(tracker_results) > 0:
    parent = issue_read(
        method="get_sub_issues",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=tracker_results[0].number
    )
    open_children = [c for c in parent if c.state == "open"]
```
```

**Automation**: This issue can be created manually or via script. The key is maintaining the exact title for workflow lookups.

### 6. Verify Configuration

Test that Copilot agents can interact with GitHub issues:

1. **Via Copilot Agent**:
   - Assign an issue to Copilot with instructions to query workflow queue
   - Example: `@copilot list all issues with workflow:triage label`

2. **Expected Behavior**:
   - Agent should successfully query and list issues
   - Agent should be able to create test issues
   - Agent should be able to update labels and add comments

3. **Troubleshooting**:
   - **"Not authenticated"**: Check environment secret is configured correctly
   - **"Permission denied"**: Verify PAT has required permissions
   - **"Label not found"**: Create workflow labels using commands in step 4

## Security Considerations

### Token Scope
- Use fine-grained PAT (not classic PAT) for better security
- Limit to specific repository only
- Use shortest acceptable expiration period
- Set minimum required permissions

### Token Rotation
- Set calendar reminder to rotate token before expiration
- Update environment secret when rotating token
- Test after rotation to ensure continuity

### Access Control
- Only repository admins can configure environment secrets
- Copilot agents inherit permissions from PAT
- Review agent activity periodically via audit logs

## Maintenance

### Token Expiration
When PAT expires:
1. Create new fine-grained PAT following step 1
2. Update environment secret with new token
3. No MCP server configuration changes needed

### Permission Changes
If workflow requirements change:
1. Edit existing PAT permissions in GitHub settings
2. No need to update environment secret unless token changes

### Troubleshooting

**Common Issues**:

1. **Agent cannot create issues**:
   - Verify PAT has "Issues: Read and write" permission
   - Check environment secret name matches MCP configuration
   - Confirm PAT hasn't expired

2. **Agent cannot update labels**:
   - Verify labels exist in repository
   - Check PAT has "Issues: Read and write" permission

3. **Agent cannot access MCP tools**:
   - Verify MCP server configuration is saved
   - Check environment is named `copilot`
   - Confirm environment secret exists

## References

- **Workflow Topology Guide**: `.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md`
- **Helper Scripts**: `.github/scripts/workflow/`
- **Fine-grained PAT Documentation**: https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#creating-a-fine-grained-personal-access-token
- **GitHub Copilot Documentation**: https://docs.github.com/en/copilot

## Support

For issues with this setup:
1. Check troubleshooting section above
2. Verify all steps completed correctly
3. Review audit logs for permission issues
4. Contact repository administrators

---

**Last Updated**: 2025-11-09
**Version**: 1.0
