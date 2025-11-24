# Research Plan: POC Metrics Mapping

## Research Objective

Analyze the production code metrics implementation and map each metric to proposed locations in the POC code to achieve parity where it makes sense. Document any metrics that cannot be accommodated without significant impact.

## Research Questions

1. **Can we achieve parity of metrics in POC compared to production code?**
   - What are all the metrics currently tracked in production?
   - Which metrics make sense for POC architecture?
   - Which metrics would require significant changes to POC?

2. **Can we implement ActivitySource / Activity in a logical way for POC code?**
   - How is ActivitySource currently implemented in production?
   - What are the key tracing points in POC that should emit activities?
   - What activity hierarchy makes sense for POC architecture?

3. **Production code uses MonitoredChannel wrapper - do we need similar or simpler approach?**
   - What does MonitoredChannel do in production?
   - Can we simplify for POC or do we need similar functionality?
   - What are the alternatives for observing channel queue depths?

## Success Metrics

### Quantitative
- All metrics in IDataFlowMetrics mapped to POC locations
- Side-by-side comparison table: Production metric → POC location
- Coverage percentage: X% of production metrics can be implemented in POC

### Qualitative
- Clear documentation of why certain metrics can't be implemented
- Logical ActivitySource implementation proposal for POC
- Simplified or alternative approach to MonitoredChannel if applicable

### Baseline
- Current state: POC has no metrics implementation
- Production has: 9 instruments (counters, histograms, gauges, up-down counters)
- Production has: ActivitySource with 2 activity types (Flow, Block)

### Validation
- Document review by team
- Verify proposed locations align with POC architecture
- Ensure no significant architectural changes required

## Validation Approach

1. **Metrics Inventory**: Create complete inventory of production metrics
2. **POC Code Mapping**: Map each metric to specific POC code locations
3. **Gap Analysis**: Document metrics that can't be easily mapped
4. **ActivitySource Design**: Propose ActivitySource implementation for POC
5. **MonitoredChannel Analysis**: Compare production approach with POC needs
6. **Test Similarity**: Review production tests, propose similar tests for POC

## Expected Outcomes

- Research documentation in `/research/poc-metrics-mapping/`
- Implementation-ready work item with complete specifications
- Metrics mapping table (Production → POC)
- ActivitySource implementation design for POC
- Channel monitoring approach recommendation
- Test scenarios for POC metrics validation
- Formal documentation (design docs, ADRs if needed)

## Timeline

Estimated: 2-3 days
- Day 1: Metrics inventory and initial mapping
- Day 2: ActivitySource design and MonitoredChannel analysis
- Day 3: Documentation and handover preparation

## Scope

**In Scope:**
- Mapping all IDataFlowMetrics metrics to POC
- ActivitySource implementation design for POC
- Channel monitoring approach for POC
- Test scenario documentation

**Out of Scope:**
- Actual implementation (will be handed over)
- OpenTelemetry integration (document separately if needed)
- Performance benchmarking (can be done during implementation)

## Notes

- Focus on POC-appropriate implementations, not direct production code copy
- Document architectural impacts clearly
- Provide rationale for any metrics that can't be implemented
- Consider POC's different architecture (graph-based, epoch-oriented)
