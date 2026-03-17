# MyFirstDataFlow Prototype

This is a working prototype console app that validates the getting started guide.

## Purpose

- Validates that the getting started guide is correct
- Provides a reference implementation
- Demonstrates all patterns from the guide

## Structure

- `Program.cs` - Complete working example from the guide
- `GlobalUsings.cs` - Global using directives
- `MyFirstDataFlow.csproj` - Project file

## How to Run

From the repository root:

```bash
cd research/new-getting-started-guide/handover/prototype/MyFirstDataFlow
dotnet run
```

## What It Does

1. Reads lines from console input
2. Transforms to uppercase
3. Writes to console output
4. Exits when you enter an empty line

## Validation

✅ Compiles successfully
✅ Runs without errors
✅ Demonstrates:
- DI registration with `AddDataFlows`
- Named block registration
- Graph definition with `UseBlock`
- Keyed service resolution
- Execution context creation
- Graph execution

## Known Issues

If you see type ambiguity errors, use fully qualified names as shown in the comments in `Program.cs`.
