#!/bin/bash
# Workflow State Dashboard
# Displays current state of all workflows
#
# Usage: ./workflow-dashboard.sh

echo "=== Workflow State Dashboard ==="
echo ""
echo "Note: This script requires GitHub CLI (gh) and GH_TOKEN to be set."
echo ""

# Check if gh is available
if ! command -v gh &> /dev/null; then
    echo "Error: GitHub CLI (gh) is not installed."
    echo "Install from: https://cli.github.com/"
    exit 1
fi

# Check if GH_TOKEN is set
if [ -z "$GH_TOKEN" ]; then
    echo "Warning: GH_TOKEN is not set. Authentication may fail."
    echo "Set GH_TOKEN in your environment or configure gh auth."
    echo ""
fi

declare -a WORKFLOWS=("triage" "research" "implementation" "tech-debt" "product-backlog" "process-modeling")

echo "Open Issues by Workflow:"
echo "------------------------"

total=0
for wf in "${WORKFLOWS[@]}"; do
    count=$(gh issue list --label "workflow:$wf" --state open --json number --jq 'length' 2>/dev/null || echo "0")
    printf "  %-20s: %d open issues\n" "workflow:$wf" "$count"
    total=$((total + count))
done

echo "------------------------"
echo "  Total: $total open issues"
echo ""

# Show recent handovers
echo "Recent Workflow Transitions (last 10):"
echo "---------------------------------------"
gh issue list --search "Handover in:comments" --state all --limit 10 --json number,title,state --jq '.[] | "  #\(.number): \(.title) [\(.state)]"' 2>/dev/null || echo "  No handovers found or GH_TOKEN not set"
echo ""
