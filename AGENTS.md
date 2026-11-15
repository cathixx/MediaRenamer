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
- The legacy documentation in `../renamer/docs/funktionsumfang.md` is German. When porting behavior from it,
  translate it, do not copy German wording into the repository.

## Project

- Cross-platform desktop application (Windows, Linux, macOS) that renames TV show video files based on TheMovieDB.
  Re-implementation of the JavaFX application in `../renamer` (current behavior: `../renamer/docs/funktionsumfang.md`,
  requirements for the re-implementation: section 11 there).
- Stack: C# / .NET 10 (LTS) with [Avalonia UI](https://avaloniaui.net/). The deliverable is a standalone executable
  (self-contained, single file), no JAR and no runtime installation required for the user.
- The SDK version is pinned in `global.json`; CI uses the same version. Avalonia's source generators need a
  recent compiler, so an older SDK fails with errors like `InitializeComponent does not exist`.
- Solution: `MediaRenamer.sln`, application project: `src/MediaRenamer/MediaRenamer.csproj`.
- Build: `dotnet build`. Run: `dotnet run --project src/MediaRenamer -- <folder> [<folder> ...]`.
- Release build, e.g. Windows: `dotnet publish src/MediaRenamer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
  (other runtime IDs: `linux-x64`, `osx-x64`, `osx-arm64`).
- Publishing trims and compresses the executable (`PublishTrimmed`, `EnableCompressionInSingleFile` in the csproj).
  Trim warnings are errors: fix them (e.g. avoid reflection, use compiled bindings), never suppress them blindly.
- The result of publishing is exactly one file on every platform. Debug symbols of native libraries shipped by
  packages are removed from the publish output (target `RemovePackageDebugSymbolsFromPublish` in the csproj).
  A macOS `.app` bundle is not implemented yet.
- CI: `.github/workflows/build.yml` publishes the executable for `win-x64`, `linux-x64`, `osx-arm64` and `osx-x64`
  on pushes to `main` and on pull requests and attaches them as artifacts. There is no release workflow yet.
- Dependabot (`.github/dependabot.yml`) combines all .NET SDK, NuGet and GitHub Actions updates into one weekly pull request.
- The application is started with folder paths as command line arguments (e.g. via the Windows "Send to" menu),
  see `Program.cs`.
- In the sandbox the .NET SDK is installed in `~/.dotnet` but is not on the `PATH`:
  `export PATH=$HOME/.dotnet:$PATH`. There is no display, so the UI can be built but not started there.
- All text files use LF line endings.
- Never commit the TMDB API key and never print it.

## Git

- Changes may be **committed** without asking.
- **Push only with explicit approval**, renewed for every single push. An earlier approval does not apply to later pushes.
  This also covers force pushes, tags and anything else that changes the state on GitHub.

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
