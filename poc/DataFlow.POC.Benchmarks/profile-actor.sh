#!/bin/bash
# Script to profile ActorBlock benchmarks with dotnet-counters
# This collects time-series metrics for steady-state vs rotation comparison
# Usage: ./profile-actor.sh [mode] [itemCount] [rotateAfter]
#   mode: "steady", "rotation", or "memory" (default: "rotation")
#   itemCount: Number of items to process (default: 10000)
#   rotateAfter: Rotation frequency for rotation/memory modes (default: 100 for rotation, 50 for memory)

set -e

# Parse arguments
MODE="${1:-rotation}"
ITEM_COUNT="${2:-10000}"
ROTATE_AFTER="${3}"

# Set defaults based on mode
if [ "$MODE" = "steady" ]; then
    BENCHMARK_COMMAND="actor-steady $ITEM_COUNT"
    OUTPUT_PREFIX="actor_steady"
elif [ "$MODE" = "memory" ]; then
    ROTATE_AFTER="${ROTATE_AFTER:-50}"
    BENCHMARK_COMMAND="actor-memory $ITEM_COUNT $ROTATE_AFTER"
    OUTPUT_PREFIX="actor_memory"
else
    # Default to rotation mode
    ROTATE_AFTER="${ROTATE_AFTER:-100}"
    BENCHMARK_COMMAND="actor-rotation $ITEM_COUNT $ROTATE_AFTER"
    OUTPUT_PREFIX="actor_rotation"
fi

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
OUTPUT_DIR="./benchmark-results"
OUTPUT_FILE="${OUTPUT_DIR}/${OUTPUT_PREFIX}_${TIMESTAMP}.csv"

echo "=============================================================================="
echo "ActorBlock Profiling with dotnet-counters"
echo "=============================================================================="
echo ""
echo "Configuration:"
echo "  Mode:         $MODE"
echo "  Items:        $ITEM_COUNT"
if [ "$MODE" != "steady" ]; then
    echo "  Rotate After: $ROTATE_AFTER items"
fi
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
echo "  python3 ../../.github/scripts/visualize-counters.py $OUTPUT_FILE --output ${OUTPUT_DIR}/charts_${OUTPUT_PREFIX}_${TIMESTAMP}"
echo ""
