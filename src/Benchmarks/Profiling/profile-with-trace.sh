#!/bin/bash
# Example script for profiling the ETL benchmark with dotnet-trace
# This demonstrates how to capture CPU profiling data for flame graph analysis
#
# Usage from repository root: ./src/Benchmarks/Profiling/profile-with-trace.sh [record_count] [iterations]
# Usage from Benchmarks dir:  ./Profiling/profile-with-trace.sh [record_count] [iterations]

set -e

echo "======================================"
echo "DataFlow Profiling with dotnet-trace"
echo "======================================"
echo ""

# Check if dotnet-trace is installed
if ! command -v dotnet-trace &> /dev/null; then
    echo "dotnet-trace not found. Installing..."
    dotnet tool install -g dotnet-trace
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# Configuration
RECORD_COUNT=${1:-50000}
ITERATIONS=${2:-1}
OUTPUT_DIR="BenchmarkDotNet.Artifacts/profiling"

echo "Configuration:"
echo "  Record Count: $RECORD_COUNT"
echo "  Iterations: $ITERATIONS"
echo "  Output Directory: $OUTPUT_DIR"
echo ""

# Determine the correct directory to run from
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BENCHMARKS_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Check if we're in the Benchmarks directory or need to navigate there
if [ ! -f "Benchmarks.csproj" ]; then
    if [ -f "$BENCHMARKS_DIR/Benchmarks.csproj" ]; then
        echo "Navigating to Benchmarks directory: $BENCHMARKS_DIR"
        cd "$BENCHMARKS_DIR"
    else
        echo "Error: Could not find Benchmarks.csproj"
        echo "Please run this script from the src/Benchmarks directory or repository root"
        exit 1
    fi
fi

# Build the benchmarks
echo "Building benchmarks in Release mode..."
dotnet build Benchmarks.csproj -c Release --no-incremental

echo ""
echo "Starting profiling run..."
echo "The benchmark will run in the background while dotnet-trace collects data."
echo ""

# Start the benchmark in the background
dotnet run -c Release --no-build -- etl-direct $RECORD_COUNT $ITERATIONS &
BENCHMARK_PID=$!

# Give it a moment to start
sleep 2

# Find the actual .NET process
echo "Looking for benchmark process..."
MAX_RETRY=10
DOTNET_PID=""

for i in $(seq 1 $MAX_RETRY); do
    # Try to find Benchmarks process
    DOTNET_PID=$(pgrep -f "etl-direct" 2>/dev/null | head -1)
    
    if [ -z "$DOTNET_PID" ]; then
        # Fallback: look for child processes
        DOTNET_PID=$(pgrep -P $BENCHMARK_PID 2>/dev/null | head -1)
    fi
    
    if [ -n "$DOTNET_PID" ] && ps -p $DOTNET_PID > /dev/null 2>&1; then
        echo "Found benchmark process (PID: $DOTNET_PID)"
        break
    fi
    
    echo "Waiting for process to start (attempt $i/$MAX_RETRY)..."
    sleep 1
    
    if [ $i -eq $MAX_RETRY ]; then
        echo "Error: Could not find benchmark process"
        echo "The benchmark may have started too quickly or failed to start"
        echo "Try running manually: dotnet run -c Release -- etl-direct $RECORD_COUNT $ITERATIONS"
        kill $BENCHMARK_PID 2>/dev/null || true
        exit 1
    fi
done

echo "Collecting trace from PID: $DOTNET_PID"
echo ""

# Collect trace
TRACE_FILE="$OUTPUT_DIR/etl_trace_$(date +%Y%m%d_%H%M%S).nettrace"
mkdir -p "$OUTPUT_DIR"

dotnet-trace collect \
    --process-id $DOTNET_PID \
    --profile cpu-sampling \
    --output "$TRACE_FILE" \
    --format speedscope

# Wait for benchmark to complete
wait $BENCHMARK_PID

echo ""
echo "======================================"
echo "Profiling complete!"
echo "======================================"
echo ""
echo "Trace file saved to: $TRACE_FILE"
echo ""
echo "To view the flame graph:"
echo "  1. Open https://www.speedscope.app in your browser"
echo "  2. Drag and drop the .speedscope.json file"
echo ""
echo "Or convert to speedscope format if needed:"
echo "  dotnet-trace convert $TRACE_FILE --format speedscope"
echo ""
