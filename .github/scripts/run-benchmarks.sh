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
  # Run POC simple benchmark (old approach)
  echo "Running POC simple benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- simple > "${POC_RESULTS_DIR}/simple-benchmark_${TIMESTAMP}.md" 2>&1
  
elif [ "$BENCHMARK" = "poc-extended" ]; then
  # Run POC extended benchmark (old approach)
  echo "Running POC extended benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- extended > "${POC_RESULTS_DIR}/extended-benchmark_${TIMESTAMP}.md" 2>&1

elif [ "$BENCHMARK" = "poc-comparison-simple" ]; then
  # Run POC comparison with dotnet-counters (simple pipeline)
  echo "Running POC comparison benchmark (simple) with dotnet-counters..."
  chmod +x .github/scripts/run-poc-comparison.sh
  .github/scripts/run-poc-comparison.sh simple "${POC_RESULTS_DIR}"

elif [ "$BENCHMARK" = "poc-comparison-extended" ]; then
  # Run POC comparison with dotnet-counters (extended pipeline)
  echo "Running POC comparison benchmark (extended) with dotnet-counters..."
  chmod +x .github/scripts/run-poc-comparison.sh
  .github/scripts/run-poc-comparison.sh extended "${POC_RESULTS_DIR}"

elif [ "$BENCHMARK" = "poc-actor-steady" ]; then
  # Run ActorBlock steady-state benchmark with dotnet-counters
  echo "Running ActorBlock steady-state benchmark with dotnet-counters..."
  chmod +x .github/scripts/run-actor-benchmark.sh
  .github/scripts/run-actor-benchmark.sh steady "${POC_RESULTS_DIR}"

elif [ "$BENCHMARK" = "poc-actor-rotation" ]; then
  # Run ActorBlock rotation benchmark with dotnet-counters
  echo "Running ActorBlock rotation benchmark with dotnet-counters..."
  chmod +x .github/scripts/run-actor-benchmark.sh
  .github/scripts/run-actor-benchmark.sh rotation "${POC_RESULTS_DIR}"

elif [ "$BENCHMARK" = "poc-actor-memory" ]; then
  # Run ActorBlock memory-intensive benchmark with dotnet-counters
  echo "Running ActorBlock memory-intensive benchmark with dotnet-counters..."
  chmod +x .github/scripts/run-actor-benchmark.sh
  .github/scripts/run-actor-benchmark.sh memory "${POC_RESULTS_DIR}"

elif [ "$BENCHMARK" = "poc-actor-all" ]; then
  # Run all ActorBlock benchmarks
  echo "Running all ActorBlock benchmarks with dotnet-counters..."
  chmod +x .github/scripts/run-actor-benchmark.sh
  .github/scripts/run-actor-benchmark.sh steady "${POC_RESULTS_DIR}"
  .github/scripts/run-actor-benchmark.sh rotation "${POC_RESULTS_DIR}"
  .github/scripts/run-actor-benchmark.sh memory "${POC_RESULTS_DIR}"
  
elif [ "$BENCHMARK" = "poc-all" ]; then
  # Run all POC benchmarks
  echo "Running POC simple benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- simple > "${POC_RESULTS_DIR}/simple-benchmark_${TIMESTAMP}.md" 2>&1 || true
  echo "Running POC extended benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- extended > "${POC_RESULTS_DIR}/extended-benchmark_${TIMESTAMP}.md" 2>&1 || true

elif [ "$BENCHMARK" = "epoch-production-io" ]; then
  # Run Epoch Production I/O benchmark (KEY BENCHMARK - validates ≤5% overhead goal in production context)
  echo "Running Epoch Production I/O benchmark..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- epoch-production-io

elif [ "$BENCHMARK" = "epoch-all" ]; then
  # Run all epoch-related benchmarks
  echo "Running all epoch benchmarks..."
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- phase4
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- epoch-production-io
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- epoch-granularity
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- epoch-scaling
  dotnet run --project "$POC_BENCHMARK_PROJECT" --configuration Release -- epoch-async-overhead
  
else
  # Run selected non-POC benchmark
  echo "Running $BENCHMARK benchmark..."
  dotnet run --project "$BENCHMARK_PROJECT" --configuration Release -- "$BENCHMARK" > "${RESULTS_DIR}/${BENCHMARK}_${TIMESTAMP}.txt" 2>&1
fi

echo "Benchmark run completed successfully"
