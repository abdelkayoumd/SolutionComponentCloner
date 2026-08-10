# Solution Component Cloner

An [XrmToolBox](https://www.xrmtoolbox.com/) plugin that copies components from one
Dataverse / Dynamics 365 solution into another.

## What it does

1. Connect to an organization (via the standard XrmToolBox connection dialog).
2. Pick a **source** solution and a **target** (unmanaged) solution.
3. The grid lists every component in the source solution, grouped by type.
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

## Building

```bash
dotnet build SolutionComponentCloner.sln -c Release
```

## Installing into XrmToolBox

1. Build in `Release` configuration (above).
2. Copy `SolutionComponentCloner\bin\Release\SolutionComponentCloner.dll` (and the matching
   `.pdb`) into your XrmToolBox plugins folder, typically:
   `%AppData%\MscrmTools\XrmToolBox\Plugins\`
3. Restart XrmToolBox. The tool appears as **Solution Component Cloner**.

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
