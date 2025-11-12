#!/usr/bin/env bash
# check-graph-drift.sh - Detect drift between graph and actual documents
# Version: 1.0
# Created: 2025-11-12
#
# This script detects "graph drift" where the model-graph.yaml references
# files/paths that don't match the actual structure of duties, procedures,
# and kernel files in the repository.

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Graph file
GRAPH_FILE="$REPO_ROOT/.team/model-graph.yaml"

echo -e "${BLUE}=== Graph Drift Detection ===${NC}"
echo "Checking for drift between graph and actual documents..."
echo ""

# Check if graph file exists
if [[ ! -f "$GRAPH_FILE" ]]; then
    echo -e "${RED}✗${NC} Graph file not found: $GRAPH_FILE"
    exit 1
fi

errors=0
warnings=0

# Parse nodes from graph (simple YAML parsing)
echo "Parsing graph nodes..."

# Extract all nodes with their paths
nodes_data=$(awk '
/^  - id:/ { 
    id = $3
}
/^    path:/ && id != "" {
    path = $0
    sub(/^[[:space:]]*path:[[:space:]]*/, "", path)
    # Remove any leading/trailing whitespace
    gsub(/^[[:space:]]+|[[:space:]]+$/, "", path)
    print id "|" path
    id = ""
}
' "$GRAPH_FILE")

total_nodes=$(echo "$nodes_data" | wc -l)
echo "Found $total_nodes nodes in graph"
echo ""

echo "[1/3] Checking node file existence..."

missing_files=()
while IFS= read -r line; do
    # Skip empty lines
    [[ -z "$line" ]] && continue
    
    node_id=$(echo "$line" | cut -d'|' -f1)
    node_path=$(echo "$line" | cut -d'|' -f2-)
    
    full_path="$REPO_ROOT/$node_path"
    if [[ ! -f "$full_path" ]]; then
        echo -e "  ${RED}✗${NC} Node '$node_id' references missing file: $node_path"
        missing_files+=("$node_id: $node_path")
        ((errors++))
    fi
done <<< "$nodes_data"

if [[ ${#missing_files[@]} -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} All node files exist"
else
    echo -e "  ${RED}✗${NC} ${#missing_files[@]} missing file(s)"
fi
echo ""

echo "[2/3] Checking for undocumented duty files..."

# Find all duty files
duty_files=$(find "$REPO_ROOT/.team/duties" -name "*_DUTY.md" -not -path "*/tests/*" -type f 2>/dev/null || true)

undocumented_duties=()
while IFS= read -r duty_file; do
    [[ -z "$duty_file" ]] && continue
    
    # Convert to relative path
    rel_path="${duty_file#$REPO_ROOT/}"
    
    # Check if this path exists in the graph
    if ! grep -q "path: $rel_path" "$GRAPH_FILE"; then
        echo -e "  ${YELLOW}⚠${NC} Duty file not in graph: $rel_path"
        undocumented_duties+=("$rel_path")
        ((warnings++))
    fi
done <<< "$duty_files"

if [[ ${#undocumented_duties[@]} -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} All duty files are documented in graph"
else
    echo -e "  ${YELLOW}⚠${NC} ${#undocumented_duties[@]} undocumented duty file(s)"
fi
echo ""

echo "[3/3] Checking for undocumented procedure files..."

# Find all procedure files (excluding README and tests)
procedure_files=$(find "$REPO_ROOT/.team/procedures" -name "*.md" -not -name "README.md" -not -path "*/tests/*" -type f 2>/dev/null || true)

undocumented_procedures=()
while IFS= read -r proc_file; do
    [[ -z "$proc_file" ]] && continue
    
    # Convert to relative path
    rel_path="${proc_file#$REPO_ROOT/}"
    
    # Check if this path exists in the graph
    if ! grep -q "path: $rel_path" "$GRAPH_FILE"; then
        echo -e "  ${YELLOW}⚠${NC} Procedure file not in graph: $rel_path"
        undocumented_procedures+=("$rel_path")
        ((warnings++))
    fi
done <<< "$procedure_files"

if [[ ${#undocumented_procedures[@]} -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} All procedure files are documented in graph"
else
    echo -e "  ${YELLOW}⚠${NC} ${#undocumented_procedures[@]} undocumented procedure file(s)"
fi
echo ""

# Summary
echo "=== Drift Detection Summary ==="
if [[ $errors -eq 0 && $warnings -eq 0 ]]; then
    echo -e "${GREEN}✓ No drift detected${NC}"
    exit 0
elif [[ $errors -eq 0 ]]; then
    echo -e "${YELLOW}⚠ $warnings warning(s) - graph may be incomplete${NC}"
    echo ""
    echo "Warnings indicate files that exist but aren't documented in the graph."
    echo "Consider adding these files to .team/model-graph.yaml if they should be tracked."
    exit 0
else
    echo -e "${RED}✗ $errors error(s), $warnings warning(s)${NC}"
    echo ""
    echo "Errors indicate the graph references files that don't exist."
    echo "Update .team/model-graph.yaml to fix these references."
    exit 1
fi
