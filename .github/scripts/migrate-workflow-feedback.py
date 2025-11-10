#!/usr/bin/env python3
"""
Migration script for workflow feedback entries from .github/workflow-improvements.md to GitHub Issues.

This script must be run by a user with GitHub API access and the appropriate permissions.
It can be run via a GitHub Actions workflow or locally with the GitHub CLI authenticated.

Usage:
    python3 migrate-workflow-feedback.py [--dry-run] [--start-index N]
    
Options:
    --dry-run       Show what would be created without actually creating issues
    --start-index N Start migration from entry N (useful for resuming)
"""

import re
import json
import sys
import subprocess
import argparse
from typing import List, Dict, Optional

PARENT_ISSUE = 254  # [Workflow Feedback] Tracker issue number
REPO_OWNER = "uniun-technology"
REPO_NAME = "lib-dataflow"

def parse_feedback_file(filepath: str) -> List[Dict]:
    """Parse workflow-improvements.md into structured entries."""
    
    with open(filepath, 'r') as f:
        content = f.read()
    
    entries = []
    
    # Split by workflow sections
    sections = re.split(r'^## (.+) Workflow Improvements$', content, flags=re.MULTILINE)
    
    for i in range(1, len(sections), 2):
        if i + 1 >= len(sections):
            break
            
        workflow_name = sections[i].strip()
        section_content = sections[i + 1]
        
        # Find all entries in this section
        entry_pattern = r'- \*\*Date\*\*: (.+?)(?=\n- \*\*Date\*\*:|(?:\n---\n)|(?:\n## )|$)'
        entry_matches = re.finditer(entry_pattern, section_content, re.DOTALL)
        
        for match in entry_matches:
            entry_text = match.group(0)
            date_match = re.search(r'- \*\*Date\*\*: (.+?)$', entry_text, re.MULTILINE)
            issue_match = re.search(r'- \*\*Issue/PR\*\*: (.+?)$', entry_text, re.MULTILINE)
            
            # Extract sections
            worked_well_match = re.search(
                r'- \*\*What worked well\*\*:(.+?)(?=- \*\*What didn\'t work well\*\*:)',
                entry_text, re.DOTALL
            )
            
            didnt_work_match = re.search(
                r'- \*\*What didn\'t work well\*\*:(.+?)(?=- \*\*Suggested improvement\*\*:)',
                entry_text, re.DOTALL
            )
            
            improvement_match = re.search(
                r'- \*\*Suggested improvement\*\*:(.+?)(?=\n(?:---|\n- \*\*Date\*\*:|## |$))',
                entry_text, re.DOTALL
            )
            
            # Determine if all improvements are addressed
            is_closed = False
            if improvement_match:
                improvement_text = improvement_match.group(1)
                # Check for ✅ markers
                numbered_items = re.findall(r'^\s+\d+\.', improvement_text, re.MULTILINE)
                addressed_items = re.findall(r'^\s+\d+\.\s*✅', improvement_text, re.MULTILINE)
                inline_addressed = re.findall(r'✅\s*(?:ADDRESSED|IMPLEMENTED)', improvement_text, re.IGNORECASE)
                
                if numbered_items and len(numbered_items) == len(addressed_items):
                    is_closed = True
                elif inline_addressed and len(inline_addressed) >= 1:
                    if len(inline_addressed) >= len(numbered_items) * 0.5:
                        is_closed = True
            
            # Create title
            title = workflow_name
            if issue_match:
                issue_text = issue_match.group(1).strip()
                if '(' in issue_text and ')' in issue_text:
                    desc = issue_text.split('(')[0].strip()
                    if desc and desc != "#[number]":
                        title = desc
                    else:
                        paren_content = re.search(r'\(([^)]+)\)', issue_text)
                        if paren_content:
                            title = paren_content.group(1)
                else:
                    title = issue_text
            
            if date_match:
                date_str = date_match.group(1).strip()
                title = f"{title} ({date_str})"
            
            title = f"[Feedback] {title}"
            if len(title) > 100:
                title = title[:97] + "..."
            
            entry = {
                'workflow': workflow_name,
                'date': date_match.group(1).strip() if date_match else 'Unknown',
                'issue_pr': issue_match.group(1).strip() if issue_match else 'Unknown',
                'what_worked_well': worked_well_match.group(1).strip() if worked_well_match else '',
                'what_didnt_work': didnt_work_match.group(1).strip() if didnt_work_match else '',
                'suggested_improvement': improvement_match.group(1).strip() if improvement_match else '',
                'is_closed': is_closed,
                'title': title
            }
            
            entries.append(entry)
    
    return entries

def format_issue_body(entry: Dict) -> str:
    """Format entry as GitHub issue body."""
    
    return f"""## Workflow Feedback Entry

**Date**: {entry['date']}
**Issue/PR**: {entry['issue_pr']}
**Workflow**: {entry['workflow']} Workflow

**Migration Note**: Migrated from `.github/workflow-improvements.md`

### What Worked Well
{entry['what_worked_well']}

### What Didn't Work Well
{entry['what_didnt_work']}

### Suggested Improvement
{entry['suggested_improvement']}
"""

def create_issue_gh_cli(title: str, body: str, state: str, dry_run: bool = False) -> Optional[int]:
    """Create a GitHub issue using gh CLI."""
    
    if dry_run:
        print(f"  [DRY RUN] Would create issue: {title}")
        print(f"  [DRY RUN] State: {state}")
        return None
    
    # Create issue
    cmd = [
        'gh', 'issue', 'create',
        '--repo', f"{REPO_OWNER}/{REPO_NAME}",
        '--title', title,
        '--body', body,
        '--label', 'workflow:process-modeling'
    ]
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
        # Extract issue number from URL
        url = result.stdout.strip()
        issue_number = int(url.split('/')[-1])
        print(f"  ✓ Created issue #{issue_number}: {title}")
        
        # Close if needed
        if state == 'closed':
            close_cmd = ['gh', 'issue', 'close', str(issue_number), '--repo', f"{REPO_OWNER}/{REPO_NAME}"]
            subprocess.run(close_cmd, check=True)
            print(f"  ✓ Closed issue #{issue_number}")
        
        return issue_number
    except subprocess.CalledProcessError as e:
        print(f"  ✗ Error creating issue: {e.stderr}")
        return None

def link_sub_issue(parent: int, child: int, dry_run: bool = False) -> bool:
    """Link child issue to parent as sub-issue using GitHub API."""
    
    if dry_run:
        print(f"  [DRY RUN] Would link #{child} to parent #{parent}")
        return True
    
    # Note: This requires GraphQL API call
    # For now, document that manual linking is needed
    print(f"  ⚠ Manual step required: Link #{child} as sub-issue of #{parent}")
    print("     To link these issues as parent/child (sub-issue), use the following steps:")
    print(f"     1. Get the node IDs for both issues:")
    print(f"        gh api graphql -f query='query {{ repository(owner: \"{REPO_OWNER}\", name: \"{REPO_NAME}\") {{ parent: issue(number: {parent}) {{ id }} child: issue(number: {child}) {{ id }} }} }}'")
    print("     2. Use the node IDs in this mutation (replace PARENT_NODE_ID and CHILD_NODE_ID with values from step 1):")
    print("""        gh api graphql -f query='
        mutation {
          addSubIssue(input: {issueId: "PARENT_NODE_ID", subIssueId: "CHILD_NODE_ID"}) {
            issue {
              id
            }
          }
        }
        '""")
    print("     See: https://docs.github.com/en/graphql/reference/mutations for more details.")
    return True

def main():
    parser = argparse.ArgumentParser(description='Migrate workflow feedback to GitHub issues')
    parser.add_argument('--dry-run', action='store_true', help='Show what would be done without creating issues')
    parser.add_argument('--start-index', type=int, default=0, help='Start from entry N (for resuming)')
    parser.add_argument('--input', default='.github/archive/workflow-improvements.md', help='Input file path')
    
    args = parser.parse_args()
    
    print("=" * 70)
    print("Workflow Feedback Migration Script")
    print("=" * 70)
    print(f"Parent Issue: #{PARENT_ISSUE}")
    print(f"Input File: {args.input}")
    if args.dry_run:
        print("Mode: DRY RUN (no changes will be made)")
    print("=" * 70)
    print()
    
    # Parse entries
    try:
        entries = parse_feedback_file(args.input)
    except FileNotFoundError:
        print(f"Error: Could not find file: {args.input}")
        sys.exit(1)
    
    print(f"Found {len(entries)} feedback entries")
    print(f"Starting from entry {args.start_index}")
    print()
    
    # Migrate entries
    created_issues = []
    for i, entry in enumerate(entries[args.start_index:], start=args.start_index):
        print(f"\n[{i+1}/{len(entries)}] {entry['title']}")
        
        body = format_issue_body(entry)
        state = 'closed' if entry['is_closed'] else 'open'
        
        issue_number = create_issue_gh_cli(entry['title'], body, state, args.dry_run)
        
        if issue_number:
            created_issues.append(issue_number)
            link_sub_issue(PARENT_ISSUE, issue_number, args.dry_run)
    
    # Summary
    print("\n" + "=" * 70)
    print("MIGRATION SUMMARY")
    print("=" * 70)
    print(f"Total entries: {len(entries)}")
    print(f"Migrated: {len(created_issues)} (from index {args.start_index})")
    print(f"Open: {sum(1 for e in entries if not e['is_closed'])}")
    print(f"Closed: {sum(1 for e in entries if e['is_closed'])}")
    print()
    
    if not args.dry_run and created_issues:
        print("Created issues:", ", ".join(f"#{n}" for n in created_issues))
        print()
        print("⚠ MANUAL STEP REQUIRED:")
        print(f"  Link all created issues as sub-issues of #{PARENT_ISSUE}")
        print("  This requires GraphQL API calls or GitHub UI")
    
    print("=" * 70)

if __name__ == '__main__':
    main()
