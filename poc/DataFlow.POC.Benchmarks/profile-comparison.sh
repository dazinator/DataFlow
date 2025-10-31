#!/bin/bash
# Script to profile POC comparison benchmark with dotnet-counters
# This collects time-series metrics for POC vs Non-POC comparison
# Usage: ./profile-comparison.sh [recordCount] [maxConcurrency] [iterations] [mode]
#   mode: "simple" (default) or "extended"

set -e

# Parse arguments
RECORD_COUNT="${1:-10000}"
MAX_CONCURRENCY="${2:-4}"
ITERATIONS="${3:-3}"
MODE="${4:-simple}"
BATCH_SIZE="${5:-100}"

# Determine which command to run
if [ "$MODE" = "simple" ]; then
    BENCHMARK_COMMAND="direct-simple $RECORD_COUNT $MAX_CONCURRENCY $ITERATIONS"
    OUTPUT_PREFIX="simple_comparison"
else
    BENCHMARK_COMMAND="direct-extended $RECORD_COUNT $MAX_CONCURRENCY $BATCH_SIZE $ITERATIONS"
    OUTPUT_PREFIX="extended_comparison"
fi

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
OUTPUT_DIR="./BenchmarkDotNet.Artifacts/profiling"
OUTPUT_FILE="${OUTPUT_DIR}/${OUTPUT_PREFIX}_${TIMESTAMP}.csv"

echo "=============================================================================="
echo "POC Comparison Profiling with dotnet-counters"
echo "=============================================================================="
echo ""
echo "Configuration:"
echo "  Mode:         $MODE"
echo "  Records:      $RECORD_COUNT"
echo "  Concurrency:  $MAX_CONCURRENCY"
if [ "$MODE" = "extended" ]; then
    echo "  Batch Size:   $BATCH_SIZE"
fi
echo "  Iterations:   $ITERATIONS"
echo "  Output:       $OUTPUT_FILE"
echo ""

# Install dotnet-counters if not present
if ! command -v dotnet-counters &> /dev/null; then
    echo "Installing dotnet-counters..."
    dotnet tool install -g dotnet-counters
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Build the benchmark project in release mode
echo "Building benchmark project..."
REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$REPO_ROOT"
dotnet build -c Release poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj --no-restore

# Start the benchmark in the background
echo ""
echo "Starting benchmark..."
dotnet run --project poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj --no-build -c Release -- $BENCHMARK_COMMAND &
BENCHMARK_PID=$!

# Wait a moment for the process to start
sleep 2

# Check if process is still running
if ! kill -0 $BENCHMARK_PID 2>/dev/null; then
    echo "ERROR: Benchmark process failed to start or completed too quickly"
    exit 1
fi

echo "Benchmark started (PID: $BENCHMARK_PID)"
echo ""
echo "Collecting counters with dotnet-counters..."
echo "This will run until the benchmark completes..."
echo ""

# Collect counters
dotnet-counters collect \
    --process-id $BENCHMARK_PID \
    --output "$OUTPUT_FILE" \
    --format csv \
    --counters System.Runtime

echo ""
echo "=============================================================================="
echo "Profiling completed"
echo "=============================================================================="
echo ""
echo "Output file: $OUTPUT_FILE"
echo ""
echo "You can analyze the CSV file with Python, Excel, or other tools."
echo "To visualize the results, use the visualization script:"
echo "  python3 .github/scripts/visualize-counters.py $OUTPUT_FILE"
echo ""
