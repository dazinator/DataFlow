// ---------------------------------------------------------------------------
// Example: How a dynamic flow definition JSON looks
// Config values are NOT stored inline — they are stored in a separate blob
// repository (IBlockConfigBlobRepository) and referenced by ID.
// ---------------------------------------------------------------------------

// Example 1: Linear pipeline without config (topology only)
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

// Example 2: Linear pipeline with config blob references (no inline values)
// The actual config JSON (including encrypted secrets) lives in the blob store.
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
  "blockConfigRefs": {
    "global:producer":  "cfg-blob-a1b2c3d4",
    "global:batch":     "cfg-blob-e5f6g7h8",
    "global:processor": "cfg-blob-i9j0k1l2"
  }
}
*/

// The blobs referenced above are stored in IBlockConfigBlobRepository.
// When the blob for "global:producer" was saved, its plain-text JSON was:
//   { "itemCount": 50, "delayMs": 200 }
// For a block with secrets, the plain-text submitted by the designer would be:
//   { "host": "prod-db", "apiKey": "sk-live-abc123" }
// The repository encrypts x-secret fields before storage; the stored blob contains:
//   { "host": "prod-db", "apiKey": "<encrypted-ciphertext>" }
// On load for execution, the repository decrypts transparently and returns the original.

// Example 3: Simple two-block flow (type-compatible pair)
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
