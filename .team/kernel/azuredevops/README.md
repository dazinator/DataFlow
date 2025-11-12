# Azure DevOps Kernel Driver (Placeholder)

**Status**: 🚧 **Placeholder** - Not yet implemented  
**Created**: 2025-11-12 (Phase 5)  
**Purpose**: Future Azure DevOps platform support

---

## Overview

This directory is a placeholder for the future Azure DevOps kernel driver. When implemented, it will provide the same 12 semantic operations as the GitHub driver, but mapped to Azure DevOps APIs.

## Semantic Operations to Implement

The Azure DevOps driver must implement all 12 semantic operations defined in the kernel layer:

### Work Item Query Operations

1. **`query_work_items_by_duty(duty)`**
   - Map to: Azure DevOps work item query API
   - Filter: Work items with tag matching duty
   - Return: List of work item IDs

2. **`get_work_item_details(work_item_id)`**
   - Map to: Azure DevOps work item details API
   - Return: Work item title, description, state, assignee, etc.

3. **`get_work_item_duty(work_item_id)`**
   - Map to: Azure DevOps work item tag inspection
   - Return: Current duty tag value

### Work Item Update Operations

4. **`assign_work_item_to_duty(work_item_id, duty)`**
   - Map to: Azure DevOps work item tag update API
   - Action: Update duty tag to new value

5. **`add_work_item_comment(work_item_id, text)`**
   - Map to: Azure DevOps work item comment API
   - Action: Add comment to work item

6. **`update_work_item(work_item_id, fields)`**
   - Map to: Azure DevOps work item update API
   - Action: Update specified fields (title, description, state, etc.)

### Hierarchy Operations

7. **`get_parent_work_item(work_item_id)`**
   - Map to: Azure DevOps work item hierarchy API
   - Return: Parent work item ID if exists

8. **`is_multi_phase(work_item_id)`**
   - Map to: Azure DevOps work item tag/hierarchy inspection
   - Return: True if work item has multi-phase tag or has children

### Work Item Creation Operations

9. **`create_work_item(type, title, description, duty, labels, ...)`**
   - Map to: Azure DevOps work item creation API
   - Action: Create new work item with specified properties

10. **`create_sub_work_item(parent_id, type, title, description, duty, ...)`**
    - Map to: Azure DevOps work item creation with parent link
    - Action: Create work item and link to parent

### Feedback Operations

11. **`submit_feedback(category, content, context)`**
    - Map to: Azure DevOps feedback work item creation
    - Action: Create feedback work item in designated backlog

12. **`query_feedback(category, filters)`**
    - Map to: Azure DevOps feedback work item query
    - Return: Matching feedback work items

---

## Implementation Plan

### Phase 1: Driver Structure (Est: 3 days)

**Files to Create**:
- `README.md` (this file - expand with implementation details)
- `operations.md` - Azure DevOps semantic operation mappings
- `examples.md` - Usage examples for each operation
- `config.md` - Azure DevOps configuration requirements

### Phase 2: API Integration (Est: 4 days)

**Implementation**:
- Azure DevOps REST API client
- Authentication and authorization
- Work item query implementation
- Work item update implementation
- Hierarchy operations
- Comment operations

### Phase 3: Testing (Est: 3 days)

**Tests to Create**:
- Semantic contract tests (similar to GitHub)
- Success cases for all 12 operations
- Failure cases (API errors, auth failures)
- Edge cases (empty results, large results)
- Integration tests with procedures and duties

### Total Estimated Effort: 7-10 days

---

## Configuration Requirements

### Azure DevOps Connection

**Required Settings** (to be added to `.team/kernel/config.yaml`):

```yaml
platform: azuredevops  # or 'github'

azuredevops:
  organization: your-org
  project: your-project
  pat_token: ${AZDO_PAT}  # Personal Access Token from environment
  api_version: "7.0"
```

### Work Item Tag Schema

Azure DevOps uses **tags** instead of labels. The duty system will use tags like:

- `workflow:triage` - Triage duty
- `workflow:research` - Research duty
- `workflow:implementation` - Implementation duty
- `workflow:tech-debt` - Tech debt duty
- `workflow:product-backlog` - Product prioritization duty
- `workflow:process-modeling` - Process modeling duty

### Work Item Types

Recommended Azure DevOps work item types:

- **User Story** - For research and implementation work items
- **Task** - For sub-work-items in multi-phase plans
- **Bug** - For tech debt and defects
- **Epic** - For large multi-phase parent work items

---

## Domain Patterns

### Azure DevOps Kernel Patterns

To be added to `.team/kernel/domains.yaml`:

```yaml
azuredevops:
  patterns:
    - 'wit.create_work_item'
    - 'wit.update_work_item'
    - 'wit.get_work_item'
    - 'wit.query_work_items'
    - 'wit.add_comment'
    - 'wit.get_comments'
    - 'wit.update_tags'
    - 'wit.get_hierarchy'
    - 'wit.create_link'
    - 'work_item'
    - 'work_items'
    # Add more as needed
```

---

## Migration Path

### From GitHub to Azure DevOps

**Steps**:

1. **Update Config**: Set `platform: azuredevops` in `.team/kernel/config.yaml`
2. **Provide Credentials**: Set `AZDO_PAT` environment variable
3. **Test Connection**: Verify Azure DevOps API access
4. **Migrate Work Items**: Import or create work items in Azure DevOps
5. **Update Tags**: Apply duty tags to work items
6. **Test Procedures**: Validate all procedures work with Azure DevOps
7. **Test Duties**: Validate all duties work with Azure DevOps
8. **Run Leak Detection**: Ensure no platform leaks

### Hybrid Operation

The kernel architecture supports running both platforms simultaneously:

- Different repositories can use different platforms
- Orchestration layer selects driver based on config
- All procedures and duties work with both platforms

---

## API Reference

### Azure DevOps REST API Endpoints

**Work Items**:
- `GET https://dev.azure.com/{org}/{project}/_apis/wit/workitems/{id}`
- `POST https://dev.azure.com/{org}/{project}/_apis/wit/workitems/${type}`
- `PATCH https://dev.azure.com/{org}/{project}/_apis/wit/workitems/{id}`

**Work Item Queries**:
- `POST https://dev.azure.com/{org}/{project}/_apis/wit/wiql`

**Comments**:
- `POST https://dev.azure.com/{org}/{project}/_apis/wit/workitems/{id}/comments`
- `GET https://dev.azure.com/{org}/{project}/_apis/wit/workitems/{id}/comments`

**Tags**:
- `PATCH https://dev.azure.com/{org}/{project}/_apis/wit/workitems/{id}` (update tags field)

**Documentation**: https://learn.microsoft.com/en-us/rest/api/azure/devops/

---

## Testing Strategy

### Unit Tests (Kernel Layer)

Test each semantic operation in isolation:

```markdown
# Test: create_work_item - Success Case

## Semantic Operation
create_work_item(
    type="User Story",
    title="Test Item",
    description="Test description",
    duty="research"
)

## Expected Azure DevOps API Call
POST https://dev.azure.com/{org}/{project}/_apis/wit/workitems/$User Story
Body:
  - op: add, path: /fields/System.Title, value: "Test Item"
  - op: add, path: /fields/System.Description, value: "Test description"
  - op: add, path: /fields/System.Tags, value: "workflow:research"

## Expected Result
work_item_id = "123"
```

### Integration Tests

Test procedures and duties using Azure DevOps driver:

- Duty assignment with Azure DevOps tags
- Multi-phase work item creation
- Handover between duties
- Feedback submission

### Leak Detection

Run leak detection to ensure no Azure DevOps-specific code leaked:

```bash
.team/scripts/check-kernel-leaks.sh
```

Should find zero leaks outside kernel layer.

---

## Dependencies

### Required Libraries (Example - .NET)

If implementing in .NET:

```xml
<PackageReference Include="Microsoft.TeamFoundationServer.Client" Version="16.205.0" />
<PackageReference Include="Microsoft.VisualStudio.Services.Client" Version="16.205.0" />
```

### Required Permissions

Azure DevOps Personal Access Token needs:

- **Work Items (Read, Write, Manage)** - For creating and updating work items
- **Project and Team (Read)** - For querying project information

---

## Related Documentation

- [Kernel Layer Overview](../README.md) - Kernel architecture and semantic operations
- [GitHub Driver](../github/README.md) - Reference implementation
- [Semantic Language](../../../docs/design/prompt-engineering/semantic-language.md) - Semantic operations specification
- [Testing Framework](../../../docs/design/prompt-engineering/testing-framework.md) - Testing methodology

---

## Status

**Current**: Placeholder structure created  
**Next**: Implementation when Azure DevOps platform support is needed  
**Priority**: Medium (GitHub driver sufficient for now)

---

**Created**: 2025-11-12  
**Author**: Phase 5 - Cleanup & Validation  
**Status**: Ready for future implementation
