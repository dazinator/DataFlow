# GitHub Actions Workflows - Implementation Summary

This document provides an overview of the GitHub Actions workflows implemented for the DataFlow library.

## Workflows Created

### 1. CI/CD Pipeline (`.github/workflows/ci-cd.yml`)

**Trigger:** Automatically runs on every push to the `develop` branch

**Purpose:** Continuous integration and deployment of NuGet packages

**Jobs:**

#### build-and-test
- Checks out code with full git history (for GitVersion)
- Sets up .NET 8.0
- Installs and runs GitVersion to determine semantic version
- Restores dependencies
- Builds the solution in Release configuration with versioning
- Runs tests filtered to `Category=UnitTest` and `Category=IntegrationTest` (using XUnit.Categories attributes)
- Publishes test results using dorny/test-reporter

#### publish-packages
- Depends on successful `build-and-test` job
- Has write permissions to GitHub Packages
- Rebuilds the solution with versioning
- Packs `Uniun.DataFlow` NuGet package
- Packs `Uniun.DataFlow.OpenTelemetry` NuGet package
- Publishes both packages to GitHub Packages feed
- Uses `--skip-duplicate` to avoid errors on duplicate versions

**Configuration:**
- Uses GitVersion with the existing `GitVersion.yml` configuration
- Packages are published to GitHub Packages feed for the repository owner: `https://nuget.pkg.github.com/{repository_owner}/index.json`
  - For the uniun-technology organization: `https://nuget.pkg.github.com/uniun-technology/index.json`

### 2. Benchmarks Workflow (`.github/workflows/benchmarks.yml`)

**Trigger:** Manual workflow dispatch from GitHub Actions UI

**Purpose:** Run performance benchmarks and track results over time

**Input Parameter:**
- `benchmark`: Choice of which benchmark to run
  - **Non-POC Options:** `all`, `diagnose`, `minimal`, `simple`, `batch`, `transform-model`, `transform-memory`, `memory-rate`
  - **POC Options:** `poc-simple`, `poc-extended`, `poc-all`, `poc-comparison-simple`, `poc-comparison-extended`, `poc-actor-steady`, `poc-actor-rotation`, `poc-actor-memory`, `poc-actor-all`, `poc-epoch-production-io`, `poc-epoch-all`
  - Default: `simple`

**Job: run-benchmarks**
- Checks out code with full git history
- Sets up .NET 8.0 and Python (for visualization)
- Installs dependencies (matplotlib, pandas, dotnet-counters)
- Restores and builds both Benchmarks and POC Benchmarks projects in Release mode
- Creates result directories if needed
- Runs selected benchmark(s)
  - Non-POC benchmarks: Output to `docs/benchmarks/*.txt`
  - POC benchmarks: Output to `poc/DataFlow.POC.Benchmarks/benchmark-results/*.md` and BenchmarkDotNet artifacts
- Uploads benchmark results as GitHub Actions artifacts (90-day retention)
  - Includes BenchmarkDotNet.Artifacts (markdown, CSV, HTML reports)
  - Includes time-series metrics and visualizations
- Commits results to repository
- Pushes changes back to the triggering branch

**Features:**
- Proper error handling for missing benchmark results
- Conditional commit/push (only if files were created)
- Results stored both as artifacts and in repository for historical tracking
- Allows comparing performance over time via git history
- POC epoch benchmarks are prefixed with `poc-` for easy identification

### 3. Prompt Architecture Validation (`.github/workflows/validate-prompt-architecture.yml`)

**Trigger:** 
- Pull requests that modify files in `.team/**`, `.github/copilot-instructions.md`, or `docs/design/prompt-engineering/**`
- Pushes to `main` or `develop` branches with the same path filters

**Purpose:** Automated validation of the prompt/workflow architecture to ensure integrity and prevent leaks

**Job: validate-prompt-architecture**
- Makes all validation scripts executable
- Runs four validation checks in sequence:

#### Check for Kernel Leaks
- Runs `.team/scripts/check-kernel-leaks.sh`
- Detects platform-specific operations (GitHub API calls, labels) outside the kernel layer
- Ensures procedures and duties use semantic operations only
- Fails PR if leaks are detected

#### Check for Dependency Leaks
- Runs `.team/scripts/check-dependency-leaks.sh`
- Detects content duplication across the dependency graph
- Ensures single source of truth is maintained
- Fails PR if leaks are detected

#### Validate Graph Integrity
- Runs `.team/scripts/validate-graph.sh`
- Validates `.team/model-graph.yaml` structure
- Checks for cycles, orphaned nodes, missing files
- Ensures all dependencies are valid
- Fails PR if graph is invalid

#### Check for Graph Drift
- Runs `.team/scripts/check-graph-drift.sh`
- Detects drift between graph and actual file structure
- Identifies missing files referenced in graph (errors)
- Identifies undocumented files not in graph (warnings)
- Fails PR only on errors, warnings don't block merge

**Features:**
- All check results are displayed in the GitHub Actions summary
- Detailed logs for each validation are shown
- Summary table at the end shows status of all checks
- Provides safety net if reviewer or agent forgets to run validations
- Prevents merging PRs with architecture violations

**Required Status Checks:**
To make this workflow block PR merging, add "validate-prompt-architecture" as a required status check in repository settings:
1. Go to Settings → Branches → Branch protection rules
2. Select rule for main/develop branch
3. Enable "Require status checks to pass before merging"
4. Search for and add "validate-prompt-architecture"

## Benchmark Results Storage

Benchmark results are stored in multiple locations:
- Non-POC benchmarks: `docs/benchmarks/` (timestamped .txt files)
- POC benchmarks: `poc/DataFlow.POC.Benchmarks/benchmark-results/` (timestamped .md files)
- BenchmarkDotNet results: `poc/DataFlow.POC.Benchmarks/BenchmarkDotNet.Artifacts/results/` (.md, .csv, .html files)
- Files are committed to the repository for historical tracking
- Results are also available as downloadable artifacts in the workflow run

## Usage

### Consuming NuGet Packages

To use the packages published by the CI/CD workflow, add the GitHub Packages feed to your `nuget.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/uniun-technology/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_GITHUB_PAT" />
    </github>
  </packageSourceCredentials>
</configuration>
```

Note: You'll need a GitHub Personal Access Token (PAT) with `read:packages` scope.

### Running Benchmarks

1. Go to the Actions tab in the GitHub repository
2. Select "Benchmarks" workflow from the workflow list
3. Click "Run workflow" button
4. Select the benchmark to run from the dropdown
5. Click the green "Run workflow" button to start

Results will be:
- Committed to `docs/benchmarks/` in the branch
- Available as artifacts in the workflow run (under "Artifacts" section)

### Viewing Benchmark History

```bash
# See all benchmark result files
git log --oneline -- docs/benchmarks/

# View a specific historical result
git show <commit-hash>:docs/benchmarks/<filename>

# Compare two benchmark results
diff <(git show commit1:docs/benchmarks/simple_timestamp1.txt) \
     <(git show commit2:docs/benchmarks/simple_timestamp2.txt)
```

## Permissions

The workflows require the following permissions:

### CI/CD Pipeline
- `contents: read` - To checkout code
- `packages: write` - To publish NuGet packages

### Benchmarks
- `contents: write` - To commit and push benchmark results

These are configured via the `permissions` key in each workflow.

## Environment Variables

Both workflows use these environment variables:
- `DOTNET_VERSION: '8.0.x'` - .NET SDK version to use
- Specific to each workflow for paths to solution/project files

## Testing Workflow Changes

To test workflow changes:

1. Make changes to workflow YAML files
2. Commit and push to a feature branch
3. For CI/CD: Push to develop to trigger automatically
4. For Benchmarks: Run manually from Actions tab

## Future Enhancements

Potential improvements:
- Add code coverage reporting to CI/CD
- Add benchmark comparison reports (comparing to baseline or previous run)
- Add workflow for publishing to nuget.org (not just GitHub Packages)
- Add workflow for generating release notes
- Add workflow for building documentation and deploying to gh-pages
