#!/bin/bash
# Query workflow queue
# Usage: ./query-workflow-queue.sh WORKFLOW_NAME
#
# Example: ./query-workflow-queue.sh research

WORKFLOW_NAME=$1

if [ -z "$WORKFLOW_NAME" ]; then
  echo "Usage: $0 WORKFLOW_NAME"
  echo "Example: $0 research"
  exit 1
fi

echo "=== Querying workflow:$WORKFLOW_NAME ==="

gh issue list \
  --label "workflow:$WORKFLOW_NAME" \
  --state open \
  --json number,title,url,createdAt \
  --jq '.[] | "Issue #\(.number): \(.title)\n  URL: \(.url)\n  Created: \(.createdAt)\n"'
