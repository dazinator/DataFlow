# Buffer Node Epoch Stream Testing

## Goal
Test what happens when buffer nodes are used with epoch streams.

## Current Understanding

### Buffer Node Implementation
- `TypedBufferNodeRouter<T>` routes plain items of type `T`
- Implements `ITypedEdgeRouter` but throws `NotSupportedException` on `.Strategy` access
- Not designed to handle `IEpochStream<T>` containers

### Epoch Stream Architecture
- Edges route `IEpochStream<T>` containers, not individual items
- Epoch streams contain metadata (epoch vector, epoch scope)
- Recent optimization (issue #39) uses pre-compiled delegates for epoch stream routing

## Test Plan

1. Create a test with buffer node receiving epoch streams
2. Create a test with buffer node sending epoch streams
3. Document behavior and errors
4. Identify architectural mismatch

## Test Results

(To be filled in after running tests)
