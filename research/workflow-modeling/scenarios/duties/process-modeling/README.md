# Process Modeling Duty Test Scenarios

**Purpose**: Test scenarios for the Process Modeling duty

**Created**: 2025-11-12  
**Duty**: Process Modeling

---

## Overview

This directory contains test scenarios for validating the Process Modeling duty's behavior. The Process Modeling duty is responsible for improving workflows, procedures, and processes through systematic testing.

---

## Test Scenarios

### Scenario 001: Following Change Procedures
**File**: `scenario-001-following-change-procedures.md`  
**Tests**: Adherence to testing framework change procedures

### Scenario 002: Kernel Leak Detection
**File**: `scenario-002-kernel-leak-detection.md`  
**Tests**: Detection of platform-specific code in higher layers

### Scenario 003: Dependency Leak Detection
**File**: `scenario-003-dependency-leak-detection.md`  
**Tests**: Detection of duplicated procedure logic in duties

### Scenario 004: Graph Maintenance
**File**: `scenario-004-graph-maintenance.md`  
**Tests**: Graph update procedures and validation

---

## Test Methodology

All tests use **tabletop simulation**:

1. Agent reads the scenario
2. Agent follows Process Modeling duty procedure
3. Agent records observations and results
4. Scenario is marked PASS/FAIL with notes

---

## Adding New Scenarios

When adding new test scenarios:

1. Follow the naming convention: `scenario-XXX-descriptive-name.md`
2. Include scenario description, expected behavior, and success criteria
3. Update this README with the new scenario
4. Ensure the scenario tests a specific aspect of the Process Modeling duty
