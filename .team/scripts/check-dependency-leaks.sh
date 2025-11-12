#!/usr/bin/env bash
# check-dependency-leaks.sh - Detect content duplication across graph
# Version: 1.0
# Created: 2025-11-12
#
# This script detects "dependency leaks" where content from dependencies
# is duplicated in dependent files instead of being referenced.

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
MIN_DUPLICATE_LINES=3  # Minimum consecutive lines to consider duplication

echo -e "${BLUE}=== Dependency Leak Detection ===${NC}"
echo "Checking for content duplication across dependency graph..."
echo ""

if [[ ! -f "$REPO_ROOT/$GRAPH_FILE" ]]; then
    echo -e "${YELLOW}⚠${NC} Graph file not found: $GRAPH_FILE"
    echo "Skipping dependency leak detection."
    exit 0
fi

# Parse graph to get edges
declare -a edges
declare -A node_paths

# Parse nodes
while IFS= read -r line; do
    if [[ "$line" =~ ^[[:space:]]*-[[:space:]]*id:[[:space:]]*(.+)$ ]]; then
        current_id="${BASH_REMATCH[1]}"
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
        fi
    fi
done < "$REPO_ROOT/$GRAPH_FILE"

echo "Loaded graph:"
echo "  Nodes: ${#node_paths[@]}"
echo "  Edges: ${#edges[@]}"
echo ""

# Function to extract significant content chunks
extract_chunks() {
    local file="$1"
    
    # Extract lines, remove markdown formatting, normalize whitespace
    grep -vE '^(#|```|---|$)' "$file" 2>/dev/null | \
        sed 's/^[[:space:]]*//;s/[[:space:]]*$//' | \
        grep -vE '^$' || true
}

# Function to find duplicate chunks between two files
find_duplicates() {
    local source_file="$1"
    local target_file="$2"
    local min_lines="$3"
    
    # Create temporary files with normalized content
    local source_tmp=$(mktemp)
    local target_tmp=$(mktemp)
    
    extract_chunks "$source_file" > "$source_tmp"
    extract_chunks "$target_file" > "$target_tmp"
    
    # Use diff to find common sections
    # Look for consecutive matching lines
    local common_lines=$(comm -12 <(sort "$source_tmp") <(sort "$target_tmp") | wc -l)
    
    rm -f "$source_tmp" "$target_tmp"
    
    # Return count of common lines
    echo "$common_lines"
}

# Check each dependency relationship
echo "Checking for content duplication..."
duplicate_count=0
checked_pairs=0

for edge in "${edges[@]}"; do
    IFS='|' read -r from_id to_id <<< "$edge"
    
    source_path="${node_paths[$from_id]:-}"
    target_path="${node_paths[$to_id]:-}"
    
    # Skip if either path not found
    if [[ -z "$source_path" ]] || [[ -z "$target_path" ]]; then
        continue
    fi
    
    source_file="$REPO_ROOT/$source_path"
    target_file="$REPO_ROOT/$target_path"
    
    # Skip if either file doesn't exist
    if [[ ! -f "$source_file" ]] || [[ ! -f "$target_file" ]]; then
        continue
    fi
    
    ((checked_pairs++))
    
    # Check for duplicates
    common_lines=$(find_duplicates "$source_file" "$target_file" "$MIN_DUPLICATE_LINES")
    
    # Flag if significant duplication found
    if [[ $common_lines -gt $MIN_DUPLICATE_LINES ]]; then
        echo -e "${YELLOW}⚠${NC} Potential content duplication:"
        echo "  Source: $source_path"
        echo "  Target: $target_path"
        echo "  Common lines: $common_lines"
        echo "  → Consider: Reference source instead of duplicating content"
        echo ""
        ((duplicate_count++))
    fi
done

echo ""
echo -e "${BLUE}=== Scan Summary ===${NC}"
echo "Dependency pairs checked: $checked_pairs"

if [[ $duplicate_count -eq 0 ]]; then
    echo -e "${GREEN}✓ No significant content duplication detected${NC}"
    echo ""
    echo "Files properly reference dependencies instead of duplicating content."
    exit 0
else
    echo -e "${YELLOW}⚠ Found $duplicate_count potential duplicate(s)${NC}"
    echo ""
    echo "Dependency leaks indicate content from dependencies being duplicated"
    echo "instead of referenced. This violates the DRY (Don't Repeat Yourself)"
    echo "principle and can lead to maintenance issues."
    echo ""
    echo "To fix:"
    echo "  1. Review flagged file pairs"
    echo "  2. Replace duplicated content with markdown references"
    echo "  3. Ensure single source of truth for each concept"
    echo ""
    echo "Note: Some duplication may be intentional (e.g., examples, templates)."
    echo "      Review each case to determine if it's a genuine leak."
    exit 0  # Warning only, not a hard failure
fi
