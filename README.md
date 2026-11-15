# MediaRenamer

Cross-platform desktop tool (Windows, Linux, macOS) that renames locally stored TV show episodes consistently,
using episode titles from [TheMovieDB](https://www.themoviedb.org/).

Built with C# / .NET and [Avalonia UI](https://avaloniaui.net/).
The behavior is specified in [docs/requirements.md](docs/requirements.md).

## Usage

The video files of a show are expected in season folders, e.g.:

```
Eureka/
  Staffel 01/          (or "Season 01", any "<word> <number>")
    S01E01.mkv
    eureka.s01e02.mkv
  Staffel 02/
    ...
  Staffel 00/          (specials: ignored, never renamed)
```

1. Start the application with the show folder, or choose the folder with "Choose folder…".
   To process only some seasons, pass one or more season folders instead.
2. The application searches the show on TheMovieDB by the folder name and proposes new names like
   `S01E02 - Many Happy Returns.mkv`. If necessary, change the search term, the selected show or the language,
   and deselect files that should not be renamed.
3. Click "Rename". Existing files are never overwritten.

Download the executable from the [releases](https://github.com/cathixx/MediaRenamer/releases), see
[Releases](#releases) for the files.

### Windows: "Send to" menu

The most convenient way on Windows is the "Send to" menu of the Explorer:

1. Press <kbd>Win</kbd>+<kbd>R</kbd>, enter `shell:sendto` and press Enter. The "SendTo" folder opens.
2. Create a shortcut to `MediaRenamer.exe` in this folder (e.g. drag the exe into the folder with the right mouse
   button and choose "Create shortcuts here"), and rename it to `MediaRenamer`.
3. In the Explorer, right-click a show folder (or select several season folders and right-click them) and choose
   "Send to" → "MediaRenamer". On Windows 11 the "Send to" menu is under "Show more options".

### Linux and macOS

The application takes the folders as command line arguments, so file managers can start it the same way, e.g. with
a script in `~/.local/share/nautilus/scripts/` (GNOME Files) or a Quick Action created with Automator (macOS
Finder) that runs `MediaRenamer "$@"`.

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
| `src/MediaRenamer` | Avalonia desktop application (views and view models) |
| `tests/MediaRenamer.Core.Tests` | Tests of the business logic (xUnit v3) |
| `tests/MediaRenamer.Tests` | Tests of the view models, headless screenshots of the windows |

## TMDB API key

The application uses [TheMovieDB](https://www.themoviedb.org/) and needs a TMDB API key (v3).
The key is embedded into the executable at build time and is taken from

- the environment variable `TMDB_API_KEY`, or
- a `.env` file in the repository root (copy `.env.example` to `.env` and fill in your key).

Without a key the build still works, but the application then requires the environment variable `TMDB_API_KEY` at
runtime. The environment variable always takes precedence, so you can also use your own key with a released executable.

Tests against the real TMDB API are not run by default; run them with `dotnet test -- --explicit only` (needs the
environment variable `TMDB_API_KEY`).

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

The executables are intentionally not code-signed. Windows SmartScreen may warn on the first start
("More info" → "Run anyway").
On macOS remove the quarantine flag after extracting: `xattr -d com.apple.quarantine MediaRenamer`.
