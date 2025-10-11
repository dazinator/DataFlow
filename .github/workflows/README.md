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
  - Options: `all`, `diagnose`, `minimal`, `simple`, `batch`, `transform-model`, `transform-memory`, `memory-rate`
  - Default: `simple`

**Job: run-benchmarks**
- Checks out code with full git history
- Sets up .NET 8.0
- Restores and builds the Benchmarks project in Release mode
- Creates `docs/benchmarks/` directory if needed
- Runs selected benchmark(s) with timestamped output files
  - Single benchmark: Creates one file `{benchmark}_{timestamp}.txt`
  - All benchmarks: Creates multiple files, one per benchmark type
- Uploads benchmark results as GitHub Actions artifacts (90-day retention)
- Commits results to `docs/benchmarks/` directory
- Pushes changes back to the triggering branch

**Features:**
- Proper error handling for missing benchmark results
- Conditional commit/push (only if files were created)
- Results stored both as artifacts and in repository for historical tracking
- Allows comparing performance over time via git history

## Benchmark Results Storage

Benchmark results are stored in `docs/benchmarks/`:
- Each result file is timestamped: `{benchmark-name}_{YYYYMMDD_HHMMSS}.txt`
- Files are committed to the repository for historical tracking
- A `README.md` in that directory explains the structure and how to compare results
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
