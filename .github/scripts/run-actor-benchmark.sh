#!/bin/bash
# Script to run ActorBlock benchmarks with dotnet-counters and generate visualizations
# Usage: ./run-actor-benchmark.sh <mode> <results-dir>
#   mode: steady, rotation, or memory

set -e

MODE="$1"
RESULTS_DIR="$2"
TIMESTAMP=$(date +"%Y-%m-%d_%H-%M-%S")

# Configuration
ITEM_COUNT=10000
ROTATE_AFTER_ROTATION=100
ROTATE_AFTER_MEMORY=50

echo "=============================================================================="
echo "Running ActorBlock Benchmark: $MODE"
echo "=============================================================================="
echo ""
echo "Configuration:"
echo "  Mode: $MODE"
echo "  Items: $ITEM_COUNT"
if [ "$MODE" = "rotation" ]; then
    echo "  Rotate After: $ROTATE_AFTER_ROTATION items"
elif [ "$MODE" = "memory" ]; then
    echo "  Rotate After: $ROTATE_AFTER_MEMORY items"
fi
echo "  Results Dir: $RESULTS_DIR"
echo ""

# Determine benchmark command and output prefix
if [ "$MODE" = "steady" ]; then
    BENCHMARK_CMD="actor-steady $ITEM_COUNT"
    OUTPUT_PREFIX="actor-steady"
elif [ "$MODE" = "rotation" ]; then
    BENCHMARK_CMD="actor-rotation $ITEM_COUNT $ROTATE_AFTER_ROTATION"
    OUTPUT_PREFIX="actor-rotation"
elif [ "$MODE" = "memory" ]; then
    BENCHMARK_CMD="actor-memory $ITEM_COUNT $ROTATE_AFTER_MEMORY"
    OUTPUT_PREFIX="actor-memory"
else
    echo "ERROR: Invalid mode '$MODE'. Must be 'steady', 'rotation', or 'memory'."
    exit 1
fi

# Create results directory
mkdir -p "$RESULTS_DIR"

# Output files
CSV_FILE="${RESULTS_DIR}/${OUTPUT_PREFIX}_${TIMESTAMP}.csv"
CHARTS_DIR="${RESULTS_DIR}/charts_${OUTPUT_PREFIX}_${TIMESTAMP}"
SUMMARY_FILE="${CHARTS_DIR}/summary.md"

echo "Building benchmark project..."
dotnet build -c Release poc/DataFlow.Benchmarks/DataFlow.Benchmarks.csproj --no-restore

# Start the benchmark in the background
echo ""
echo "Starting benchmark process..."
dotnet run --project poc/DataFlow.Benchmarks/DataFlow.Benchmarks.csproj \
    --no-build -c Release -- $BENCHMARK_CMD &
BENCHMARK_PID=$!

# Wait for process to start
sleep 2

# Check if process is still running
if ! kill -0 $BENCHMARK_PID 2>/dev/null; then
    echo "ERROR: Benchmark process failed to start or completed too quickly"
    exit 1
fi

echo "Benchmark started (PID: $BENCHMARK_PID)"
echo ""
echo "Collecting metrics with dotnet-counters..."

# Collect counters
dotnet-counters collect \
    --process-id $BENCHMARK_PID \
    --output "$CSV_FILE" \
    --format csv \
    --counters System.Runtime

echo ""
echo "Counters collection completed"
echo "CSV output: $CSV_FILE"

# Generate visualizations
if [ -f "$CSV_FILE" ]; then
    echo ""
    echo "Generating visualizations..."
    mkdir -p "$CHARTS_DIR"
    
    # Run Python visualization script
    python3 .github/scripts/visualize-counters.py "$CSV_FILE" --output "$CHARTS_DIR"
    
    # Create summary markdown
    cat > "$SUMMARY_FILE" << EOF
# ActorBlock Benchmark Results - $MODE

**Timestamp:** $TIMESTAMP  
**Mode:** $MODE  
**Items Processed:** $ITEM_COUNT  
EOF

    if [ "$MODE" = "rotation" ]; then
        echo "**Rotation Frequency:** Every $ROTATE_AFTER_ROTATION items" >> "$SUMMARY_FILE"
        echo "**Expected Rotations:** ~$((ITEM_COUNT / ROTATE_AFTER_ROTATION))" >> "$SUMMARY_FILE"
    elif [ "$MODE" = "memory" ]; then
        echo "**Rotation Frequency:** Every $ROTATE_AFTER_MEMORY items" >> "$SUMMARY_FILE"
        echo "**Expected Rotations:** ~$((ITEM_COUNT / ROTATE_AFTER_MEMORY))" >> "$SUMMARY_FILE"
        echo "**Memory per Rotation Cycle:** ~${ROTATE_AFTER_MEMORY} KB" >> "$SUMMARY_FILE"
    fi

    cat >> "$SUMMARY_FILE" << EOF

## Overview

This benchmark measures ActorBlock performance using dotnet-counters for time-series metric collection.

### Mode: $MODE

EOF

    if [ "$MODE" = "steady" ]; then
        cat >> "$SUMMARY_FILE" << EOF
**Steady-state mode** runs without actor rotation, providing a baseline performance measurement.
The actor instance remains active for the entire duration, processing all items in a single DI scope.

**Key Metrics to Observe:**
- Memory allocation patterns without rotation
- GC behavior with long-lived actor instance
- Baseline throughput without rotation overhead

EOF
    elif [ "$MODE" = "rotation" ]; then
        cat >> "$SUMMARY_FILE" << EOF
**Rotation mode** periodically rotates the actor (every $ROTATE_AFTER_ROTATION items), demonstrating:
- Overhead of DI scope rotation
- Memory management benefits from periodic cleanup
- Impact on throughput vs steady-state

**Expected Behavior:**
- Periodic memory spikes as new scopes are created
- Regular GC collections as old scopes are disposed
- Slight throughput reduction vs steady-state (~10-20%)

EOF
    elif [ "$MODE" = "memory" ]; then
        cat >> "$SUMMARY_FILE" << EOF
**Memory-intensive mode** simulates real-world scenarios where actors accumulate memory:
- Each item adds ~1 KB to actor's internal buffer
- Rotation (every $ROTATE_AFTER_MEMORY items) releases accumulated memory
- Demonstrates how rotation prevents unbounded memory growth

**Expected Behavior:**
- Sawtooth memory pattern: growth → rotation → drop → growth
- Peak memory: ~${ROTATE_AFTER_MEMORY} KB per rotation cycle
- More frequent GC collections due to memory pressure

EOF
    fi

    cat >> "$SUMMARY_FILE" << EOF
## Charts

The following charts visualize key performance metrics over time:

![Performance Overview](comparison_charts.png)

### Reading the Charts

1. **GC Heap Size** - Total managed heap memory
   - Steady-state: May show gradual growth
   - Rotation: Shows periodic drops as scopes are disposed
   - Memory-intensive: Shows clear sawtooth pattern

2. **Allocation Rate** - Memory allocated per second
   - Indicates allocation pressure
   - Spikes during active processing
   - Drops during idle periods

3. **GC Collections** - Cumulative GC events (Gen 0, 1, 2)
   - Gen 0: Frequent, short collections
   - Gen 1: Occasional medium collections
   - Gen 2: Rare, expensive full collections

4. **Working Set** - Total process memory (managed + unmanaged)
   - May be higher than GC heap
   - Includes native allocations and OS overhead

5. **ThreadPool Usage** - Active thread count
   - Shows concurrency level
   - Should be relatively stable for this benchmark

6. **CPU Usage** - Processor utilization
   - Indicates computational load
   - May show spikes during processing bursts

## Raw Data

- **CSV File:** \`${OUTPUT_PREFIX}_${TIMESTAMP}.csv\`
- **Charts Directory:** \`charts_${OUTPUT_PREFIX}_${TIMESTAMP}/\`

Use the CSV file for detailed analysis with Excel, Python pandas, or other tools.

## Benchmark Configuration

\`\`\`
dotnet run -c Release -- $BENCHMARK_CMD
\`\`\`

## Notes

- All measurements collected using \`dotnet-counters\` with System.Runtime counters
- Time-series data sampled approximately every 100-500ms
- Charts generated using matplotlib visualization script
EOF

    echo ""
    echo "Summary created: $SUMMARY_FILE"
    echo "Charts directory: $CHARTS_DIR"
else
    echo "WARNING: CSV file not found at $CSV_FILE"
    echo "Skipping visualization generation"
fi

echo ""
echo "=============================================================================="
echo "ActorBlock Benchmark Completed: $MODE"
echo "=============================================================================="
echo ""
echo "Results:"
echo "  CSV: $CSV_FILE"
if [ -d "$CHARTS_DIR" ]; then
    echo "  Charts: $CHARTS_DIR"
    echo "  Summary: $SUMMARY_FILE"
fi
echo ""
