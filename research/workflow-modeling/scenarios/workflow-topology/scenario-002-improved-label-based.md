# Scenario: Improved System - Label-Based Workflow Designation

## Context

Testing proposed label-based workflow designation system.

This validates that labels can solve the central coordination problem.

## Starting Point

- New issue created in GitHub
- Automatically labeled with `workflow:triage`
- Triage workflow available to assess issues

## Steps to Follow

1. **Issue Creation**
   - GitHub issue created
   - Automatic label: `workflow:triage`
   - Triage workflow triggered

2. **Triage Workflow**
   - Query issues with `workflow:triage` label
   - Assess issue: Determine it's a research task
   - Apply label: `workflow:research`
   - Remove label: `workflow:triage`
   - Comment: "Designated to Research Workflow based on [reasoning]"

3. **Research Workflow**
   - Query issues with `workflow:research` label
   - Find the issue in queue
   - Process research work
   - When complete:
     - Create handover materials (file-based)
     - Apply label: `workflow:product-backlog`
     - Remove label: `workflow:research`
     - Comment: "Research complete. Handover materials in /product/backlog/[id]/"

4. **Product Prioritization Workflow**
   - Query issues with `workflow:product-backlog` label
   - Prioritize items
   - Select item for implementation
   - Apply label: `workflow:implementation`
   - Remove label: `workflow:product-backlog`

5. **Implementation Workflow**
   - Query issues with `workflow:implementation` label
   - Find issue in queue
   - Implement solution
   - When complete: Close issue

## Expected Outcome

✅ Centralized state (labels in GitHub)
✅ Clear workflow designation
✅ Each workflow has a queue to pull from
✅ Handover is label change + comment
✅ No merge conflicts (GitHub handles label updates)

## Success Criteria

- [ ] Labels provide centralized state
- [ ] Workflows can query their queue via GitHub API
- [ ] Label changes are atomic (no merge conflicts)
- [ ] Comments provide audit trail
- [ ] Re-designation is possible (change labels)
- [ ] Triage workflow can route new issues

## Test Result

**Status**: PASS (validates label-based approach solves core problems)

**Tabletop Simulation Notes**:

Simulated the proposed flow using GitHub capabilities:

**1. Issue Creation & Auto-Labeling**
- GitHub Actions can auto-label new issues (existing capability)
- Workflow trigger: `on: issues: types: [opened]`
- Action adds label `workflow:triage`
- ✅ Validated: GitHub Actions supports this

**2. Triage Workflow Query**
- Using GitHub CLI: `gh issue list --label "workflow:triage" --json number,title`
- Returns all issues with that label
- ✅ Validated: GitHub CLI/API supports label queries

**3. Label Change for Designation**
- Triage assesses issue, determines it's research
- Changes label: `gh issue edit <number> --add-label "workflow:research" --remove-label "workflow:triage"`
- Adds comment: `gh issue comment <number> --body "Designated to Research..."`
- ✅ Validated: GitHub CLI supports atomic label changes

**4. Research Workflow Picks Up**
- Queries: `gh issue list --label "workflow:research"`
- Finds issue in queue
- Processes work
- When complete: changes label to `workflow:product-backlog`
- ✅ Validated: Same label change pattern works

**5. Handover Chain**
- Product Backlog: queries `workflow:product-backlog` label
- Implementation: queries `workflow:implementation` label
- Each workflow changes label to hand over to next
- ✅ Validated: Label changes are centralized (GitHub is source of truth)

**Key Validations:**
- ✅ Labels solve centralization problem (no file branching)
- ✅ GitHub API/CLI supports required queries
- ✅ Label changes are atomic (no merge conflicts)
- ✅ Comments provide audit trail
- ✅ Re-designation is simple (change labels)
- ✅ Concurrent PRs safe (different issues, independent label changes)

**Limitations Identified:**
- ⚠️ Rich metadata still needs files (handover docs, attachments)
- ⚠️ Requires GitHub Actions for automation (e.g., auto-label on create)
- ⚠️ Workflows need update to query by label

**Conclusion**: Label-based approach is viable and solves the core centralization and concurrency problems.
