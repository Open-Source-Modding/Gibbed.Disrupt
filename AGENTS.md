# Gibbed.Disrupt

Tools for modding Disrupt-engine games (Watch Dogs series). .NET Framework 4.7.2, SDK-style csproj.

## Build

```bash
dotnet restore
dotnet build Disrupt.sln
```

Requires .NET SDK with net472 targeting pack (Windows, or via mono). CI runs on `windows-latest`.

Output goes to `bin/` (exe projects set `<OutputPath>..\..\bin\</OutputPath>`).

## Submodules

Clone with `--recurse-submodules`. Key submodules:

- `projects/Gibbed.IO`, `projects/Gibbed.ProjectData`, `projects/NDesk.Options`, `projects/XCompression` — shared libraries (all under `vs2017` branch)
- `bin/projects/Watch Dogs/files`, `bin/projects/Watch Dogs 2/files`, `bin/projects/Watch Dogs Legion/files` — game file lists

## Project structure

- `Gibbed.IO` — multi-targets `netstandard2.0;net40;net45;net472;net48`; all other exe projects target `net472` only
- `XCompression` — multi-targets `netstandard2.0;net45;net40`, requires `PlatformTarget=x86`
- Exe projects (all in `projects/`): `Gibbed.WatchDogs.*.Unpack`, `Gibbed.WatchDogs.*.Pack`, `Gibbed.WatchDogs*.RebuildFileLists`, `Gibbed.Disrupt.Convert*`, `Gibbed.Disrupt.Packing`, `Gibbed.Disrupt.BinaryObjectInfo`
- Game-specific projects are grouped in solution folders: `Watch Dogs`, `Watch Dogs 2`, `Watch Dogs Legion`
- Shared/misc libs under `Misc` solution folder: `Gibbed.IO`, `Gibbed.ProjectData`, `NDesk.Options`, `XCompression`, `Gibbed.Disrupt.FileFormats`

## Testing / linting

No test projects, linter, or formatter configured. Verify changes by building successfully.
