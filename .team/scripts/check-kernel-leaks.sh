#!/usr/bin/env bash
# check-kernel-leaks.sh - Detect platform-specific terms outside kernel layer
# Version: 2.0
# Created: 2025-11-12
# Updated: 2025-11-12 - Now reads patterns from domains.yaml
#
# This script detects "kernel leaks" where platform-specific operations
# (like GitHub MCP tool calls) appear in procedures or duties instead of
# being abstracted through semantic operations.

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

# Domains file
DOMAINS_FILE="$REPO_ROOT/.team/kernel/domains.yaml"

# Directories to check (exclude kernel layer)
# Note: .team/prompts was archived in Phase 5, replaced by .team/duties
CHECK_DIRS=(
    ".team/procedures"
    ".team/duties"
    ".github/copilot-instructions.md"
)

echo -e "${BLUE}=== Kernel Leak Detection ===${NC}"
echo "Checking for platform-specific operations outside kernel layer..."
echo ""

# Check if domains file exists
if [[ ! -f "$DOMAINS_FILE" ]]; then
    echo -e "${RED}✗${NC} Domains file not found: $DOMAINS_FILE"
    echo "Cannot perform leak detection without kernel domain registry."
    exit 1
fi

echo "Loading patterns from: $DOMAINS_FILE"
echo ""

# Parse patterns from domains.yaml
# Extract leak_patterns for each domain
declare -A domain_patterns

current_domain=""
in_leak_patterns=false

while IFS= read -r line; do
    # Check for domain name
    if [[ "$line" =~ ^[[:space:]]*-[[:space:]]*name:[[:space:]]*(.+)$ ]]; then
        current_domain="${BASH_REMATCH[1]}"
        in_leak_patterns=false
    fi
    
    # Check for leak_patterns section
    if [[ "$line" =~ ^[[:space:]]*leak_patterns:[[:space:]]*$ ]]; then
        in_leak_patterns=true
        continue
    fi
    
    # If we're in leak_patterns section and find a pattern
    if [[ "$in_leak_patterns" == true ]] && [[ "$line" =~ ^[[:space:]]*-[[:space:]]*(.+)$ ]]; then
        pattern="${BASH_REMATCH[1]}"
        # Add to domain patterns
        if [[ -n "$current_domain" ]]; then
            if [[ -z "${domain_patterns[$current_domain]:-}" ]]; then
                domain_patterns[$current_domain]="$pattern"
            else
                domain_patterns[$current_domain]="${domain_patterns[$current_domain]}|$pattern"
            fi
        fi
    fi
    
    # End leak_patterns section if we hit a non-indented line (except empty lines)
    if [[ "$in_leak_patterns" == true ]] && [[ "$line" =~ ^[^[:space:]-] ]] && [[ -n "$line" ]]; then
        in_leak_patterns=false
    fi
done < "$DOMAINS_FILE"

# Display loaded patterns
total_patterns=0
for domain in "${!domain_patterns[@]}"; do
    pattern_count=$(echo "${domain_patterns[$domain]}" | tr '|' '\n' | wc -l)
    echo "Loaded $pattern_count patterns for $domain kernel"
    ((total_patterns += pattern_count))
done
echo ""

if [[ $total_patterns -eq 0 ]]; then
    echo -e "${YELLOW}⚠${NC} No leak detection patterns found in domains.yaml"
    echo "Leak detection cannot be performed."
    exit 0
fi

# Function to check file for leaks
check_file_for_leaks() {
    local file="$1"
    local rel_path="${file#$REPO_ROOT/}"
    local found_leaks=false
    
    # Check patterns for each domain
    for domain in "${!domain_patterns[@]}"; do
        # Split patterns by pipe
        IFS='|' read -ra patterns <<< "${domain_patterns[$domain]}"
        
        for pattern in "${patterns[@]}"; do
            # Find matches, but exclude anti-pattern examples (lines with "# Wrong" or in anti-pattern code blocks)
            matches=$(grep -nE "$pattern" "$file" 2>/dev/null | grep -vE "(# Wrong|❌)" || true)
            
            if [[ -n "$matches" ]]; then
                if [[ "$found_leaks" == false ]]; then
                    echo -e "${RED}✗${NC} Kernel leak detected in: $rel_path"
                    found_leaks=true
                fi
                
                # Show first match context
                line_num=$(echo "$matches" | head -1 | cut -d: -f1)
                echo -e "  ${YELLOW}Line $line_num:${NC} $domain pattern '$pattern' found"
            fi
        done
    done
    
    if [[ "$found_leaks" == true ]]; then
        return 1
    fi
    
    return 0
}

# Scan files
total_files=0
files_with_leaks=0

# Temporarily disable set -e for file processing loop to avoid early exit
set +e

for check_item in "${CHECK_DIRS[@]}"; do
    check_path="$REPO_ROOT/$check_item"
    
    if [[ -f "$check_path" ]]; then
        # Single file
        ((total_files++))
        if ! check_file_for_leaks "$check_path"; then
            ((files_with_leaks++))
        fi
    elif [[ -d "$check_path" ]]; then
        # Directory (exclude tests subdirectories)
        # Store file list in a temp variable
        file_list=$(find "$check_path" -name "*.md" -type f -not -path "*/tests/*" 2>/dev/null || true)
        if [[ -n "$file_list" ]]; then
            while IFS= read -r file; do
                if [[ -n "$file" ]]; then
                    ((total_files++))
                    if ! check_file_for_leaks "$file"; then
                        ((files_with_leaks++))
                    fi
                fi
            done <<< "$file_list"
        fi
    fi
done

# Re-enable set -e
set -e

echo ""
echo -e "${BLUE}=== Scan Summary ===${NC}"
echo "Files scanned: $total_files"

if [[ $files_with_leaks -eq 0 ]]; then
    echo -e "${GREEN}✓ No kernel leaks detected${NC}"
    echo ""
    echo "All files properly use semantic operations instead of platform-specific calls."
    exit 0
else
    echo -e "${RED}✗ Kernel leaks found in $files_with_leaks file(s)${NC}"
    echo ""
    echo "Kernel leaks indicate platform-specific operations (like GitHub MCP tool calls)"
    echo "appearing in procedures or duties. These should be abstracted through semantic"
    echo "operations defined in the kernel layer."
    echo ""
    echo "To fix:"
    echo "  1. Replace platform-specific calls with semantic operations"
    echo "  2. See .team/kernel/README.md for available semantic operations"
    echo "  3. Add new semantic operations if needed"
    echo ""
    echo "Patterns are defined in: .team/kernel/domains.yaml"
    exit 1
fi
