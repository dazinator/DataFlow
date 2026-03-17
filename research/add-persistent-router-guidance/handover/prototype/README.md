# Prototype: Generic ERP Handler Pattern (Approach C)

This prototype demonstrates the recommended approach for migrating from `AddPersistentRouter` to
the POC DataFlow library. It shows a single actor block that dynamically dispatches to
system-type-specific handlers, with configuration-driven tenant support.

## Purpose

Validate that:
1. A single actor block can handle multiple ERP system types without per-type graph topology.
2. New tenants don't require new routes or restarts.
3. The "unrouted" fallback path works correctly.
4. The pattern integrates naturally with the POC DI model.

## Files

- `ErpHandlerPrototype.cs` — Core interfaces and implementation sketch
- `DispatcherBlockPrototype.cs` — The dispatcher actor block
- `GraphRegistrationPrototype.cs` — DI registration example

## How to Use

This is reference code for the implementation team. It is NOT compiled into the POC project —
copy and adapt these patterns into the target application.

## Key Points

1. `INamedErpSystemHandler` replaces per-route block instantiation.
2. `ErpPostingActor` replaces the `PersistentRouter` + per-route transform blocks.
3. `item.System` provides the same info that was previously injected as a constructor arg.
4. DI scoping is handled by the POC actor model — no manual scope creation needed.
