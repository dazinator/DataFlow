#!/bin/bash
# Handover issue to next workflow
# Usage: ./handover-issue.sh ISSUE_NUMBER FROM_WORKFLOW TO_WORKFLOW "REASON"
#
# Example: ./handover-issue.sh 123 research implementation "Research validated approach"

ISSUE=$1
FROM_WORKFLOW=$2
TO_WORKFLOW=$3
REASON=$4

if [ -z "$ISSUE" ] || [ -z "$FROM_WORKFLOW" ] || [ -z "$TO_WORKFLOW" ] || [ -z "$REASON" ]; then
  echo "Usage: $0 ISSUE_NUMBER FROM_WORKFLOW TO_WORKFLOW \"REASON\""
  echo "Example: $0 123 research implementation \"Research validated approach\""
  exit 1
fi

echo "=== Handing over Issue #$ISSUE ==="
echo "From: workflow:$FROM_WORKFLOW"
echo "To: workflow:$TO_WORKFLOW"
echo "Reason: $REASON"

# Change labels
gh issue edit $ISSUE \
  --remove-label "workflow:$FROM_WORKFLOW" \
  --add-label "workflow:$TO_WORKFLOW"

# Post handover comment
gh issue comment $ISSUE \
  --body "🔄 **Handover: $FROM_WORKFLOW → $TO_WORKFLOW**

$REASON

See workflow documentation for next steps:
- Triage: \`.team/workflows/TRIAGE_WORKFLOW.md\` (to be created)
- Research: \`.team/workflows/RESEARCH_WORKFLOW.md\`
- Implementation: \`.team/workflows/IMPLEMENTATION_WORKFLOW.md\`
- Tech Debt: \`.team/workflows/TECH_DEBT_WORKFLOW.md\`
- Product: \`.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md\`
- Process Modeling: \`.team/workflows/PROCESS_MODELING_WORKFLOW.md\`"

echo "✅ Handover complete!"
