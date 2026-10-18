# Solution Component Cloner

An [XrmToolBox](https://www.xrmtoolbox.com/) tool that copies components from one Dataverse / Dynamics 365
solution into another, browsing the source solution the way the Power Apps solution explorer does.

![Choose a source and a target solution](https://raw.githubusercontent.com/abdelkayoumd/SolutionComponentCloner/main/docs/screenshots/1-select-solutions.png)

## Features

- **Solution explorer tree** – the source solution's components are grouped by type. Tables hold their
  columns, relationships, keys, forms, views, charts, dashboards and business rules, and web resources are
  split into Code, Data and Images. Folders show counts, such as "Web resources (19)".
- **Select exactly what you need** – every folder has a tri-state checkbox, so one click selects everything
  inside it. Large solutions open collapsed so every type fits on one screen.
- **Search, sort and filter** – a search box narrows the tree as you type (Ctrl+F), the Modified, Modified by
  and State columns are sortable, and *Select modified since...* ticks everything changed after a date.
- **See what the target already has** – components already in the target solution are marked, can be hidden,
  and are counted in the footer, so you don't copy things twice.
- **Include required components** – optionally add each component's dependencies too, or copy only what you
  ticked. *Preview* lists exactly which dependencies would come along, and each table can override the setting
  from its right-click menu (include all subcomponents, or only the ones you ticked).
- **Safe, visible copying** – only unmanaged solutions are accepted as a target. Copying runs in batches with a
  live "n of m" count and a Cancel button. Tables are copied before their columns, forms and views, and
  Model-driven apps carry a warning because they always pull in everything they are built from.
- **Clear results** – a banner summarises how many components were copied or failed, with a per-component
  list one click away. Failures can be exported to CSV.
- **Everything around the copy** – *Publish all customizations* afterwards, *New target solution...* without
  leaving the tool, and *Save selection / Load selection* to repeat the same copy on another environment.
- **Remembers your choices** – the last source and target per connection and the *Include required* setting.
  Solutions are listed newest first, with their creation date.
- **Readable names everywhere** – including custom and ISV component types, which are resolved through
  `solutioncomponentdefinition`. Solutions and components with thousands of rows are loaded in full.

## Screenshots

**Browse the tree and tick what to copy**

![Components grouped by type, with a table expanded](https://raw.githubusercontent.com/abdelkayoumd/SolutionComponentCloner/main/docs/screenshots/2-components-tree.png)

**Review the results**

![Copy results with a failure banner and details](https://raw.githubusercontent.com/abdelkayoumd/SolutionComponentCloner/main/docs/screenshots/3-copy-results.png)

## Install

- From the XrmToolBox Tool Library: search for **Solution Component Cloner**.
- Or download the latest DLL from the [Releases](https://github.com/abdelkayoumd/SolutionComponentCloner/releases)
  page and copy it into XrmToolBox's `Plugins` folder.

## License

[MIT](https://github.com/abdelkayoumd/SolutionComponentCloner/blob/main/LICENSE) © 2026 abdelkayoumd
