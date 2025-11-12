#!/usr/bin/env bash
# validate-graph.sh - Validate dependency graph for cycles, orphans, and integrity
# Version: 1.0
# Created: 2025-11-12
#
# This script validates the model-graph.yaml file to ensure:
# - No circular dependencies
# - No orphaned nodes
# - All referenced files exist
# - Graph integrity (valid node types, edge types, etc.)

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

# Graph file to validate
GRAPH_FILE="${1:-.team/model-graph.yaml}"

if [[ ! -f "$REPO_ROOT/$GRAPH_FILE" ]]; then
    echo -e "${RED}✗${NC} Graph file not found: $GRAPH_FILE"
    exit 1
fi

echo -e "${BLUE}=== Graph Validation Tool ===${NC}"
echo "Validating: $GRAPH_FILE"
echo ""

# Validation results
errors=0
warnings=0

# Extract nodes and edges from YAML (simple parsing)
declare -A nodes
declare -a edges
declare -A node_paths
declare -A incoming_edges
declare -A outgoing_edges

echo "Parsing graph..."

# Parse nodes
while IFS= read -r line; do
    if [[ "$line" =~ ^[[:space:]]*-[[:space:]]*id:[[:space:]]*(.+)$ ]]; then
        current_id="${BASH_REMATCH[1]}"
        nodes["$current_id"]=1
    elif [[ "$line" =~ ^[[:space:]]*path:[[:space:]]*(.+)$ ]] && [[ -n "${current_id:-}" ]]; then
        path="${BASH_REMATCH[1]}"
        node_paths["$current_id"]="$path"
    fi
done < "$REPO_ROOT/$GRAPH_FILE"

# Parse edges
in_edges_section=false
while IFS= read -r line; do
    if [[ "$line" =~ ^edges: ]]; then
        in_edges_section=true
        continue
    fi
    
    if [[ "$in_edges_section" == true ]]; then
        if [[ "$line" =~ ^[[:space:]]*-[[:space:]]*from:[[:space:]]*(.+)$ ]]; then
            from_node="${BASH_REMATCH[1]}"
            current_edge_from="$from_node"
        elif [[ "$line" =~ ^[[:space:]]*to:[[:space:]]*(.+)$ ]] && [[ -n "${current_edge_from:-}" ]]; then
            to_node="${BASH_REMATCH[1]}"
            edges+=("$current_edge_from|$to_node")
            
            # Track incoming/outgoing
            incoming_edges["$to_node"]="${incoming_edges[$to_node]:-} $current_edge_from"
            outgoing_edges["$current_edge_from"]="${outgoing_edges[$current_edge_from]:-} $to_node"
        fi
    fi
done < "$REPO_ROOT/$GRAPH_FILE"

echo "  Nodes: ${#nodes[@]}"
echo "  Edges: ${#edges[@]}"
echo ""

# Validation 1: Check all node files exist
echo -e "${BLUE}[1/5]${NC} Checking node file existence..."
missing_files=0
for node_id in "${!node_paths[@]}"; do
    path="${node_paths[$node_id]}"
    full_path="$REPO_ROOT/$path"
    
    if [[ ! -f "$full_path" ]]; then
        echo -e "  ${RED}✗${NC} Node '$node_id' references missing file: $path"
        ((missing_files++))
        ((errors++))
    fi
done

if [[ $missing_files -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} All node files exist"
else
    echo -e "  ${RED}✗${NC} Found $missing_files missing files"
fi
echo ""

# Validation 2: Check for orphaned nodes
echo -e "${BLUE}[2/5]${NC} Checking for orphaned nodes..."
orphaned=0
for node_id in "${!nodes[@]}"; do
    # Skip orchestration node (entry point, expected to have no incoming edges)
    if [[ "$node_id" == "orchestration" ]]; then
        continue
    fi
    
    # Check if node has any incoming or outgoing edges
    has_incoming="${incoming_edges[$node_id]:-}"
    has_outgoing="${outgoing_edges[$node_id]:-}"
    
    if [[ -z "$has_incoming" ]] && [[ -z "$has_outgoing" ]]; then
        echo -e "  ${YELLOW}⚠${NC} Orphaned node (no connections): $node_id"
        ((orphaned++))
        ((warnings++))
    fi
done

if [[ $orphaned -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} No orphaned nodes found"
else
    echo -e "  ${YELLOW}⚠${NC} Found $orphaned orphaned nodes"
fi
echo ""

# Validation 3: Check edge references
echo -e "${BLUE}[3/5]${NC} Checking edge node references..."
invalid_refs=0
for edge in "${edges[@]}"; do
    IFS='|' read -r from to <<< "$edge"
    
    if [[ -z "${nodes[$from]:-}" ]]; then
        echo -e "  ${RED}✗${NC} Edge references unknown 'from' node: $from"
        ((invalid_refs++))
        ((errors++))
    fi
    
    if [[ -z "${nodes[$to]:-}" ]]; then
        echo -e "  ${RED}✗${NC} Edge references unknown 'to' node: $to"
        ((invalid_refs++))
        ((errors++))
    fi
done

if [[ $invalid_refs -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} All edge references valid"
else
    echo -e "  ${RED}✗${NC} Found $invalid_refs invalid edge references"
fi
echo ""

# Validation 4: Check for circular dependencies
echo -e "${BLUE}[4/5]${NC} Checking for circular dependencies..."

# Simplified cycle detection using DFS with global arrays
declare -A visited
declare -A rec_stack

check_cycle() {
    local node="$1"
    
    visited["$node"]=1
    rec_stack["$node"]=1
    
    # Get outgoing edges for this node
    local neighbors="${outgoing_edges[$node]:-}"
    for neighbor in $neighbors; do
        if [[ -z "${visited[$neighbor]:-}" ]]; then
            if check_cycle "$neighbor"; then
                return 0  # Cycle found
            fi
        elif [[ "${rec_stack[$neighbor]:-}" == "1" ]]; then
            echo -e "  ${RED}✗${NC} Cycle detected: $node → $neighbor"
            return 0  # Cycle found
        fi
    done
    
    rec_stack["$node"]=0
    return 1  # No cycle
}

cycles_found=0

for node in "${!nodes[@]}"; do
    if [[ -z "${visited[$node]:-}" ]]; then
        if check_cycle "$node"; then
            ((cycles_found++))
            ((errors++))
        fi
    fi
done

if [[ $cycles_found -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} No circular dependencies found"
else
    echo -e "  ${RED}✗${NC} Found circular dependencies"
fi
echo ""

# Validation 5: Graph integrity checks
echo -e "${BLUE}[5/5]${NC} Checking graph integrity..."
integrity_issues=0

# Check for reasonable graph structure
if [[ ${#nodes[@]} -eq 0 ]]; then
    echo -e "  ${RED}✗${NC} Graph has no nodes"
    ((integrity_issues++))
    ((errors++))
fi

if [[ ${#edges[@]} -eq 0 ]]; then
    echo -e "  ${YELLOW}⚠${NC} Graph has no edges"
    ((integrity_issues++))
    ((warnings++))
fi

# Check orchestration node exists
if [[ -z "${nodes[orchestration]:-}" ]]; then
    echo -e "  ${YELLOW}⚠${NC} No orchestration node found"
    ((warnings++))
fi

if [[ $integrity_issues -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} Graph structure is valid"
fi
echo ""

# Summary
echo -e "${BLUE}=== Validation Summary ===${NC}"
if [[ $errors -eq 0 ]] && [[ $warnings -eq 0 ]]; then
    echo -e "${GREEN}✓ Graph is valid${NC}"
    exit 0
elif [[ $errors -eq 0 ]]; then
    echo -e "${YELLOW}⚠ Graph is valid with warnings${NC}"
    echo "  Warnings: $warnings"
    exit 0
else
    echo -e "${RED}✗ Graph validation failed${NC}"
    echo "  Errors: $errors"
    echo "  Warnings: $warnings"
    exit 1
fi
