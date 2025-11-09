# Scenario 007: Verify Script Paths in Documentation

## Context

Testing that all script paths referenced in workflow documentation are correct and accessible.

## Starting Point

- All workflows updated with topology integration
- Scripts moved to `.team/scripts/workflow/`
- Documentation references scripts

## Steps to Follow

### 1. Check Research Workflow

Read `.team/workflows/RESEARCH_WORKFLOW.md`:

1. Find "Workflow Queue" section
2. Verify script path: `./.team/scripts/workflow/query-workflow-queue.sh research`
3. Find "Handover to Next Workflow" section
4. Verify script paths in handover examples

**Expected**: All paths start with `./.team/scripts/workflow/`

### 2. Check Implementation Workflow

Read `.team/workflows/IMPLEMENTATION_WORKFLOW.md`:

1. Find "Workflow Queue" section
2. Verify query script path
3. Find "Handover to Next Workflow" section
4. Verify handover script paths

**Expected**: All paths correct

### 3. Check All Other Workflows

Repeat for:
- `.team/workflows/TECH_DEBT_WORKFLOW.md`
- `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md`
- `.team/workflows/PROCESS_MODELING_WORKFLOW.md`
- `.team/workflows/TRIAGE_WORKFLOW.md` (already deployed)

### 4. Check Workflow Topology Guide

Read `.team/workflows/WORKFLOW_TOPOLOGY_GUIDE.md`:

1. Find "Helper Scripts" section
2. Verify: "All scripts are located in `.team/scripts/workflow/`"
3. Check all example commands
4. Verify script paths are consistent

### 5. Check Copilot Instructions

Read `.github/copilot-instructions.md`:

1. Find "Workflow Topology System" section
2. Verify query script path
3. Verify handover script path
4. Check repository structure shows scripts location

### 6. Test Script Execution Paths

From repository root:

```bash
# These should all work (or show help if no labels exist yet)
./.team/scripts/workflow/query-workflow-queue.sh --help
./.team/scripts/workflow/handover-issue.sh
./.team/scripts/workflow/workflow-dashboard.sh
./.team/scripts/workflow/migrate-labels.sh --help
```

**Expected**: Scripts are executable and show help/usage

## Expected Outcome

- All script paths in documentation are correct
- Paths are consistent across all files
- Scripts are executable from repository root
- No broken references or 404s

## Success Criteria

- [x] Research workflow paths correct
- [x] Implementation workflow paths correct
- [x] Tech Debt workflow paths correct
- [x] Product Prioritization workflow paths correct
- [x] Process Modeling workflow paths correct
- [x] Triage workflow paths correct
- [x] Workflow Topology Guide paths correct
- [x] Copilot Instructions paths correct
- [x] Scripts are executable
- [x] All paths start with `./.team/scripts/workflow/`
- [x] No references to old prototype location
- [x] Repository structure section is accurate

## Test Result

**Status**: PASS ✅

**Notes**: 
- All script paths verified across all workflows
- TRIAGE workflow had old prototype paths - FIXED
- All scripts confirmed executable (chmod +x)
- All paths now consistently use `./.team/scripts/workflow/`
- No broken references found
- Repository structure in copilot instructions accurate

**Issues Found and Fixed**:
1. TRIAGE_WORKFLOW.md had old paths (`./research/workflow-topology-design/handover/prototype/`) - Updated to new paths
2. All other workflows already had correct paths

**Verification**:
```bash
# All workflows now reference correct paths
grep -c "\.team/scripts/workflow" .team/workflows/*_WORKFLOW.md
# RESEARCH: 4, IMPLEMENTATION: 5, TECH_DEBT: 4, PRODUCT_PRIORITIZATION: 3, 
# PROCESS_MODELING: 2, TRIAGE: 5

# All scripts are executable
ls -la .team/scripts/workflow/*.sh
# All have -rwxrwxr-x permissions
```

## Observations

This scenario ensures:
- No broken references after script migration
- Consistency across all documentation
- Scripts are accessible as documented
- Users can copy-paste commands and they work
