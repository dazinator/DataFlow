#!/usr/bin/env bash
# infer-graph.sh - Extract dependency graph from markdown references
# Version: 1.0
# Created: 2025-11-12
#
# This script scans markdown files for references and infers the dependency graph.
# It looks for standardized reference patterns and builds a YAML graph structure.

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
SEARCH_DIRS=(
    ".github"
    ".team"
    "docs/design/prompt-engineering"
)

OUTPUT_FILE="${1:-.team/model-graph-inferred.yaml}"

echo -e "${BLUE}=== Graph Inference Tool ===${NC}"
echo "Scanning markdown files for references..."
echo ""

# Function to extract markdown links
extract_links() {
    local file="$1"
    local rel_path="${file#$REPO_ROOT/}"
    
    # Find markdown links: [text](path) or [text](path.md)
    # Also find <a> tags with href or markdown reference tags
    grep -oE '\[([^\]]+)\]\(([^)]+)\)' "$file" 2>/dev/null || true
}

# Function to resolve relative path
resolve_path() {
    local source_file="$1"
    local target_path="$2"
    local source_dir="$(dirname "$source_file")"
    
    # Remove fragment identifiers
    target_path="${target_path%%#*}"
    
    # If absolute path from repo root
    if [[ "$target_path" == /* ]]; then
        echo "${REPO_ROOT}${target_path}"
    else
        # Relative path
        echo "$(cd "$source_dir" && cd "$(dirname "$target_path")" && pwd)/$(basename "$target_path")"
    fi
}

# Collect all markdown files
declare -a MD_FILES=()
for dir in "${SEARCH_DIRS[@]}"; do
    search_path="$REPO_ROOT/$dir"
    if [[ -d "$search_path" ]]; then
        while IFS= read -r -d '' file; do
            MD_FILES+=("$file")
        done < <(find "$search_path" -name "*.md" -type f -print0)
    fi
done

echo "Found ${#MD_FILES[@]} markdown files"
echo ""

# Build graph structure
declare -A nodes
declare -a edges

# Extract nodes (files)
for file in "${MD_FILES[@]}"; do
    rel_path="${file#$REPO_ROOT/}"
    # Remove leading ./
    rel_path="${rel_path#./}"
    
    # Determine node type
    node_type="unknown"
    if [[ "$rel_path" == ".github/copilot-instructions.md" ]]; then
        node_type="orchestration"
    elif [[ "$rel_path" == .team/prompts/*_WORKFLOW.md ]]; then
        node_type="workflow"
    elif [[ "$rel_path" == .team/duties/*_DUTY.md ]]; then
        node_type="duty"
    elif [[ "$rel_path" == .team/procedures/*.md ]]; then
        node_type="procedure"
    elif [[ "$rel_path" == .team/kernel/*.md ]] || [[ "$rel_path" == .team/kernel/*/*.md ]]; then
        node_type="kernel"
    elif [[ "$rel_path" == docs/design/* ]]; then
        node_type="design-doc"
    elif [[ "$rel_path" == .github/docs/*.md ]]; then
        node_type="system-doc"
    elif [[ "$rel_path" == .team/*.md ]]; then
        node_type="supporting-doc"
    fi
    
    # Store node
    nodes["$rel_path"]="$node_type"
done

# Extract edges (references)
for source_file in "${MD_FILES[@]}"; do
    source_rel="${source_file#$REPO_ROOT/}"
    source_rel="${source_rel#./}"
    
    # Extract all markdown links from file
    while IFS= read -r link; do
        # Parse link: [text](path)
        if [[ "$link" =~ \[([^\]]+)\]\(([^)]+)\) ]]; then
            link_text="${BASH_REMATCH[1]}"
            link_path="${BASH_REMATCH[2]}"
            
            # Skip external URLs
            if [[ "$link_path" =~ ^https?:// ]]; then
                continue
            fi
            
            # Resolve to absolute path
            target_abs=$(resolve_path "$source_file" "$link_path" 2>/dev/null || echo "")
            
            if [[ -n "$target_abs" ]] && [[ -f "$target_abs" ]]; then
                target_rel="${target_abs#$REPO_ROOT/}"
                target_rel="${target_rel#./}"
                
                # Only include if target is in our node set
                if [[ -n "${nodes[$target_rel]:-}" ]]; then
                    # Determine edge type from context
                    edge_type="cross-reference"
                    
                    # Check for "Required Context" or "See" patterns
                    if echo "$link_text" | grep -qi "required"; then
                        edge_type="required"
                    elif echo "$link_text" | grep -qi "see\|reference\|refer"; then
                        edge_type="optional"
                    fi
                    
                    edges+=("$source_rel|$target_rel|$edge_type|$link_text")
                fi
            fi
        fi
    done < <(extract_links "$source_file")
done

# Remove duplicates from edges
IFS=$'\n' edges=($(printf "%s\n" "${edges[@]}" | sort -u))

# Generate YAML output
{
    cat <<EOF
# Inferred Dependency Graph
# Generated: $(date -u +"%Y-%m-%d %H:%M:%S UTC")
# Tool: infer-graph.sh
# Source: Markdown reference analysis
#
# This graph was automatically inferred from markdown links.
# Review and validate before using as authoritative source.

nodes:
EOF

    # Output nodes
    for node_path in "${!nodes[@]}"; do
        node_type="${nodes[$node_path]}"
        node_id=$(echo "$node_path" | sed 's/[^a-zA-Z0-9_-]/-/g')
        
        # Get file description (first heading or filename)
        description=$(grep -m1 "^#" "$REPO_ROOT/$node_path" 2>/dev/null | sed 's/^#* *//' || basename "$node_path" .md)
        
        cat <<EOF
  - id: $node_id
    type: $node_type
    path: $node_path
    description: "$description"
EOF
    done

    cat <<EOF

edges:
EOF

    # Output edges
    for edge in "${edges[@]}"; do
        IFS='|' read -r from_path to_path edge_type reason <<< "$edge"
        from_id=$(echo "$from_path" | sed 's/[^a-zA-Z0-9_-]/-/g')
        to_id=$(echo "$to_path" | sed 's/[^a-zA-Z0-9_-]/-/g')
        
        cat <<EOF
  - from: $from_id
    to: $to_id
    type: $edge_type
    reason: "$reason"
EOF
    done

} > "$OUTPUT_FILE"

echo -e "${GREEN}✓${NC} Graph inference complete"
echo ""
echo "Output: $OUTPUT_FILE"
echo ""
echo "Summary:"
echo "  Nodes: ${#nodes[@]}"
echo "  Edges: ${#edges[@]}"
echo ""
echo -e "${YELLOW}Note:${NC} Review generated graph for accuracy."
echo "      Compare with manual graph at .team/model-graph.yaml"
