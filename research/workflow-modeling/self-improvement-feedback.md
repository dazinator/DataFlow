# Self-Improvement Feedback

**Duty**: Process Modeling  
**Work Item**: TBD - Improve Scheduled Triage Workflow and Template  
**Submitted**: 2025-11-13

---

## What Worked Well ✅

1. **Clear step-by-step Process Modeling duty procedure** - The procedure in `.team/duties/PROCESS_MODELING_DUTY.md` provided excellent guidance on the complete workflow from plan creation to archiving

2. **Test scenario structure and examples** - Having existing scenario examples in `/research/workflow-modeling/scenarios/` made it easy to understand the expected format and structure

3. **Leak detection tooling** - The automated `.team/scripts/check-kernel-leaks.sh` and `.team/scripts/check-dependency-leaks.sh` scripts made validation straightforward and caught no issues with my changes

4. **Plan.md template** - The existing template structure in `research/workflow-modeling/plan.md` made it clear what information to track during the work

5. **File ownership clarity** - The copilot instructions clearly stated which files are owned by Process Modeling duty, making it clear I had authority to make these changes

---

## What Didn't Work Well ❌

1. **Scenario naming ambiguity** - The duty says "Format: `scenario-NNN-[type]-[brief-description].md`" but doesn't clarify if NNN should be sequential across all scenarios or just within a project. I used project-level numbering (001-004) which seems right.

2. **GitHub Actions script testing** - No guidance on how to test GitHub Actions workflow changes. I could only validate the logic by inspection, not actual execution. The tabletop simulation covered the logic but not the actual GitHub API integration.

3. **Issue number in history** - History template shows "PR | #XXX" but the issue creating this work doesn't have a number yet (shows as "TBD"). Not clear if I should update this later or leave as TBD.

4. **Graph update decision criteria** - While I concluded graph update was N/A (changes to Actions scripts, not prompt system), the criteria for when graph updates ARE needed could be more explicit in the duty procedure.

5. **Template simplification scope** - No specific guidance on what level of simplification is appropriate for issue templates. I removed references and simplified based on my judgment, but clearer criteria would help.

---

## Proposed Improvements 💡

1. **Add scenario numbering guidance** - In Process Modeling duty, clarify: "Scenario numbers (NNN) are sequential within each scenario directory, starting from 001. Each new project gets its own directory with numbering reset."

2. **Add GitHub Actions testing guidance** - In Process Modeling duty or copilot instructions, add section: "Testing GitHub Actions changes: Validate script logic through code inspection and tabletop simulation. Actual execution testing requires manual workflow dispatch after PR merge."

3. **Add TBD placeholder guidance in history** - In Process Modeling duty Step 9 (Update History), add note: "If PR number not yet available, use 'TBD' as placeholder. Update after PR is created."

4. **Clarify graph update triggers** - In Process Modeling duty Step 8, add explicit list: "Graph updates required when: (1) Adding/removing duties, (2) Adding/removing procedures, (3) Adding/removing kernel operations, (4) Changing dependency relationships between any prompt system files. NOT required for: GitHub Actions scripts, issue templates, documentation files."

5. **Add template simplification criteria** - In Process Modeling duty (or document hygiene guide), add guidance: "Issue template simplification: Remove broken links, outdated file paths, and references to non-existent directories. Keep template focused on essential fields. Target: user can fill out template in under 2 minutes."

---

## Additional Notes

This was my first Process Modeling work item, and the duty procedure was generally excellent. The main gaps were around edge cases and testing approaches for non-code changes. The work completed successfully with all test scenarios passing.
