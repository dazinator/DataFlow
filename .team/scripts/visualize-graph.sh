#!/usr/bin/env bash
# visualize-graph.sh - Generate Mermaid diagram from dependency graph
# Version: 1.0
# Created: 2025-11-12
#
# This script generates a Mermaid flowchart diagram from model-graph.yaml

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
GRAPH_FILE="${1:-.team/model-graph.yaml}"
OUTPUT_FILE="${2:-graph-visualization.md}"

if [[ ! -f "$REPO_ROOT/$GRAPH_FILE" ]]; then
    echo -e "${RED}✗${NC} Graph file not found: $GRAPH_FILE"
    exit 1
fi

echo -e "${BLUE}=== Graph Visualization Tool ===${NC}"
echo "Input: $GRAPH_FILE"
echo "Output: $OUTPUT_FILE"
echo ""

# Parse graph
declare -A nodes
declare -A node_types
declare -a edges

echo "Parsing graph..."

# Parse nodes
while IFS= read -r line; do
    if [[ "$line" =~ ^[[:space:]]*-[[:space:]]*id:[[:space:]]*(.+)$ ]]; then
        current_id="${BASH_REMATCH[1]}"
        nodes["$current_id"]=1
    elif [[ "$line" =~ ^[[:space:]]*type:[[:space:]]*(.+)$ ]] && [[ -n "${current_id:-}" ]]; then
        node_type="${BASH_REMATCH[1]}"
        node_types["$current_id"]="$node_type"
    elif [[ "$line" =~ ^[[:space:]]*description:[[:space:]]*\"(.+)\"$ ]] && [[ -n "${current_id:-}" ]]; then
        description="${BASH_REMATCH[1]}"
        # Store first 50 chars of description
        nodes["$current_id"]="${description:0:50}"
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
            current_edge_to="$to_node"
        elif [[ "$line" =~ ^[[:space:]]*type:[[:space:]]*(.+)$ ]] && [[ -n "${current_edge_from:-}" ]] && [[ -n "${current_edge_to:-}" ]]; then
            edge_type="${BASH_REMATCH[1]}"
            edges+=("$current_edge_from|$current_edge_to|$edge_type")
        fi
    fi
done < "$REPO_ROOT/$GRAPH_FILE"

echo "  Nodes: ${#nodes[@]}"
echo "  Edges: ${#edges[@]}"
echo ""

# Generate Mermaid diagram
echo "Generating Mermaid diagram..."

{
    cat <<'EOF'
# Dependency Graph Visualization

This diagram shows the dependency relationships between prompt system components.

```mermaid
flowchart TB
EOF

    # Define nodes with styling
    for node_id in "${!nodes[@]}"; do
        label="${nodes[$node_id]}"
        node_type="${node_types[$node_id]:-unknown}"
        
        # Format label for Mermaid (escape special chars, limit length)
        label="${label//\"/\\\"}"
        label="${label:0:40}"
        
        # Add node with label
        echo "    $node_id[\"$label\"]"
    done
    
    echo ""
    
    # Define edges
    for edge in "${edges[@]}"; do
        IFS='|' read -r from to edge_type <<< "$edge"
        
        # Choose arrow style based on edge type
        case "$edge_type" in
            required)
                arrow="-->"
                ;;
            optional)
                arrow="-.->
"
                ;;
            cross-reference)
                arrow="-.->
"
                ;;
            *)
                arrow="-->"
                ;;
        esac
        
        echo "    $from $arrow $to"
    done
    
    echo ""
    
    # Apply styling based on node types
    echo "    %% Node styling by type"
    for node_id in "${!nodes[@]}"; do
        node_type="${node_types[$node_id]:-unknown}"
        
        case "$node_type" in
            orchestration)
                echo "    style $node_id fill:#e1f5ff,stroke:#333,stroke-width:3px"
                ;;
            kernel)
                echo "    style $node_id fill:#ffe1e1,stroke:#333,stroke-width:2px"
                ;;
            procedure)
                echo "    style $node_id fill:#f0f0f0,stroke:#333"
                ;;
            duty|workflow)
                echo "    style $node_id fill:#fff9e1,stroke:#333"
                ;;
            design-doc)
                echo "    style $node_id fill:#e1ffe1,stroke:#333"
                ;;
            system-doc|supporting-doc)
                echo "    style $node_id fill:#f5f5f5,stroke:#333"
                ;;
        esac
    done
    
    cat <<'EOF'
```

## Legend

**Node Types:**
- **Orchestration** (Light Blue, Bold Border): Entry point (copilot-instructions.md)
- **Kernel** (Light Red, Thick Border): Platform-specific drivers
- **Procedure** (Gray): Global procedures (platform-agnostic)
- **Duty/Workflow** (Light Yellow): Duty-specific procedures
- **Design Doc** (Light Green): Design documentation
- **System/Supporting Doc** (Light Gray): Supporting documentation

**Edge Types:**
- **Solid Arrow** (→): Required dependency
- **Dotted Arrow** (-.→): Optional reference or cross-reference

---

**Generated**: $(date -u +"%Y-%m-%d %H:%M:%S UTC")  
**Source**: `$GRAPH_FILE`  
**Tool**: `visualize-graph.sh`
EOF

} > "$OUTPUT_FILE"

echo -e "${GREEN}✓${NC} Visualization generated"
echo ""
echo "Output: $OUTPUT_FILE"
echo ""
echo "To view:"
echo "  1. Open $OUTPUT_FILE in any Markdown viewer"
echo "  2. The Mermaid diagram will render automatically"
echo "  3. Or copy the mermaid block to https://mermaid.live"
