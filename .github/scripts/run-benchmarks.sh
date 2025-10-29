#!/bin/bash
set -e

# Script to run benchmarks for DataFlow project
# Usage: ./run-benchmarks.sh <benchmark-type> <benchmark-project> <poc-benchmark-project> <results-dir> <poc-results-dir>

BENCHMARK="$1"
BENCHMARK_PROJECT="$2"
POC_BENCHMARK_PROJECT="$3"
RESULTS_DIR="$4"
POC_RESULTS_DIR="$5"

TIMESTAMP=$(date +"%Y-%m-%d_%H-%M-%S")

echo "Running benchmark: $BENCHMARK"
echo "Timestamp: $TIMESTAMP"

if [ "$BENCHMARK" = "all" ]; then
  # Run all non-POC benchmarks sequentially
  for bench in minimal simple batch transform-model transform-memory memory-rate; do
    echo "Running $bench benchmark..."
    dotnet run --project "$BENCHMARK_PROJECT" --configuration Release -- "$bench" > "${RESULTS_DIR}/${bench}_${TIMESTAMP}.txt" 2>&1 || true
  done
  
elif [ "$BENCHMARK" = "poc-simple" ]; then
  # Run POC simple benchmark
  echo "Running POC simple benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- simple > "${POC_RESULTS_DIR}/simple-benchmark_${TIMESTAMP}.md" 2>&1
  
elif [ "$BENCHMARK" = "poc-extended" ]; then
  # Run POC extended benchmark
  echo "Running POC extended benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- extended > "${POC_RESULTS_DIR}/extended-benchmark_${TIMESTAMP}.md" 2>&1
  
elif [ "$BENCHMARK" = "poc-all" ]; then
  # Run all POC benchmarks
  echo "Running POC simple benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- simple > "${POC_RESULTS_DIR}/simple-benchmark_${TIMESTAMP}.md" 2>&1 || true
  echo "Running POC extended benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- extended > "${POC_RESULTS_DIR}/extended-benchmark_${TIMESTAMP}.md" 2>&1 || true
  
else
  # Run selected non-POC benchmark
  echo "Running $BENCHMARK benchmark..."
  dotnet run --project "$BENCHMARK_PROJECT" --configuration Release -- "$BENCHMARK" > "${RESULTS_DIR}/${BENCHMARK}_${TIMESTAMP}.txt" 2>&1
fi

echo "Benchmark run completed successfully"
