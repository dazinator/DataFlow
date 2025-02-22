# Template Repository

This is an opinionated template repository for a quick setup for developing .net libraries or applications.

- Versioning done with `git`

## Features

### Version Numbering

SemVer build numbers are produced using [GitVersion](https://github.com/GitTools/GitVersion)

## Build Pipelines

Build pipelines are defined for:

- GitHub Actions (primary)
- Azure Devops Pipelines (redundency)
- AppVeyor (redundency)

### Build Pipeline Features

- Will auto push formatting fixes up using `dotnet-format` without failing the build
- Will build and publish the mkdocs site (in `/docs`) to `gh-pages` branch, in a way that supports versioned docs (e.g for `develop` branch vs `master`)

## GitAttributes and EditorConfig

Set up intelligently with rules for

- `dotnet format` (so it works cross platform with less issues.)
- [VerifyTests](https://github.com/VerifyTests/Verify) - if you haven't used these you should. I now include this library in tests projects as standard.

## Source Link

All published assemblies will be SourceLinked to github thanks to `directory.props` file.

## Default Unit Tests project

With my choice of packages for a productive experience:-

  - [xunit](https://github.com/xunit/xunit)
  - [xunit.categories](https://github.com/brendanconnolly/Xunit.Categories)
  - [coverlet.collector](https://github.com/coverlet-coverage/coverlet)
  - [shouldly](https://github.com/shouldly/shouldly)
  - [testcontainers](https://github.com/testcontainers/testcontainers-dotnet)
  - [verify](https://github.com/VerifyTests/Verify)

 and a default unit test.

 ## Dual Licence

 There is a `LICENCE.md` file in the root of the repo. This is a dual licence by default, edit it to your hearts content.
 Then to include it in a published nuget pakcage add this to your csproj

 ```xml
  <PropertyGroup>
    ...
    <PackageLicenseFile>LICENSE.md</PackageLicenseFile>
    ...
  </PropertyGroup>

  <ItemGroup>
    <None Include="../../LICENSE.md" Pack="true" PackagePath="/" />
  </ItemGroup>
```

# Getting Started

- Clone this repo, then push to your own origin or use githubs "create from template" capability.
- Update `/src/global.json` to the version of the .net sdk that you require.
- run `dotnet tool restore` in root of repo (look at `./config/dotnet-tools.json` to see what those are.)
  Change the [Project-Name] token to be the name of your project, wherever it appears:
  -  `/github/workflows/dotnet.yml`
  -  `/readme.md` 
- update the `LICENCE.md` in the root of the repo and if you want to incldue it in nuget packages see the section above.
- For AppVeyor builds, update AppVeyor.yml:
    - dotnet sdk version (currently set to install latest pre-release).
    - Now you can add to AppVeyor.
- For Azure Devops builds:
    - Import pipelines yaml file into Azure Devops pipeline.
- For GitHub Actions - the workflow file is detected automatically when you push up and should be run.
  - Change the nuget feed url in `/github/workflows/dotnet.yml` as nuget packages will be pushed a `dazinator` feed.
    - In order for this to work, the GITHUB_TOKEN must have write access to the feed. If pushing to your own github nuget feed, in the github project settings, go to "Actions" > "General" and set "Workflow permissions" to "Read and Write".
  - To enable the `/docs` to be build and published to `gh-pages` - in the `dotnet.yml` change the variable named "DEPLOY_DOCS" with any truthy value.
  - 
