#!/usr/bin/env bash
#
# reset-plan.sh - Reset plan.md to clean state
#
# This script resets /research/workflow-modeling/plan.md to a clean state
# using the template, while preserving the Recent Completion and Archive sections.
#
# Usage:
#   ./reset-plan.sh [last_completed_date] [last_completed_description] [last_completed_archive_file]
#
# Arguments:
#   $1: Date of last completion (YYYY-MM-DD)
#   $2: Brief description of last completion
#   $3: Archive filename (YYYY-MM-DD-[name].md)
#
# Example:
#   ./reset-plan.sh "2025-11-08" "Multi-Item Backlog Processing" "2025-11-08-multi-item-processing.md"
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLAN_FILE="$SCRIPT_DIR/../plan.md"
ARCHIVE_DIR="$SCRIPT_DIR/../archive"

# Check if arguments provided
if [ $# -lt 3 ]; then
    echo "Usage: $0 <completion_date> <completion_description> <archive_file>"
    echo "Example: $0 \"2025-11-08\" \"Multi-Item Backlog Processing\" \"2025-11-08-multi-item-processing.md\""
    exit 1
fi

COMPLETION_DATE="$1"
COMPLETION_DESC="$2"
ARCHIVE_FILE="$3"

# Verify archive file exists
if [ ! -f "$ARCHIVE_DIR/$ARCHIVE_FILE" ]; then
    echo "Error: Archive file not found: $ARCHIVE_DIR/$ARCHIVE_FILE"
    exit 1
fi

# Get list of all archived plans (newest first)
ARCHIVE_LIST=""
if [ -d "$ARCHIVE_DIR" ]; then
    # Get all .md files, sort by date (newest first)
    for file in $(ls -t "$ARCHIVE_DIR"/*.md 2>/dev/null || true); do
        filename=$(basename "$file")
        # Extract description from first line of archive file
        desc=$(grep -m 1 "^# " "$file" | sed 's/^# //' || echo "Process modeling work")
        ARCHIVE_LIST="${ARCHIVE_LIST}- \`${filename}\` - ${desc}\n"
    done
fi

# Generate clean plan.md
cat > "$PLAN_FILE" << EOF
# Process Modeling Plan

## Current Work

**Status**: No active work

---

## How to Start New Work

When a new workflow improvement issue is assigned:

1. Update this section with issue details
2. Create test scenarios in \`/scenarios/[workflow-name]/\`
3. Execute tabletop simulations
4. Document results and refine workflows
5. Archive this plan when complete

## Recent Completion

**Last Completed**: $COMPLETION_DATE - $COMPLETION_DESC
**See Archive**: \`/research/workflow-modeling/archive/$ARCHIVE_FILE\`

## Archive

Previous work can be found in \`/research/workflow-modeling/archive/\`:
$(echo -e "$ARCHIVE_LIST")
EOF

echo "✅ Successfully reset plan.md to clean state"
echo "   Last Completed: $COMPLETION_DATE - $COMPLETION_DESC"
echo "   Archive: $ARCHIVE_FILE"
echo ""
echo "ℹ️  Review the file and commit when ready:"
echo "   git add research/workflow-modeling/plan.md"
echo "   git commit -m 'Reset plan.md to clean state after completion'"
