# RoslynCodeGenCLI

A lightweight command-line utility for executing Roslyn incremental code generators manually.

## Description

Zajda.RoslynCodeGenCLI allows you to run Microsoft.CodeAnalysis.Generator implementations outside of the standard MSBuild process. This gives you more control over when and how source generators run, allowing for more predictable workflows.

## Installation

### As a .NET Tool (Recommended)

```powershell
dotnet tool install --global Zajda.RoslynCodeGenCLI
```

### As a .NET Tool (Local)

```powershell
dotnet publish -c Release -p:PublishSingleFile=true
dotnet pack -c Release

Copy-Item .\bin\Release\Zajda.RoslynCodeGenCLI.*.nupkg ./.nupkg

$fullPath = Resolve-Path ./.nupkg
dotnet nuget add source $fullPath --name Zajda.RoslynCodeGenCLI
dotnet new tool-manifest --force
dotnet tool install --local Zajda.RoslynCodeGenCLI
```

### Building from Source

```powershell
git clone <repository-url>
cd <repository-directory>
dotnet build
```

## Usage

```powershell
roslyncodegen <Generator.dll> <Schema.json> <OutDir> [-P:Key=Value]...
```

### Parameters

- `Generator.dll`: Path to the assembly containing the Generator implementation
- `Schema.json`: Path to the input schema file that will be passed to the generator as AdditionalText
- `OutDir`: Directory where generated files will be saved
- `-P:Key=Value`: Optional properties to pass to the generator (can specify multiple)

## Use Cases

### Control Over Generation Lifecycle

- Integrate code generation into custom build processes
- Generate code at specific points in your workflow
- Keep generated artifacts in version control
- Validate generated output before committing

### External Build Tools

- Use with non-.NET build systems
- Integrate with CI/CD pipelines that don't support MSBuild
- Create custom code generation steps in task runners

### JetBrains Rider Compatibility

This tool helps overcome known issues with JetBrains Rider's handling of Roslyn source generators:
- Avoid IDE freezes and performance issues with complex generators
- See your generated code immediately without IDE caching problems
- Generate code manually when needed rather than on each build

### Command Line Integration

- Automate code generation with scripts
- Use in Git hooks for pre-commit validation
- Integrate with other command line tools in a pipeline

## Example

```powershell
# Generate files using a custom property
roslyncodegen ./MyGenerator.dll ./schema.json ./GeneratedCode -P:Namespace=MyCompany.Project
```
