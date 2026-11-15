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
- Stack: C# / .NET 8 with [Avalonia UI](https://avaloniaui.net/). The deliverable is a standalone executable
  (self-contained, single file), no JAR and no runtime installation required for the user.
- Project: `src/MediaRenamer/MediaRenamer.csproj`.
- Build: `dotnet build src/MediaRenamer`. Run: `dotnet run --project src/MediaRenamer -- <folder> [<folder> ...]`.
- Release build, e.g. Windows: `dotnet publish src/MediaRenamer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
  (other runtime IDs: `linux-x64`, `osx-x64`, `osx-arm64`).
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
