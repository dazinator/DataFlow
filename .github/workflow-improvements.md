# Workflow Improvement Suggestions

This document tracks suggestions for improving our development workflows based on real experiences from Copilot agents and reviewers working on issues.

## Purpose

Before any PR is marked ready for review, Copilot agents should:
1. **Evaluate workflow effectiveness** for the current task
2. **Document what worked well and what didn't**
3. **Propose specific improvements** to the workflow
4. **Add suggestions to this file** in the appropriate section

**Before adding a suggestion**: Check if it's already been raised in the relevant section below.

---

## Research Workflow Improvements

### Suggestions

<!-- Add research workflow improvement suggestions here -->
<!-- Format:
- **Date**: YYYY-MM-DD
- **Issue/PR**: #[number]
- **What worked well**: [description]
- **What didn't work well**: [description]
- **Suggested improvement**: [specific actionable improvement]
-->

---

## Implementation Workflow Improvements

### Suggestions

<!-- Add implementation workflow improvement suggestions here -->
<!-- Format:
- **Date**: YYYY-MM-DD
- **Issue/PR**: #[number]
- **What worked well**: [description]
- **What didn't work well**: [description]
- **Suggested improvement**: [specific actionable improvement]
-->

---

## General Workflow Improvements

### Suggestions

- **Date**: 2025-11-05
- **Issue/PR**: Self-improvement loop implementation
- **What worked well**: 
  - Clear requirements in the issue made it straightforward to understand what needed to be implemented
  - The existing workflow documentation structure provided good context for where to add the self-improvement requirements
  - Multiple parallel file reads helped quickly understand the repository structure
  - All workflow files were well-organized and easy to locate
- **What didn't work well**: 
  - Initially unclear whether the workflow-improvements.md should be in the root .github/ directory or elsewhere - settled on .github/ next to copilot-instructions.md as specified in requirements
  - The relationship between different workflow documents (copilot-instructions.md vs RESEARCH_WORKFLOW.md) required careful review to ensure consistent messaging
- **Suggested improvement**: 
  - Consider adding a visual diagram (mermaid flowchart) to show the relationship between different workflow documents (copilot-instructions.md, RESEARCH_WORKFLOW.md, issue templates, workflow-improvements.md) and when each is consulted during the development process
  - Add a "Quick Start" section at the top of copilot-instructions.md that references the self-improvement loop early, so agents see it immediately
  - Consider adding a GitHub Actions workflow that checks if workflow-improvements.md has been updated in PRs (though this might be overly prescriptive)

<!-- Add general workflow improvement suggestions here that apply to all workflows -->
<!-- Format:
- **Date**: YYYY-MM-DD
- **Issue/PR**: #[number]
- **What worked well**: [description]
- **What didn't work well**: [description]
- **Suggested improvement**: [specific actionable improvement]
-->

---

## POC Workflow Improvements

### Suggestions

<!-- Add POC-specific workflow improvement suggestions here -->
<!-- Format:
- **Date**: YYYY-MM-DD
- **Issue/PR**: #[number]
- **What worked well**: [description]
- **What didn't work well**: [description]
- **Suggested improvement**: [specific actionable improvement]
-->

---

## Documentation and Communication Improvements

### Suggestions

<!-- Add suggestions for improving documentation, issue templates, or communication -->
<!-- Format:
- **Date**: YYYY-MM-DD
- **Issue/PR**: #[number]
- **What worked well**: [description]
- **What didn't work well**: [description]
- **Suggested improvement**: [specific actionable improvement]
-->

---

## How to Use This File

### For Copilot Agents

Before marking a PR ready for review:
1. Reflect on the workflow you followed for your task
2. Identify what worked well and what could be improved
3. Add a new suggestion entry to the appropriate section above
4. Be specific and actionable in your suggestions
5. Reference the current issue/PR number

### For Reviewers

When reviewing workflow improvement suggestions:
1. Evaluate the merit of each suggestion
2. Prioritize suggestions that would have the most impact
3. When implementing a suggestion, update the relevant workflow documentation:
   - `.github/copilot-instructions.md` for general Copilot guidance
   - `/research/RESEARCH_WORKFLOW.md` for research-specific processes
   - `.github/ISSUE_TEMPLATE/*.md` for issue template improvements
4. Mark implemented suggestions with `[IMPLEMENTED - YYYY-MM-DD]` prefix
5. Archive old implemented suggestions periodically to keep the file focused

---

## Implemented Suggestions Archive

<!-- Move implemented suggestions here with implementation date -->
<!-- This helps maintain a clean working list while preserving history -->

### [IMPLEMENTED - YYYY-MM-DD] Example Suggestion
- **Original Date**: YYYY-MM-DD
- **Issue/PR**: #[number]
- **Improvement**: [description]
- **Implemented in**: [PR or commit reference]
