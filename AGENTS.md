# AGENTS.md

Notes for AI agents working in this repository.

## Language

- Everything in the repository is written in **English**: code, identifiers, comments, commit messages, documentation
  and UI texts. This is an open source project that other people are meant to contribute to.
- Only the conversation with the maintainer (prompts and answers while working) is in German.
- UI texts are localized: English is the neutral language (`src/MediaRenamer/Resources/Strings.resx`), translations are
  `Strings.<culture>.resx` next to it (currently `de`). Translations are the only place where non-English text belongs.
  Never hard-code UI texts; add a key to all resx files and a property to `Resources/Strings.cs`, then use it in XAML
  via `{x:Static res:Strings.Key}` or in code via `Strings.Key`.

## Project

- Cross-platform desktop application (Windows, Linux, macOS) that renames TV show video files based on TheMovieDB.
- **Requirements: `docs/requirements.md`.** It is the reference for behavior and tests; requirement IDs (e.g. `D-5`)
  may be referenced in code and tests. Keep it up to date when behavior changes, including the open questions and
  the implementation status at the end.
- Stack: C# / .NET 10 (LTS, built with the .NET 11 SDK) with [Avalonia UI](https://avaloniaui.net/). The deliverable is a standalone executable
  (self-contained, single file), no runtime installation required for the user. The executables are intentionally
  not code-signed (decided, see `docs/requirements.md` section 9); do not add code signing.
- The SDK version is pinned in `global.json`; CI uses the same version. Avalonia's source generators need a
  recent compiler, so an older SDK fails with errors like `InitializeComponent does not exist`.
- Solution `MediaRenamer.sln` with these projects:
  - `src/MediaRenamer.Core`: business logic without UI dependency (folder and season detection, episode number
    recognition, naming, title cleaning, duplicate check, renaming). New business logic belongs here, with tests.
  - `src/MediaRenamer`: the Avalonia desktop application. `ViewModels/` contains the complete UI logic (MVVM with
    CommunityToolkit.Mvvm) and must not use Avalonia types, so it can be tested without a display; `Views/` contains
    the XAML views (compiled bindings, `x:DataType` required).
  - `tests/MediaRenamer.Core.Tests`: xUnit v3 tests of the core library; file system tests use `TempFolder`.
  - `tests/MediaRenamer.Tests`: tests of the view models, plus `ScreenshotTests`.
- Common settings are in `Directory.Build.props`, all package versions in `Directory.Packages.props`
  (central package management: `PackageReference` without `Version`).
- Build: `dotnet build`. Test: `dotnet test` (runs on Microsoft.Testing.Platform, configured in `global.json`).
  Run: `dotnet run --project src/MediaRenamer -- <folder> [<folder> ...]`.
- Release build, e.g. Windows: `dotnet publish src/MediaRenamer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
  (other runtime IDs: `linux-x64`, `osx-x64`, `osx-arm64`).
- Publishing trims and compresses the executable (`PublishTrimmed`, `EnableCompressionInSingleFile` in the csproj).
  Trim warnings are errors: fix them (e.g. avoid reflection, use compiled bindings), never suppress them blindly.
- The result of publishing is exactly one file on every platform. Debug symbols of native libraries shipped by
  packages are removed from the publish output (target `RemovePackageDebugSymbolsFromPublish` in the csproj).
  A macOS `.app` bundle is not implemented yet.
- CI: `.github/workflows/build.yml` ("Build & Release") publishes the executable for `win-x64`, `linux-x64`,
  `osx-arm64` and `osx-x64`, and runs the tests on Windows, Linux and macOS. Pull requests are only built (artifacts); pushes to `main` additionally create a tag and
  a public GitHub release with the next version number (changes only to `*.md`, `LICENSE`, `.idea/` or `.github/`
  don't create a release; use "Run workflow" to release manually).
- Self-update (requirements section 10): `MediaRenamer.Core/Updates/UpdateService.cs` downloads the release file
  `MediaRenamer-<runtime>.zip`/`.tar.gz` and expects the executable at the top level of the archive. Keep the asset
  names and the archive layout in `.github/workflows/build.yml` compatible, otherwise installed versions can no
  longer update themselves.
- Dependabot (`.github/dependabot.yml`) combines all .NET SDK, NuGet and GitHub Actions updates into one weekly pull request.
- **Application icon:** `src/MediaRenamer/Assets/icon.svg`, `icon.ico` and `icon-256.png` are generated from the
  vector description in `tests/MediaRenamer.Tests/IconGenerator.cs`; never edit them directly. After changing the icon:
  `SCREENSHOT_DIR=/tmp/shots dotnet test --project tests/MediaRenamer.Tests -- --explicit only --filter-class *IconGenerator`
  and check `/tmp/shots/icon-preview.png`.
- **Checking the UI without a display:** `ScreenshotTests` renders the windows headless (Avalonia.Headless with Skia)
  with test data and saves PNGs: `SCREENSHOT_DIR=/tmp/shots dotnet test --project tests/MediaRenamer.Tests -- --explicit only`,
  then look at the images. Do this after every visible change of the UI.
- The application is started with folder paths as command line arguments (e.g. via the Windows "Send to" menu),
  see `Program.cs`.
- In the sandbox the .NET SDK is installed in `~/.dotnet` but is not on the `PATH`:
  `export PATH=$HOME/.dotnet:$PATH`. There is no display, so the UI can only be checked with screenshots (see above).
- All text files use LF line endings.
- TMDB API key: locally in `.env` (ignored by git, template `.env.example`), in CI the repository secret
  `TMDB_API_KEY`. `src/MediaRenamer/EmbeddedTmdbApiKey.targets` embeds it at build time into the generated class
  `EmbeddedTmdbApiKey` (in `obj/`); at runtime the environment variable takes precedence. Never commit the key and
  never print it (also not from the generated file).
- Tests against the real TMDB API are marked `Explicit` and need the key in the environment:
  `set -a; . ./.env; set +a; dotnet test -- --explicit only`. The sandbox needs network access to
  `api.themoviedb.org` and `image.tmdb.org`.

## Git

- Changes may be **committed** without asking.
- **Push only with explicit approval**, renewed for every single push. An earlier approval does not apply to later pushes.
  This also covers force pushes, tags and anything else that changes the state on GitHub.
- Reason: every push to `main` automatically creates a public GitHub release via the workflow `Build & Release`.
- `#minor` or `#major` in a commit message increases the release version accordingly (default: patch).
  Only use these keywords when explicitly requested.

## Git workflow

- **Commits:** Every change may be committed directly without asking.
  Stage only the files that belong to the change (no pure line-ending changes).
- **Push:** Never push without explicit approval. The approval applies only to the
  specific push, not to later ones.
- **Amend:** If two thematically related changes are requested right after each other and the previous commit
  has not been pushed yet, fold the second change into it with `git commit --amend` instead of creating a new commit.
  Verify beforehand that the commit is really local (e.g. `git status` / `git log origin/main..HEAD`).
  Never amend commits that have already been pushed.
- **Linear history, no merge commits:** PRs are taken over by fast-forward
  (`git fetch && git merge --ff-only origin/<pr-branch>`, then push). If a fast-forward is not possible,
  rebase the PR branch first.
  Dependabot only rebases its PRs on conflicts, and for grouped multi-ecosystem PRs it did not react to
  `@dependabot rebase`. In that case merge the PR on GitHub with **"Squash and merge"**: GitHub signs that commit,
  whereas commits created by "Rebase and merge" (and "Update with rebase") are unsigned.
- **Signatures:** All commits must be signed (`commit.gpgsign=true`).
- **Setting up the GPG key in the sandbox:** In the sandbox container `commit.gpgsign=true` is set with
  `user.signingkey=D6FC8D086220A70A`, but the key is missing from the (freshly created) keyring. The private key is
  located read-only at `/c/workspace/fobi/gpg-key/cathixx_sec.gpg` (host folder mount). Import it once per sandbox,
  before the first commit:

  ```bash
  gpg --list-secret-keys D6FC8D086220A70A || gpg --batch --import /c/workspace/fobi/gpg-key/cathixx_sec.gpg
  ```

  The key has no passphrase, so no pinentry is needed. The `[ unknown]` trust warning is irrelevant for signing.
  Never write into that folder, never copy the key or put it into the repository. Afterwards verify with
  `git log --show-signature -1`.
