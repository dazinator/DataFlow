# Scenario: Copilot Faces Large-Scale Test Migration

## Context

A copilot agent is implementing a migration task that requires updating 25 similar test files. Each test file needs the same pattern changes: replacing old actor pattern with new unified actor pattern. The copilot is about to start making repetitive edits file-by-file.

## Starting Point

- Copilot has implementation issue for "Migrate tests to unified actor API"
- Issue mentions 20-30 test files need similar updates
- Copilot reviewing `.team/prompts/IMPLEMENTATION_WORKFLOW.md` for guidance
- Copilot considering whether to edit each file manually or create helper

## Steps to Follow

Copilot would:

1. Read implementation workflow Quick Start
2. Look for guidance on handling bulk migrations
3. Check "Using Ecosystem Tools" section for automation guidance
4. Find recommendation: For >5 similar files, consider helper patterns
5. Decide whether to:
   - Create helper factory methods (e.g., `ActorFactory.CreateNoOpProcessor<T>()`)
   - Use scripting/code generation
   - Manual edits if variation is high
6. Implement chosen approach
7. Document pattern for future migrations

## Expected Outcome

**With the improvement:**
- Workflow includes "Suggest helper patterns for bulk migrations"
- Copilot sees guidance: ">5 similar files → consider helpers"
- Example provided: factory methods for actor patterns
- Reference to "Using Ecosystem Tools" in copilot-instructions.md
- Copilot creates helper, reduces duplication, saves time
- Future migrations benefit from established pattern

**Without the improvement:**
- No guidance on bulk migration patterns
- Copilot might manually edit all 25 files
- Repetitive, error-prone, time-consuming
- Each copilot session re-invents the wheel
- Inconsistent approaches across different migrations
- Missed opportunity to establish reusable patterns

## Success Criteria

- [ ] Workflow mentions bulk migration pattern guidance
- [ ] Threshold specified (e.g., ">5 similar files")
- [ ] Examples provided (factory methods, templates, etc.)
- [ ] Distinction clear: when to automate vs manual edits
- [ ] Reference to "Using Ecosystem Tools" section
- [ ] Copilot can decide: helper pattern vs manual based on guidance

## Test Result

**Status**: [x] PASS  [ ] FAIL

**Notes**: 
**BASELINE SIMULATION** (2025-11-08):
- No specific bulk migration guidance in IMPLEMENTATION_WORKFLOW.md
- copilot-instructions.md has generic "use refactoring tools" advice
- No threshold guidance (e.g., >5 files)
- No examples of helper patterns (factory methods, templates)
- Copilot must decide approach without specific guidance
- Risk of choosing inefficient manual approach for large migrations
- **Needed**: Add specific bulk migration guidance with thresholds and examples

**IMPROVED SIMULATION** (2025-11-08):
- ✅ Added "Bulk Migration Strategies" section to IMPLEMENTATION_WORKFLOW.md Step 6
- Clear decision criteria table: 1-5 files (manual), 5-15 (helpers), 15+ (scripting)
- Concrete examples: NoOpProcessorActor, TestActorFactory patterns
- Best practices: create helper first, test with 2-3 files, then scale
- When to use each approach clearly explained
- Balance guidance: helper creation time vs manual edit time
- **Status**: PASS - All success criteria met
