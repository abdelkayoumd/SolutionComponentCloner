# Solution Component Cloner

An [XrmToolBox](https://www.xrmtoolbox.com/) plugin that copies components from one
Dataverse / Dynamics 365 solution into another.

## What it does

1. Connect to an organization (via the standard XrmToolBox connection dialog).
2. Pick a **source** solution and a **target** (unmanaged) solution.
3. The grid lists every component in the source solution as a tree, like the Power Apps solution
   explorer: components are grouped by type, each table holds its columns, relationships, keys,
   forms, views, charts, dashboards and business rules, and web resources are split into Code,
   Data and Images. Folders have tri-state checkboxes, so one click selects everything inside.
4. Check the components you want to copy.
5. Toggle **Include required components**:
   - **Checked** — each selected component is added to the target solution along with
     everything Dataverse considers a required dependency (e.g. adding a form also pulls
     in the entity, the view it depends on, etc.), the same behavior as `AddRequiredComponents = true`
     on `AddSolutionComponentRequest`.
   - **Unchecked** — only the exact components you selected are added, nothing else.
6. Click **Copy selected → target**. Results (success/failure per component) are listed at
   the bottom.

## Requirements

- XrmToolBox (latest)
- .NET Framework 4.8 (the current XrmToolBox plugin target)
- To build from the command line: the .NET SDK. The `.slnx` solution file needs the .NET 9+ SDK
  (or Visual Studio 2022 17.13+); with an older SDK, build the `.csproj` directly as shown below.

## Building

```bash
dotnet build SolutionComponentCloner/SolutionComponentCloner.csproj -c Release
```

## Running the tests

```bash
dotnet test SolutionComponentCloner.Tests/SolutionComponentCloner.Tests.csproj
```

The tests use a hand-written fake `IOrganizationService`, so no Dataverse connection is needed.

## Publishing to the XrmToolBox Tool Library

Follows the [official guide](https://www.xrmtoolbox.com/documentation/for-developers/deploy-your-plugin-in-plugins-store/).

1. Bump the version in **both** `Version` in `SolutionComponentCloner.csproj` and
   `AssemblyVersion`/`AssemblyFileVersion` in `Properties/AssemblyInfo.cs` (they must match, or users
   get false "update available" prompts), and update `<releaseNotes>` in
   `SolutionComponentCloner.nuspec`.
2. `dotnet pack SolutionComponentCloner/SolutionComponentCloner.csproj -c Release -o nupkg`
   (CI also uploads the package as a build artifact).
3. Push the `.nupkg` to nuget.org (needs your account and an API key).
4. Wait for nuget.org to index it, then register the tool at
   <https://www.xrmtoolbox.com/plugins/new/> using the NuGet package id `SolutionComponentCloner`.
   An administrator reviews it, which can take several days.

The nuspec must keep the `XrmToolBox` tag, the `XrmToolBox` dependency, the icon and project URLs
(the repository must be public for those links to resolve), and the DLL under `lib/net48/Plugins/`.

## Installing into XrmToolBox

Every build (Debug or Release) automatically copies `SolutionComponentCloner.dll` and its
`.pdb` into `%AppData%\MscrmTools\XrmToolBox\Plugins\` via an MSBuild post-build target — no
manual copy step needed. Just build, then (re)start XrmToolBox and the tool appears as
**Solution Component Cloner**.

If you ever need to do it by hand: copy
`SolutionComponentCloner\bin\Release\SolutionComponentCloner.dll` (and the matching `.pdb`)
into that same Plugins folder.

## Debugging (F5 in Visual Studio)

`SolutionComponentCloner.csproj.user` (gitignored, machine-specific) points the debugger at
a local XrmToolBox install and passes `/plugin:"Solution Component Cloner"` so it jumps
straight to this tool instead of the tool list. If you clone this repo on a different
machine, create/edit that file yourself:

```xml
<Project>
  <PropertyGroup>
    <StartAction>Program</StartAction>
    <StartProgram>C:\path\to\your\XrmToolBox.exe</StartProgram>
    <StartArguments>/plugin:"Solution Component Cloner"</StartArguments>
    <StartWorkingDirectory>C:\path\to\your\XrmToolBox</StartWorkingDirectory>
  </PropertyGroup>
</Project>
```

Set a breakpoint, hit F5, connect to an org, and step through as normal. Make sure your
local XrmToolBox's `XrmToolBox.Extensibility.dll` version matches the `XrmToolBoxPackage`
version in the `.csproj` — a mismatch there is the most common source of confusing runtime
errors when debugging a plugin.

## Project layout

```
SolutionComponentCloner/
  Plugin.cs                     Plugin entry point (MEF export + metadata/icons)
  UI/PluginControl.cs           Main tool UI (WinForms, built in code)
  UI/Theme.cs                   Shared colors/fonts
  Services/DataverseSolutionService.cs   Solution + component queries, copy logic
  Services/MetadataNameCache.cs          Lazy metadata cache for entity/attribute/etc. names
  Services/ComponentTypeCatalog.cs       componenttype -> friendly name / table mapping
  Models/                        Plain data models used by the UI and services
  Resources/PluginImages.cs      Base64 plugin icons
```

## Notes on component type coverage

The component grid resolves friendly names for the most common component types (forms,
views, web resources, plugin steps, workflows, entities, attributes, relationships, option
sets, etc.). Less common types still show up and can still be copied — they just fall back
to showing their raw type number and record id if a friendly-name lookup isn't implemented.

Copying itself doesn't depend on name resolution: it calls `AddSolutionComponentRequest`
with the component's real id and type, so unrecognized types copy correctly too.
