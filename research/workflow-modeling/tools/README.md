# Process Modeling Tools

This directory contains automation scripts and templates for process modeling work.

## Available Tools

### reset-plan.sh

Resets `plan.md` to a clean state after completing process modeling work.

**Purpose**: Automates the plan.md reset process to prevent duplicates and ensure consistent structure.

**Usage**:
```bash
cd /research/workflow-modeling/tools
./reset-plan.sh <completion_date> <completion_description> <archive_file>
```

**Arguments**:
- `completion_date`: Date of last completion (YYYY-MM-DD)
- `completion_description`: Brief description of completed work
- `archive_file`: Archive filename (YYYY-MM-DD-[name].md)

**Example**:
```bash
./reset-plan.sh "2025-11-08" "Multi-Item Backlog Processing" "2025-11-08-multi-item-processing.md"
```

**What it does**:
1. Validates the archive file exists
2. Scans all archived plans and extracts their descriptions
3. Generates a clean plan.md with:
   - Current Work: No active work
   - Recent Completion: Your specified completion
   - Archive: Complete list of all archived plans (newest first)
4. Replaces plan.md content atomically (no duplicates!)

**When to use**:
- After completing process modeling work (step 7 in "Completing Process Modeling Work")
- Before committing the final state of your process modeling PR
- Anytime plan.md has duplicate sections or incorrect structure

### plan-template.md

Template file for manual plan.md creation if needed.

**Usage**:
```bash
cp plan-template.md ../plan.md
# Then manually edit Recent Completion and Archive sections
```

**Note**: The `reset-plan.sh` script is preferred as it automatically populates the Archive section.

## Adding New Tools

When adding new automation scripts:

1. Place script in this directory
2. Make it executable: `chmod +x script-name.sh`
3. Document it in this README
4. Reference it in `PROCESS_MODELING_WORKFLOW.md` where appropriate
5. Test thoroughly before committing

## Maintenance

These tools are maintained as part of the Process Modeling Workflow. If you encounter issues or have suggestions:

1. Add entry to `.github/workflow-improvements.md`
2. Create a workflow improvement issue
3. Follow the Process Modeling Workflow to update the tools
