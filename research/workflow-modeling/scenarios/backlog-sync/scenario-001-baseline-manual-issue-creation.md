# Scenario 001: Baseline - Manual Issue Creation from Backlog Item

## Type

**baseline** - Testing current workflow state without automation

## Context

Currently, backlog items exist in `/product/backlog/*.md` but are not automatically synced to GitHub issues. Teams must manually create GitHub issues referencing backlog items.

This scenario tests the current manual process to establish a baseline for comparison with the automated sync workflow.

## Starting Point

- Repository with active product backlog
- 8 backlog items in `/product/backlog/`
- No GitHub issues created for backlog items
- Implementation team wants to work on a backlog item
- Product team has prioritized items in `/product/prioritization.md`

## Steps to Follow

### 1. Implementation Team Checks Prioritization

Implementation team reads `/product/prioritization.md`:

**Action**: View prioritization file
**Expected**: See list of up to 5 prioritized items with rationale

### 2. Implementation Team Reads Backlog Item

Implementation team opens highest priority backlog item file:

**Action**: Open `/product/backlog/[item-id].md`
**Expected**: Read Summary, Context, Implementation Guidance, Success Criteria

### 3. Implementation Team Creates GitHub Issue Manually

To track work, implementation team creates a GitHub issue:

**Action**: Create issue via GitHub UI
**Steps**:
1. Go to Issues → New Issue
2. Choose "Implementation" template
3. Fill in title from backlog item
4. Copy relevant sections from backlog item to issue description
5. Add labels manually (tech-debt, etc.)
6. Reference backlog file path in issue body
7. Create issue

**Expected**: Issue created with manual content copy

### 4. Implementation Team Links Issue to Backlog

**Action**: Update backlog item file to add issue reference
**Steps**:
1. Open backlog item file
2. Add "GitHub Issue: #123" to References section
3. Commit change

**Expected**: Bidirectional link established (issue → backlog, backlog → issue)

### 5. Implementation Work Proceeds

**Action**: Implementation team works on the issue
**Expected**: Normal implementation workflow

### 6. Implementation Team Updates Backlog on Completion

**Action**: Update backlog item status
**Steps**:
1. Open backlog item file
2. Change status to "Completed"
3. Add PR reference
4. Archive to `/product/resolved/YYYY-MM/`
5. Update prioritization.md

**Expected**: Backlog item archived, prioritization updated

### 7. Implementation Team Closes GitHub Issue

**Action**: Close the GitHub issue
**Expected**: Issue closed, marked as complete

## Expected Outcome

**End state**:
- ✅ GitHub issue created for backlog item
- ✅ Backlog item references GitHub issue
- ✅ Work tracked in both places
- ✅ Both updated when work completes
- ✅ Backlog item archived
- ✅ Issue closed

**Pain points identified**:
- ⏱️ Manual issue creation takes 3-5 minutes
- 📋 Copy-paste from backlog to issue is error-prone
- 🔄 Requires updating both backlog file and issue
- 🏷️ Labels must be added manually
- 🔗 Easy to forget to link issue back to backlog
- 📊 No visibility of backlog items as issues before selection

## Success Criteria

- [ ] Instructions were clear and unambiguous
- [ ] No gaps or missing information
- [ ] Workflow led to expected outcome
- [ ] No confusion or back-tracking needed
- [ ] Pain points documented for comparison

## Test Result

**Status**: PASS ✅

**Time to complete**: ~5-8 minutes per backlog item

**Pain points encountered**:
1. **Manual copy-paste** - Had to copy Summary, Context, and Implementation Guidance sections from backlog file to issue
2. **Label selection** - Had to manually determine and apply labels (tech-debt, etc.)
3. **Formatting inconsistency** - Different people might format issues differently
4. **Missing references** - Easy to forget to add backlog file path link
5. **Bidirectional linking** - Must remember to update backlog file with issue number
6. **Visibility before sync** - Backlog items not visible until someone creates issue
7. **Scaling problem** - With 8 backlog items, would take 40-60 minutes to create all issues

**Notes**:

Current workflow is functional but inefficient:
- Product Prioritization system works well (`/product/prioritization.md`)
- Backlog item template provides good structure
- Implementation workflow references backlog items
- But creating GitHub issues is manual, time-consuming, error-prone

The bigger visibility problem: **Backlog items are hidden until someone creates an issue**. Product team can prioritize, but implementation team and stakeholders can't easily see what's in the backlog without browsing markdown files.

**Improvements needed in baseline**:
- Implementation workflow could have example of creating issue from backlog item
- Template for issue body when creating from backlog item
- Checklist for what to include in issue (labels, links, etc.)

---

## Metrics to Capture

For comparison with automated sync scenario:

- **Time to create issue**: 5-8 minutes per item
- **Errors made**: High risk (forgetting links, labels, inconsistent formatting)
- **Steps required**: 10-12 manual steps
- **Manual interventions**: All steps are manual
- **Backlog-issue drift risk**: HIGH (must update both places manually)
