# MediaRenamer

Cross-platform desktop tool (Windows, Linux, macOS) that renames locally stored TV show episodes consistently,
using episode titles from [TheMovieDB](https://www.themoviedb.org/).

This is a re-implementation of a JavaFX application in C# / .NET with [Avalonia UI](https://avaloniaui.net/).
Current state: hello-world skeleton, the actual functionality is not implemented yet.

## Development

Requirement: [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0) in the version from `global.json` or a newer
feature band of .NET 11 (e.g. `winget install Microsoft.DotNet.SDK.11`).

```bash
# build
dotnet build

# run, optionally with folder paths as arguments
dotnet run --project src/MediaRenamer -- "C:\Shows\Doctor Who"

# run with a specific UI language
dotnet run --project src/MediaRenamer -- --lang=de

# run the tests
dotnet test
```

Project structure:

| Project | Content |
|---|---|
| `src/MediaRenamer.Core` | Business logic without UI: folder and season detection, episode number recognition, naming, renaming |
| `src/MediaRenamer` | Avalonia desktop application |
| `tests/MediaRenamer.Core.Tests` | Tests of the business logic (xUnit v3) |

## Localization

UI texts live in `src/MediaRenamer/Resources/Strings*.resx`: `Strings.resx` is the English default, `Strings.de.resx`
the German translation. The language is chosen at startup:

1. `--lang=<culture>` on the command line, e.g. `--lang=de` or `--lang=en` (useful for testing),
2. otherwise the UI language of the operating system,
3. if there is no translation for that language, English is used.

To add a language, copy `Strings.resx` to `Strings.<culture>.resx` (e.g. `Strings.fr.resx`) and translate the values.

## Building an executable

A single, standalone executable (no .NET runtime installation needed for the user):

```bash
dotnet publish src/MediaRenamer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Other target platforms: `-r linux-x64`, `-r osx-x64`, `-r osx-arm64`.
The result is placed in `src/MediaRenamer/bin/Release/net10.0/<runtime>/publish/`.
The executable is trimmed and compressed (about 21 MB). Trimming removes code that is only used via reflection,
so test the published executable after changes and fix trim warnings (they fail the build).

## Releases

The [Build & Release](.github/workflows/build.yml) workflow builds the executables for all platforms:

- every push to `main` (except changes to documentation and `.github/`) creates a new
  [release](https://github.com/cathixx/MediaRenamer/releases) with the next version number. The patch version is
  increased by default; `#minor` or `#major` in a commit message increases the minor or major version instead.
- pull requests are only built; the executables can be downloaded as artifacts of the workflow run.

Release assets:

| Platform | File |
|---|---|
| Windows (x64) | `MediaRenamer-win-x64.zip` |
| Linux (x64) | `MediaRenamer-linux-x64.tar.gz` |
| macOS (Apple Silicon) | `MediaRenamer-osx-arm64.tar.gz` |
| macOS (Intel) | `MediaRenamer-osx-x64.tar.gz` |

The executables are not code-signed. Windows SmartScreen may warn on the first start ("More info" → "Run anyway").
On macOS remove the quarantine flag after extracting: `xattr -d com.apple.quarantine MediaRenamer`.
