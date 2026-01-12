#!/bin/bash
# Script to run POC comparison benchmarks with dotnet-counters and generate visualizations
# Usage: ./run-poc-comparison.sh <mode> <output_dir>
#   mode: "simple" or "extended"

set -e

MODE="${1:-simple}"
OUTPUT_DIR="${2:-./poc/DataFlow.Benchmarks/benchmark-results}"
TIMESTAMP=$(date +"%Y-%m-%d_%H-%M-%S")

echo "=============================================================================="
echo "Running POC Comparison Benchmark with dotnet-counters"
echo "=============================================================================="
echo ""
echo "Mode: $MODE"
echo "Output Directory: $OUTPUT_DIR"
echo "Timestamp: $TIMESTAMP"
echo ""

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Install tools if needed
echo "Checking for required tools..."
if ! command -v dotnet-counters &> /dev/null; then
    echo "Installing dotnet-counters..."
    dotnet tool install -g dotnet-counters
fi

# Check for Python and required packages
if ! command -v python3 &> /dev/null; then
    echo "ERROR: python3 is required but not installed"
    exit 1
fi

# Try to import required packages
if python3 -c "import matplotlib, pandas" 2>/dev/null; then
    SKIP_VISUALIZATION=0
else
    echo "Warning: matplotlib or pandas not installed"
    echo "Install with: pip3 install matplotlib pandas"
    echo "Continuing without visualization generation..."
    SKIP_VISUALIZATION=1
fi

# Build the project
echo ""
echo "Building benchmark project..."
dotnet build -c Release poc/DataFlow.Benchmarks/DataFlow.Benchmarks.csproj

# Determine benchmark parameters based on mode
if [ "$MODE" = "simple" ]; then
    RECORD_COUNTS=(1000 5000 10000)
    MAX_CONCURRENCY=4
    ITERATIONS=3
    BENCHMARK_CMD="direct-simple"
else
    RECORD_COUNTS=(1000 5000 10000)
    MAX_CONCURRENCY=4
    BATCH_SIZE=100
    ITERATIONS=3
    BENCHMARK_CMD="direct-extended"
fi

# Run benchmarks for each configuration
for RECORDS in "${RECORD_COUNTS[@]}"; do
    echo ""
    echo "--------------------------------------------------------------------------"
    echo "Running benchmark: $MODE mode with $RECORDS records"
    echo "--------------------------------------------------------------------------"
    
    CSV_FILE="$OUTPUT_DIR/${MODE}_${RECORDS}rec_${TIMESTAMP}.csv"
    
    # Start the benchmark
    if [ "$MODE" = "simple" ]; then
        dotnet run --project poc/DataFlow.Benchmarks/DataFlow.Benchmarks.csproj \
            --no-build -c Release -- $BENCHMARK_CMD $RECORDS $MAX_CONCURRENCY $ITERATIONS &
    else
        dotnet run --project poc/DataFlow.Benchmarks/DataFlow.Benchmarks.csproj \
            --no-build -c Release -- $BENCHMARK_CMD $RECORDS $MAX_CONCURRENCY $BATCH_SIZE $ITERATIONS &
    fi
    
    BENCHMARK_PID=$!
    
    # Wait for process to start
    sleep 2
    
    # Check if still running
    if ! kill -0 $BENCHMARK_PID 2>/dev/null; then
        echo "ERROR: Benchmark process failed to start"
        continue
    fi
    
    echo "Collecting counters (PID: $BENCHMARK_PID)..."
    
    # Collect counters
    dotnet-counters collect \
        --process-id $BENCHMARK_PID \
        --output "$CSV_FILE" \
        --format csv \
        --counters System.Runtime || echo "Counter collection completed"
    
    echo "Counters saved to: $CSV_FILE"
    
    # Generate visualization if tools are available
    if [ -z "$SKIP_VISUALIZATION" ]; then
        echo "Generating visualization..."
        CHART_DIR="$OUTPUT_DIR/charts_${RECORDS}rec"
        python3 .github/scripts/visualize-counters.py "$CSV_FILE" --output "$CHART_DIR" || {
            echo "Warning: Visualization generation failed"
        }
    fi
    
    # Brief pause between runs
    sleep 3
done

# Create summary report
SUMMARY_FILE="$OUTPUT_DIR/${MODE}_comparison_summary_${TIMESTAMP}.md"

echo ""
echo "Generating summary report..."

cat > "$SUMMARY_FILE" << EOF
# POC vs Non-POC Comparison Benchmark Results

**Mode:** $MODE  
**Date:** $(date '+%Y-%m-%d %H:%M:%S')  
**Platform:** $(uname -s) $(uname -m)  

## Configuration

- **Pipeline Type:** $MODE
- **Concurrency:** $MAX_CONCURRENCY
- **Iterations per test:** $ITERATIONS
EOF

if [ "$MODE" = "extended" ]; then
    echo "- **Batch Size:** $BATCH_SIZE" >> "$SUMMARY_FILE"
fi

cat >> "$SUMMARY_FILE" << EOF

## Test Scenarios

The benchmark ran with the following record counts:
EOF

for RECORDS in "${RECORD_COUNTS[@]}"; do
    echo "- ${RECORDS} records" >> "$SUMMARY_FILE"
done

cat >> "$SUMMARY_FILE" << EOF

## Results

### Time-Series Metrics

For each test scenario, time-series metrics were collected using \`dotnet-counters\`:

- **GC Heap Size** - Memory allocated by the GC over time
- **Working Set** - Total physical memory used by the process
- **Allocation Rate** - Rate of memory allocation (MB/sec)
- **GC Collections** - Number of Gen 0, 1, 2 collections
- **ThreadPool Threads** - Number of active ThreadPool threads

EOF

# Link to charts if they exist
if [ -z "$SKIP_VISUALIZATION" ]; then
    cat >> "$SUMMARY_FILE" << EOF
### Visualizations

EOF
    for RECORDS in "${RECORD_COUNTS[@]}"; do
        CHART_FILE="charts_${RECORDS}rec/comparison_metrics.png"
        if [ -f "$OUTPUT_DIR/$CHART_FILE" ]; then
            echo "#### ${RECORDS} Records" >> "$SUMMARY_FILE"
            echo "" >> "$SUMMARY_FILE"
            echo "![${RECORDS} Records Comparison](./$CHART_FILE)" >> "$SUMMARY_FILE"
            echo "" >> "$SUMMARY_FILE"
        fi
    done
fi

cat >> "$SUMMARY_FILE" << EOF

## Analysis

This benchmark addresses the memory measurement anomalies observed in previous benchmarks 
by using time-series data collection with \`dotnet-counters\` instead of static GC snapshots.

### Key Improvements

1. **Continuous Monitoring** - Metrics are sampled continuously during execution
2. **No Manual GC** - Avoids artifacts from manual GC collection
3. **Peak Detection** - Captures peak memory usage, not just final state
4. **GC Insights** - Shows GC collection frequency and pressure
5. **ThreadPool Activity** - Reveals concurrency patterns

### Interpreting Results

- **Memory trends should be monotonic** - Higher loads should use more memory
- **GC frequency indicates pressure** - More collections suggest memory pressure
- **Allocation rate shows throughput** - Higher rates indicate more work
- **ThreadPool growth shows concurrency** - Should scale with workload

## Raw Data

CSV files with detailed time-series data:
EOF

for RECORDS in "${RECORD_COUNTS[@]}"; do
    CSV_PATTERN="${MODE}_${RECORDS}rec_${TIMESTAMP}.csv"
    echo "- \`$CSV_PATTERN\`" >> "$SUMMARY_FILE"
done

echo ""
echo "=============================================================================="
echo "Benchmark completed successfully!"
echo "=============================================================================="
echo ""
echo "Summary report: $SUMMARY_FILE"
echo "Output directory: $OUTPUT_DIR"
echo ""
