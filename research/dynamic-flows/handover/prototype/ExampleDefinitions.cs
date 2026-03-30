// ---------------------------------------------------------------------------
// Example: How a dynamic flow definition JSON looks
// This file shows two example definitions that can be used with the prototype.
// ---------------------------------------------------------------------------

// Example 1: Linear pipeline (mirrors the hardcoded linear demo)
// File: linear-pipeline.json
/*
{
  "flowId": "linear-pipeline-v1",
  "version": 1,
  "name": "Linear Pipeline",
  "blocks": [
    { "instanceId": "global:producer",   "blockKey": "global:producer" },
    { "instanceId": "global:transform",  "blockKey": "global:transform" },
    { "instanceId": "global:batch",      "blockKey": "global:batch" },
    { "instanceId": "global:processor",  "blockKey": "global:processor" }
  ],
  "connections": [
    { "from": "global:producer",  "to": "global:transform",  "bufferCapacity": 100 },
    { "from": "global:transform", "to": "global:batch",      "bufferCapacity": 100 },
    { "from": "global:batch",     "to": "global:processor",  "bufferCapacity": 100 }
  ]
}
*/

// Example 2: Linear pipeline with block configuration snapshot
// File: linear-pipeline-with-config.json
/*
{
  "flowId": "linear-pipeline-configured",
  "version": 2,
  "name": "Linear Pipeline (50 items, batch-5)",
  "blocks": [
    { "instanceId": "global:producer",   "blockKey": "global:producer" },
    { "instanceId": "global:transform",  "blockKey": "global:transform" },
    { "instanceId": "global:batch",      "blockKey": "global:batch" },
    { "instanceId": "global:processor",  "blockKey": "global:processor" }
  ],
  "connections": [
    { "from": "global:producer",  "to": "global:transform",  "bufferCapacity": 100 },
    { "from": "global:transform", "to": "global:batch",      "bufferCapacity": 100 },
    { "from": "global:batch",     "to": "global:processor",  "bufferCapacity": 100 }
  ],
  "blockConfig": {
    "global:producer":  { "itemCount": 50, "delayMs": 200 },
    "global:batch":     { "batchSize": 5 },
    "global:processor": { "delayMs": 60 }
  }
}
*/

// Example 3: Simple two-block flow (Producer → Processor, no transform/batch)
// Shows that the engine works with any valid subset of registered blocks.
// File: minimal-pipeline.json
/*
{
  "flowId": "minimal-pipeline",
  "version": 1,
  "name": "Minimal Pipeline",
  "blocks": [
    { "instanceId": "global:producer",  "blockKey": "global:producer" },
    { "instanceId": "global:processor", "blockKey": "global:processor" }
  ],
  "connections": [
    { "from": "global:producer", "to": "global:processor", "bufferCapacity": 50 }
  ]
}
*/

// NOTE: Example 3 would fail type-validation if producer outputs `int` but processor
// expects `List<int>` — which is intentional. The DynamicFlowBuilder.Validate() method
// catches this at build time and surfaces a clear error message before any execution occurs.
