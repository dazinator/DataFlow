# Research Code Reversion Plan

**After PR Approval**: The following exploratory code will be REVERTED to keep the POC clean.

---

## Files to REVERT (Exploratory Code)

These files were created for research validation and will be removed after approval:

### POC Implementation (REVERT)
- `/poc/DataFlow.POC/Core/SelectiveRoutingEdgeStrategy.cs`
  - **Saved to**: `/research/selective-content-routing/handover/prototype/SelectiveRoutingEdgeStrategy.cs`
  - **Reason**: Exploratory prototype for validation
  - **Next Steps**: Implementation duty will integrate based on handover specs

### Tests (REVERT)
- `/poc/DataFlow.POC.Tests/SelectiveRoutingEdgeStrategyTests.cs`
  - **Saved to**: `/research/selective-content-routing/handover/prototype/SelectiveRoutingEdgeStrategyTests.cs`
  - **Reason**: Research validation tests
  - **Next Steps**: Implementation duty will adapt for production

### Benchmarks (REVERT)
- `/poc/DataFlow.POC.Benchmarks/SelectiveRoutingBenchmark.cs`
  - **Saved to**: `/research/selective-content-routing/handover/prototype/SelectiveRoutingBenchmark.cs`
  - **Reason**: Performance validation prototype
  - **Next Steps**: Implementation duty will refine and run

---

## Files to KEEP (Formal Documentation)

These files provide lasting value and will NOT be reverted:

### Research Documentation (KEEP)
- `/research/selective-content-routing/README.md` - Research findings
- `/research/selective-content-routing/research-plan.md` - Research methodology
- `/research/selective-content-routing/notes/option1-analysis.md` - Design analysis
- `/research/selective-content-routing/handover/README.md` - Implementation specifications
- `/research/selective-content-routing/handover/prototype/` - Saved prototype code

### Analysis Documentation (KEEP)
- `/poc/docs/analysis/routing/selective-routing-analysis.md` - Formal analysis

---

## Reversion Procedure

**After PR Review Approval**:

1. Create a separate PR for cleanup:
   ```bash
   git checkout -b research/cleanup-selective-routing
   ```

2. Revert exploratory code:
   ```bash
   git rm poc/DataFlow.POC/Core/SelectiveRoutingEdgeStrategy.cs
   git rm poc/DataFlow.POC.Tests/SelectiveRoutingEdgeStrategyTests.cs
   git rm poc/DataFlow.POC.Benchmarks/SelectiveRoutingBenchmark.cs
   ```

3. Verify research documentation is preserved:
   ```bash
   git status research/
   git status poc/docs/analysis/
   ```

4. Commit and create PR:
   ```bash
   git commit -m "Research: Revert exploratory code for selective routing (research complete)"
   git push origin research/cleanup-selective-routing
   ```

5. Link cleanup PR to original research issue (#512)

---

## Why Revert?

**Research Code Fate**: Research creates temporary exploratory code to validate approaches. This code:

✅ **Served its purpose**: Validated that `SelectiveRoutingEdgeStrategy` works as expected
✅ **Created knowledge**: Generated research documentation and implementation specs
✅ **Informed implementation**: Saved in handover folder for implementation duty

❌ **Not production-ready**: Research prototypes may cut corners for speed
❌ **Needs integration**: Implementation duty will properly integrate with full testing
❌ **Documentation > Code**: Research value is in documentation, not code itself

**Best Practice**: Keep POC clean by removing exploratory code after research completes.

---

## Implementation Duty Handover

**Implementation Duty** will:
1. Review research findings and handover documentation
2. Review saved prototype code in `/research/selective-content-routing/handover/prototype/`
3. Integrate `SelectiveRoutingEdgeStrategy` properly into POC
4. Add comprehensive tests and documentation
5. Run performance benchmarks to validate improvements
6. Merge to main branch when complete

**Research Duty** deliverables:
✅ Research findings document
✅ Analysis document  
✅ Implementation handover specifications
✅ Saved prototype code
✅ Test scenarios and validation approach
✅ Performance analysis and recommendations

---

## Verification

Before reversion, verify:
- [x] All research documentation committed
- [x] Prototype code saved to handover folder
- [x] Implementation handover complete
- [x] Analysis documentation complete
- [x] Self-improvement feedback submitted (pending)

After reversion, verify:
- [ ] Exploratory code removed from `/poc`
- [ ] Research documentation still present in `/research`
- [ ] Analysis documentation still present in `/poc/docs/analysis`
- [ ] Handover folder intact
- [ ] No broken links in documentation
