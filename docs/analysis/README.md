# Analysis Documentation

This folder contains investigative and exploratory documentation, including:

- Problem discovery and root cause analysis
- Performance benchmarks and comparisons
- Technology evaluations and comparisons
- Exploratory research and feasibility studies
- Tech debt investigations

## Purpose

Analysis documents are used for **problem discovery** and **investigation**. They help us understand problems before we design solutions.

## When to Create an Analysis Document

Create an analysis document when you need to:

- Investigate the root cause of an issue
- Benchmark or compare different approaches
- Explore a new technology or pattern
- Document tech debt findings
- Perform exploratory research

## Structure

Each analysis topic should have its own subfolder:

```
/docs/analysis/
  /<topic>/
    README.md          # Main analysis document
    benchmarks/        # Performance data (if applicable)
    comparisons/       # Comparison tables/charts (if applicable)
    findings/          # Supporting documents
```

## What to Include

Your analysis document should include:

1. **Context and Motivation**: Why is this analysis needed?
2. **Observations and Metrics**: What did you discover?
3. **Alternative Approaches**: What options were considered?
4. **Outcome**: Conclusions and next steps
5. **Link to Design**: If this leads to a solution design, link to it

## Linking to Issues

Reference analysis documents from GitHub issues using relative paths:

```markdown
See analysis: [Topic Analysis](../../docs/analysis/<topic>/README.md)
```

In your analysis document, link back to the related issue:

```markdown
Related issue: uniun-technology/lib-dataflow#123
```

## Cross-Linking with Design Documents

When analysis leads to a design proposal:

- In the analysis document: Add "See design: [Design Name](../design/<topic>/README.md)"
- In the design document: Add "Based on analysis: [Analysis Name](../analysis/<topic>/README.md)"
