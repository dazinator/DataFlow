#!/usr/bin/env bash
# get-dependencies.sh - Show dependencies for a specific file
# Version: 1.0
# Created: 2025-11-12
#
# This script shows what a file depends on and what depends on it

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

# Configuration
GRAPH_FILE=".team/model-graph.yaml"
TARGET_FILE="${1:-}"

if [[ -z "$TARGET_FILE" ]]; then
    echo "Usage: $0 <file-path>"
    echo ""
    echo "Examples:"
    echo "  $0 .team/prompts/RESEARCH_WORKFLOW.md"
    echo "  $0 .team/kernel/README.md"
    exit 1
fi

if [[ ! -f "$REPO_ROOT/$GRAPH_FILE" ]]; then
    echo -e "${RED}✗${NC} Graph file not found: $GRAPH_FILE"
    exit 1
fi

# Normalize target path
TARGET_FILE="${TARGET_FILE#./}"
TARGET_FILE="${TARGET_FILE#$REPO_ROOT/}"

echo -e "${BLUE}=== Dependency Analysis ===${NC}"
echo "Target: $TARGET_FILE"
echo ""

# Parse graph
declare -A node_ids
declare -A node_paths
declare -a edges

# Parse nodes
while IFS= read -r line; do
    if [[ "$line" =~ ^[[:space:]]*-[[:space:]]*id:[[:space:]]*(.+)$ ]]; then
        current_id="${BASH_REMATCH[1]}"
    elif [[ "$line" =~ ^[[:space:]]*path:[[:space:]]*(.+)$ ]] && [[ -n "${current_id:-}" ]]; then
        path="${BASH_REMATCH[1]}"
        node_ids["$path"]="$current_id"
        node_paths["$current_id"]="$path"
    fi
done < "$REPO_ROOT/$GRAPH_FILE"

# Find node ID for target file
TARGET_NODE_ID="${node_ids[$TARGET_FILE]:-}"

if [[ -z "$TARGET_NODE_ID" ]]; then
    echo -e "${YELLOW}⚠${NC} File not found in graph: $TARGET_FILE"
    echo ""
    echo "Available files:"
    for path in "${!node_ids[@]}"; do
        echo "  - $path"
    done | sort
    exit 1
fi

echo -e "Node ID: ${GREEN}$TARGET_NODE_ID${NC}"
echo ""

# Parse edges
in_edges_section=false
declare -a incoming_edges
declare -a outgoing_edges

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
            current_edge_to="$to_node"
        elif [[ "$line" =~ ^[[:space:]]*type:[[:space:]]*(.+)$ ]] && [[ -n "${current_edge_from:-}" ]] && [[ -n "${current_edge_to:-}" ]]; then
            edge_type="${BASH_REMATCH[1]}"
        elif [[ "$line" =~ ^[[:space:]]*reason:[[:space:]]*\"(.+)\"$ ]] && [[ -n "${current_edge_from:-}" ]] && [[ -n "${current_edge_to:-}" ]]; then
            reason="${BASH_REMATCH[1]}"
            
            # Check if this edge involves our target
            if [[ "$current_edge_from" == "$TARGET_NODE_ID" ]]; then
                outgoing_edges+=("$current_edge_to|$edge_type|$reason")
            fi
            
            if [[ "$current_edge_to" == "$TARGET_NODE_ID" ]]; then
                incoming_edges+=("$current_edge_from|$edge_type|$reason")
            fi
        fi
    fi
done < "$REPO_ROOT/$GRAPH_FILE"

# Display dependencies (what this file depends on)
echo -e "${BLUE}Dependencies${NC} (what $TARGET_FILE depends on):"
if [[ ${#outgoing_edges[@]} -eq 0 ]]; then
    echo "  (none)"
else
    for edge in "${outgoing_edges[@]}"; do
        IFS='|' read -r to_node edge_type reason <<< "$edge"
        to_path="${node_paths[$to_node]}"
        
        # Format edge type
        case "$edge_type" in
            required)
                type_indicator="${RED}[REQUIRED]${NC}"
                ;;
            optional)
                type_indicator="${YELLOW}[OPTIONAL]${NC}"
                ;;
            *)
                type_indicator="[${edge_type}]"
                ;;
        esac
        
        echo -e "  → $to_path $type_indicator"
        echo "      Reason: $reason"
    done
fi
echo ""

# Display dependents (what depends on this file)
echo -e "${BLUE}Dependents${NC} (what depends on $TARGET_FILE):"
if [[ ${#incoming_edges[@]} -eq 0 ]]; then
    echo "  (none)"
else
    for edge in "${incoming_edges[@]}"; do
        IFS='|' read -r from_node edge_type reason <<< "$edge"
        from_path="${node_paths[$from_node]}"
        
        # Format edge type
        case "$edge_type" in
            required)
                type_indicator="${RED}[REQUIRED]${NC}"
                ;;
            optional)
                type_indicator="${YELLOW}[OPTIONAL]${NC}"
                ;;
            *)
                type_indicator="[${edge_type}]"
                ;;
        esac
        
        echo -e "  ← $from_path $type_indicator"
        echo "      Reason: $reason"
    done
fi
echo ""

# Impact analysis
echo -e "${BLUE}Impact Analysis:${NC}"
if [[ ${#incoming_edges[@]} -eq 0 ]]; then
    echo -e "  ${GREEN}✓${NC} Low impact: No files depend on this file"
else
    echo -e "  ${YELLOW}⚠${NC} Changes to this file will affect ${#incoming_edges[@]} dependent file(s)"
    echo "      Review and test all dependents when making changes"
fi
echo ""

# Recommendations
if [[ ${#outgoing_edges[@]} -gt 0 ]]; then
    echo -e "${BLUE}Recommendations:${NC}"
    echo "  - When modifying this file, ensure dependencies remain valid"
    echo "  - Check that referenced files still exist and are up-to-date"
    
    # Check for required dependencies
    required_count=0
    for edge in "${outgoing_edges[@]}"; do
        IFS='|' read -r to_node edge_type reason <<< "$edge"
        if [[ "$edge_type" == "required" ]]; then
            ((required_count++))
        fi
    done
    
    if [[ $required_count -gt 0 ]]; then
        echo "  - This file has $required_count required dependencies - changes may break functionality"
    fi
fi
