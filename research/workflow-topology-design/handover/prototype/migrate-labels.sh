#!/bin/bash
# Migration script: Add workflow labels to existing issues
# This script adds workflow labels to existing issues based on current state

echo "=== Workflow Topology Migration Script ==="
echo ""
echo "This script will add workflow labels to existing open issues."
echo "Press Ctrl+C to cancel, or Enter to continue..."
read

# Create labels if they don't exist
echo "Creating workflow labels..."
for label in triage research implementation tech-debt product-backlog process-modeling; do
  gh label create "workflow:$label" \
    --description "Designated to $label workflow" \
    --color "0E8A16" \
    --force 2>/dev/null || echo "  Label workflow:$label already exists"
done

echo ""
echo "=== Migrating existing issues ==="

# Label research issues (issues with 'research' label)
echo "1. Migrating research issues..."
gh issue list --search "is:open label:research -label:workflow:research" --json number --jq '.[].number' | \
  xargs -I {} sh -c 'echo "  Adding workflow:research to issue #{}"; gh issue edit {} --add-label "workflow:research"'

# Label implementation issues (check /product/backlog references or implementation label)
echo "2. Migrating implementation issues..."
gh issue list --search "is:open label:implementation -label:workflow:implementation" --json number --jq '.[].number' | \
  xargs -I {} sh -c 'echo "  Adding workflow:implementation to issue #{}"; gh issue edit {} --add-label "workflow:implementation"'

# Label tech-debt issues
echo "3. Migrating tech-debt issues..."
gh issue list --search "is:open label:tech-debt -label:workflow:tech-debt" --json number --jq '.[].number' | \
  xargs -I {} sh -c 'echo "  Adding workflow:tech-debt to issue #{}"; gh issue edit {} --add-label "workflow:tech-debt"'

# Label product/backlog issues
echo "4. Migrating product backlog issues..."
gh issue list --search "is:open label:product -label:workflow:product-backlog" --json number --jq '.[].number' | \
  xargs -I {} sh -c 'echo "  Adding workflow:product-backlog to issue #{}"; gh issue edit {} --add-label "workflow:product-backlog"'

# Label process-modeling issues  
echo "5. Migrating process-modeling issues..."
gh issue list --search "is:open label:process-modeling -label:workflow:process-modeling" --json number --jq '.[].number' | \
  xargs -I {} sh -c 'echo "  Adding workflow:process-modeling to issue #{}"; gh issue edit {} --add-label "workflow:process-modeling"'

# All remaining open issues without workflow label → triage
echo "6. Migrating unlabeled issues to triage..."
gh issue list --search "is:open -label:workflow:triage -label:workflow:research -label:workflow:implementation -label:workflow:tech-debt -label:workflow:product-backlog -label:workflow:process-modeling" --json number --jq '.[].number' | \
  xargs -I {} sh -c 'echo "  Adding workflow:triage to issue #{}"; gh issue edit {} --add-label "workflow:triage"'

echo ""
echo "✅ Migration complete!"
echo ""
echo "Verify with:"
echo "  gh issue list --label \"workflow:triage\""
echo "  gh issue list --label \"workflow:research\""
echo "  # etc."
