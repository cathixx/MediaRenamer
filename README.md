# MediaRenamer

Cross-platform desktop tool (Windows, Linux, macOS) that renames locally stored TV show episodes consistently,
using episode titles from [TheMovieDB](https://www.themoviedb.org/).

This is a re-implementation of a JavaFX application in C# / .NET with [Avalonia UI](https://avaloniaui.net/).
Current state: hello-world skeleton, the actual functionality is not implemented yet.

## Development

Requirement: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
# build
dotnet build

# run, optionally with folder paths as arguments
dotnet run --project src/MediaRenamer -- "C:\Shows\Doctor Who"

# run with a specific UI language
dotnet run --project src/MediaRenamer -- --lang=de
```

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
The result is placed in `src/MediaRenamer/bin/Release/net8.0/<runtime>/publish/`.
The executable is trimmed and compressed (about 18 MB). Trimming removes code that is only used via reflection,
so test the published executable after changes and fix trim warnings (they fail the build).
On macOS the native libraries (`*.dylib`) are placed next to the executable and have to be shipped with it.

The [Build](.github/workflows/build.yml) workflow builds the executables for all platforms on every push to `main`
and on every pull request; they can be downloaded as artifacts of the workflow run.
