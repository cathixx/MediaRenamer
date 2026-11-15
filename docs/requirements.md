# Requirements

This document describes how MediaRenamer behaves. It is the reference for the implementation and the tests.
Each requirement has an ID (e.g. `D-5`) that code comments and tests may refer to.

## 1. Purpose

MediaRenamer gives locally stored video files of TV shows consistent names. The files are single episodes located in
season folders. The application determines season and episode number of every file, loads the episode titles from the
online database [TheMovieDB](https://www.themoviedb.org/) (TMDB), shows old and new file name side by side and renames
the files at the push of a button.

Target format of the file name:

```
S<season>E<episode> - <episode title>.<extension>
S<season>E<episode>.<extension>                     (no title available)
S<season>E<episode>E<episode> - <title>.<extension>  (double episode)
```

Example: `S01E02 - Pilot.mp4`

Workflow:

1. Start the application with the path of a show folder (e.g. via the Windows Explorer "Send to" menu), or choose
   the folder in the application.
2. The application lists the video files of all seasons and searches the show on TMDB by the folder name.
3. If necessary, adjust the search term, the selected show and the language, and deselect single files.
4. Click "Rename".

## 2. Start and input

The application is started with folder paths as command line arguments, typically via the "Send to" menu of the
Windows Explorer, which passes the selected folders. This explains the two modes: one selected show folder (E-1) or
one or more selected season folders (E-2).

| ID | Requirement |
|---|---|
| E-1 | **Show folder mode:** the first argument is a folder whose name does not start with a season keyword (E-2). It is the show folder. All its direct sub folders whose names match the season pattern (E-3) are the seasons. Further arguments are ignored. |
| E-2 | **Season folder mode:** the name of the first argument starts with the season keyword `Staffel` or `Season` (case-sensitive), not followed by another letter: `Staffel 01`, `Season 1`, `Staffel01`, but not `Seasons of Love`. Then all arguments are season folders, and the show folder is the parent of the first argument. This allows processing single seasons of a show. |
| E-3 | **Season pattern:** `^[a-zA-Z]+ ([0-9]+)` – a word of the letters A–Z, exactly one space, a number, then any text. The number is the season number. Matching: `Staffel 01`, `Staffel 1`, `Season 3 extras`. Not matching: `Staffel01`, `Specials`, `Zusätze 1` (umlaut). |
| E-4 | Seasons are sorted by season number. |
| E-5 | The name of the show folder is the initial search term; its absolute path is shown in the status bar. |
| E-6 | In show folder mode any word works for the season folders (e.g. `Saison 1`), in season folder mode the first folder must start with a season keyword (E-2). |
| E-7 | **Specials folder:** the folder with season number 0 (`Staffel 00`, `Staffel 0`) is a place for episodes that must not be renamed. It is ignored: its files are neither shown nor renamed. A show folder that only contains the specials folder has no seasons. |
| E-8 | **Choosing a folder:** if no path is passed, the application starts without an error and lets the user choose a folder. The folder can also be changed at any time while the application is running. A chosen folder is handled like a single argument (E-1, E-2). |
| E-9 | **Invalid arguments:** if the first argument is a file or does not exist, the application opens with a hint and the user can choose a folder (E-8). In season folder mode, arguments that are not season folders of the same show (no folder, not matching E-3, specials folder, different parent folder) are ignored and a hint names them. |
| E-10 | If no season folder is found, a hint is shown and the user can choose another folder (E-8). |
| E-11 | **Command line options** (arguments starting with `--` are never folder paths): `--lang=<culture>` sets the UI language and the start language of the episode titles (T-25), e.g. `--lang=de`, `--lang=fr-FR`; `--theme=light` or `--theme=dark` sets the theme (section 6); `--complete-update=<path>` and `--previous-process=<id>` are used internally by the self-update (UP-4). |

## 3. Video files and episode numbers

### 3.1 Video files

| ID | Requirement |
|---|---|
| D-1 | Considered are regular files directly in the season folder (not recursive) with the extensions `.avi`, `.mkv`, `.mpg`, `.mp4`, `.mov`, case-insensitive. |
| D-2 | All other files (subtitles, `.nfo`, images, ...) are ignored and never renamed. |
| D-3 | The "old file name" shown is the file name without the extension (everything before the last dot). |
| D-4 | The season number of a file is always the one of its folder, never the one in the file name. |

### 3.2 Episode number in the file name

First, the text `-x264-` is removed from the complete file name (including the extension). Then the following patterns
are tried in order, each first with the two-digit season number (`01`) and then with the one-digit one (`1`). The
first match anywhere in the name wins. `SS` is the season number of the folder, `EE` **exactly two digits**.

| Rank | Pattern | Example (season 1) | Note |
|---|---|---|---|
| 1 | `S`SS`E`EE`E`EE | `S01E02E03`, `s01e02e03` | double episode (D-9) |
| 2 | `S`SS`E`EE | `S01E02`, `s1e02` | S and E case-insensitive |
| 3 | `S`SS`x`EE | `S01x02` | |
| 4 | SS`x`EE | `01x02`, `1x02` | |
| 5 | `S`SS, any single character, `E`EE | `S01.E02`, `S01_E02`, `S01 E02` | |
| 6 | `- `SSEE` -` | `Show - 0102 - Title` | with spaces as shown |
| 7 | `Ep`EE | `Show Ep02` | |
| 8 | `-E`EE`-` | `show-E02-x` | |
| 9 | SS`-`EE | `01-02` | |

Consequences:

- The season number in the file name must match the folder: `S02E03.mkv` in `Staffel 01` is not recognized.
- Episode numbers are always two digits. One-digit numbers are not recognized (`S01E1`), of three-digit numbers the
  first two digits count (`S01E123` → 12).
- Not recognized are e.g. `Folge 02.mkv`, `E02.mkv`, `02.mkv`, `Show 0102.mkv`, `Show 102.mkv`.

| ID | Requirement |
|---|---|
| D-9 | **Double episodes:** a file containing two episodes is recognized only by the notation `SxxEyyEzz` (case-insensitive) and named with both numbers, e.g. `S01E02E03 - <title>.mkv`. Other notations like `S01E02-E03` or `S01E02-03` are not recognized as double episodes. Both episode numbers are taken (D-5, R-1). If the two episodes are separate files, they are treated as two normal episodes. |

### 3.3 Files without a recognized episode number, list order

| ID | Requirement |
|---|---|
| D-5 | Files without a recognized number get, in the order of their file names, the lowest episode number of the season folder that is still free. They fill the gaps between the recognized episodes. The order is ordinal (upper case before lower case). |
| D-6 | If two files have the same recognized episode number, both are listed with this number (see R-1). |
| D-7 | The list is sorted by season, then episode number (of a double episode: its first episode). |
| D-8 | Every row gets a consecutive number across all seasons (column `#`), counted in display order after sorting. A double episode counts once. Initially every file is selected for renaming. |

## 4. TheMovieDB

The only data source is TMDB (API v3), accessed through an interface with two operations: search a show, load the
episodes of a show.

### 4.1 Show search

| ID | Requirement |
|---|---|
| T-1 | The show is searched at startup with the folder name and afterwards on every change of the search field, 1 second after the last input. |
| T-2 | Spaces at the start and end of the search term are removed. The search starts as soon as at least one character remains, so shows like "24" or "ER" can be found. An empty search field clears the result list, the new names and the poster. |
| T-3 | The search runs in parallel in the **start language** (T-25) and in English (`en-US`), so a show is found by its local and by its international name: `GET /3/search/tv` with `query`, `language`, `include_adult=false`. Only the first result page per language (up to 20 results) is evaluated. |
| T-4 | Results whose name contains neither a letter A–Z nor a digit are discarded (e.g. purely non-Latin titles). |
| T-5 | Results of all languages are merged by TMDB ID. A show thereby has one name per language in which it was found. |
| T-6 | A year in parentheses at the end of a show name is removed: `Doctor Who (2005)` → `Doctor Who`. |
| T-7 | Taken per show: ID, names, year of first air date (from `first_air_date`), poster path, popularity. Year and poster come from the result of any of the languages. |
| T-8 | **Sorting of results:** 1. shows whose name in any language equals the search term exactly (case-insensitive) first. 2. popularity descending. 3. number of languages with a name descending. 4. year descending, shows without year first. 5. ID ascending. |
| T-9 | After every search the first result is selected automatically and its episodes are loaded. |
| T-10 | A result is displayed as its name in the selected language, otherwise its name in the first search language in which it was found, followed by ` (year)` if known. |

### 4.2 Loading episodes

Triggered by selecting a result (languages and episodes) or changing the language (episodes only).

| ID | Requirement |
|---|---|
| T-24 | **Languages of the show:** `GET /3/tv/{id}/translations` returns the languages in which TMDB has a translation of the show, as language and region (e.g. `de-DE`, `pt-BR`). Only these are offered in the language selection, named in the language of the UI (e.g. "Portuguese (Brazil)") and sorted by name. |
| T-25 | **Selected language:** the start language is the language the application was started with (UI language of the operating system or `--lang`). For every show, the language that fits the preferred language best is selected: the same language and region, the same language in its main region (e.g. `de-DE` for `de`), the same language in any region, then English likewise, then the first language of the show. The preferred language is the start language until the user selects a language; afterwards it is the selected one, also for other shows. |
| T-11 | `GET /3/tv/{id}` in the selected language returns the number of seasons. |
| T-12 | Seasons 1 to that number are loaded in parallel with `GET /3/tv/{id}/season/{n}` in the selected language. Season 0 (specials) is never loaded. |
| T-13 | **Matching:** a local file belongs to the TMDB episode with the same season and episode number (of a double episode: its first episode). There is no other matching (no title comparison, no absolute numbering, no alternative order). |
| T-14 | Files without a matching TMDB episode have no new name and are shown as "not found". |
| T-15 | If no result is selected, all new names and the poster are cleared. |

### 4.3 Poster

| ID | Requirement |
|---|---|
| T-16 | The poster of the selected show is loaded from `https://image.tmdb.org/t/p/w400<poster_path>` and shown on the right. Without a poster the area stays empty. |

### 4.4 Cache, concurrency and errors

| ID | Requirement |
|---|---|
| T-17 | Results are kept in memory while the application runs: search results per search term, episodes per show and language, languages per show, posters per URL. Repeated requests return immediately without network access. There is no persistent cache. |
| T-18 | All network access and the renaming run in the background; the UI stays responsive. |
| T-19 | If several requests of the same kind run, only the result of the most recently started one is shown. Older results only go into the cache. |
| T-20 | **Network errors are visible:** if a TMDB request fails, this is shown in the UI. A failed request is not cached, so a retry can succeed. |

### 4.5 API key

| ID | Requirement |
|---|---|
| T-21 | Order: 1. environment variable `TMDB_API_KEY` at runtime. 2. key embedded into the executable at build time (from the environment variable `TMDB_API_KEY` or the file `.env`). |
| T-22 | It is a TMDB v3 key, appended to every request as query parameter `api_key`. |
| T-23 | Without a key an error is shown and the application exits (see section 8). |

## 5. New file name

| ID | Requirement |
|---|---|
| N-1 | Form: `S%02dE%02d - <episode title>`. Season and episode have at least two digits, larger numbers are kept completely (`S12E123`). A double episode gets both numbers and the title of its first episode: `S01E02E03 - <title>`. |
| N-2 | Without a title (TMDB returns none, or nothing is left after cleaning) the name is only the episode code: `S01E02`, `S01E02E03`. |
| N-7 | Placeholder titles that TMDB returns for languages without translated episode titles (e.g. `Episode 1`, `Episódio 1`) are used like real titles: `S01E01 - Episódio 1`. This is intended; they are not detected or removed. |
| N-3 | Dots at the start and end of the title are removed (`...und dann?` → `und dann`, `Ende.` → `Ende`), unless fewer than two characters would remain. After cleaning (N-4), dots at the end are removed in any case, so the name never ends with a dot followed by the dot of the extension (`Dr. House & Co.` → `S01E02 - Dr. House & Co`, `A.?` → `S01E02 - A`). |
| N-4 | The title is cleaned, in this order: 1. All kinds of quotation marks (`"` `'` `’` `‘` `„` `“` `”` `«` `»` ...) become `'`. 2. `...` becomes `…`, all kinds of dashes (`–` en dash, `—` em dash, ...) become `-`. 3. `:` and `?` are removed. 4. The punctuation `-` `.` `,` `'` `&` `(` `)` `…` is kept. Every other special character becomes `_`, including the characters forbidden on Windows and `!`. Letters are kept completely, including umlauts, accents and `ß`, as well as digits. 5. Every whitespace character (tab, no-break space, ...) becomes a normal space, consecutive spaces become one; spaces at the start and end are removed. Examples: `AC/DC` → `AC_DC`, `Say "Hi"` → `Say 'Hi'`, `Stop!` → `Stop_`, `Teil 1: Der Anfang` → `Teil 1 Der Anfang`, `Was nun?` → `Was nun`, `Dr. House & Co.` stays unchanged (but see N-3). |
| N-5 | The extension is kept unchanged, including its case. The file stays in its folder. |
| N-6 | The show name is not part of the file name; it is only shown in the result list. |

## 6. User interface

A single window, title `Media Renamer`. It follows the light or dark mode of the operating system unless `--theme=light` or `--theme=dark` is given (e.g. for testing); the dark mode uses shades of gray, not black. All texts are localized (English, German); the language
follows the operating system and can be set with `--lang=<culture>`.

### 6.1 Layout

| Area | Element | Function |
|---|---|---|
| left | text field | search term for the show, initially the folder name |
| left | list | search results (T-10), single selection |
| left | selection box | language of the episode titles: the languages of the selected show (T-24), default see T-25; empty without a selected show |
| left | button "Rename" | starts renaming |
| center | table | one row per video file |
| right | image | poster of the selected show, at most 400 × 750, aspect ratio kept |
| bottom | status bar | progress indicator and absolute path of the show folder; on the right `© cathixx   <version>`, the version is `DEVELOP` for local builds |
| | folder selection | choose or change the show folder (E-8) |

### 6.2 Table

| Column | Content |
|---|---|
| (no title) | checkbox "rename this file". Only visible when nothing is loading and the new name differs from the old one. |
| `#` | consecutive number, three digits (`001`) |
| old file name | current name without extension |
| new file name | proposal without extension |

- The columns are not sortable. The names are not editable, only the checkbox can be changed.
- A separator line is shown exactly before the first displayed row of every further season.
- Display of the column "new file name":

| State | Display |
|---|---|
| loading | "Loading...", italic, light gray |
| no name found | `<not found>`, italic, red |
| name differs only in case | dark blue |
| name differs otherwise | bold |
| name is identical | normal |

### 6.3 State of the controls

| Situation | Effect |
|---|---|
| any background task is running | progress indicator visible, table and "Rename" disabled |
| show search is running | additionally the result list is disabled |
| renaming is running, or an update is being downloaded | additionally result list, search field, language selection and folder selection are disabled |
| no file is selected **and** changeable | "Rename" disabled |
| episode numbers conflict (R-1) | "Rename" disabled, the conflict is shown |

## 7. Renaming

| ID | Requirement |
|---|---|
| R-1 | **No data loss, unique episode numbers:** among the files selected for renaming, every episode number may occur only once per season (both numbers of a double episode count). If a number occurs more than once, an error is shown that names or marks the affected files, and renaming is not possible. Deselecting affected files until only one per number is selected resolves it. |
| R-2 | An existing file is never overwritten, also not by renaming. This is the last safeguard in addition to R-1. |
| R-3 | A file is renamed if it is selected, has a new name and the new name differs from the old one (a difference in case counts). |
| R-4 | There is no confirmation and no undo. The preview in the table is the only check. |
| R-5 | The files are renamed one after another within their folders. A failure does not stop the run. |
| R-6 | After success the row shows the new name as old name. It is done, the checkbox disappears. |
| R-7 | Failed files are listed together in an error dialog at the end. They stay unchanged in the list and can be retried. |
| R-8 | After renaming the application stays open. Language or show can be changed and the files renamed again. |

## 8. Error handling

| Trigger | Message | Afterwards |
|---|---|---|
| no argument | none | the user chooses a folder (E-8) |
| first argument is not a folder | hint naming the path | the user chooses a folder (E-9) |
| ignored arguments in season folder mode | hint naming the paths | the application continues with the valid seasons (E-9) |
| no season folder found | hint | the user chooses another folder (E-10) |
| no TMDB API key | "No API key for TheMovieDB" / "This version was built without an API key. Please set your own TMDB API key in the environment variable TMDB_API_KEY." | the application exits |
| TMDB request failed | visible in the UI | the application continues, a retry is possible (T-20) |
| duplicate episode numbers | names or marks the affected files | renaming is disabled until resolved (R-1) |
| files could not be renamed | "The following files could not be renamed: <file names>" | the application continues (R-7) |
| update check failed (e.g. offline) | none, only logged | the application continues (UP-1) |
| update could not be downloaded or verified | "The update failed" with the reason and the link to the release page | the current version continues (UP-5) |
| new version could not replace the executable | "The update failed" with the reason | the new version continues (UP-7) |
| unexpected error | "An unexpected error occurred" with message, type and stack trace (selectable, so it can be reported) | the application continues |

## 9. Constraints

| Topic | Requirement |
|---|---|
| Platforms | Windows, Linux and macOS (x64 and Apple Silicon). Main use is on Windows. |
| Delivery | a single standalone executable per platform, no runtime installation needed |
| Code signing | none. The executables are intentionally not code-signed; Windows SmartScreen and macOS Gatekeeper warn on the first start. |
| Configuration | none. Nothing is stored: no settings, no window state, no history. |
| Logging | console; every rename is logged as `rename 'old' -> 'new'` |
| Versioning | every push to `main` creates a release with the next version number (patch; `#minor`/`#major` in the commit message) |

Not supported, among others: movies (only TV shows), data sources other than TMDB, moving files or creating folders,
renaming companion files (subtitles, `.nfo`), a configurable name format, manually editing the new name or the episode
number, drag & drop, batch processing of several shows, operation without UI.

## 10. Updates

The application updates itself from the [GitHub releases](https://github.com/cathixx/MediaRenamer/releases).

| ID | Requirement |
|---|---|
| UP-1 | After the show folder has been opened at startup, the application checks for the latest release (`GET https://api.github.com/repos/cathixx/MediaRenamer/releases/latest`). If the check fails (e.g. offline), nothing is shown; the error is only logged. |
| UP-2 | Only a release with a higher version than the running one and with a file for the current platform (`MediaRenamer-<runtime>.zip` / `.tar.gz`) counts as update. Local builds (version `0.0.0-snapshot`) never check for updates. |
| UP-3 | The user is asked: "Version X is available (installed: Y). Update now?" with the buttons "Update now" and "Later". "Later" continues with the current version; the question comes again at the next start. |
| UP-4 | "Update now": the file is downloaded and checked (size and SHA-256 checksum provided by GitHub), and the new executable is extracted next to the running one as `<name>.new`. The running executable is never changed: a single-file application loads parts of itself from its file while it runs. The application starts `<name>.new` with the options `--complete-update=<path> --previous-process=<id>` and the original arguments, and exits. The new version waits until the previous one has exited, copies itself over `<name>` (retried for up to 30 seconds, e.g. while a virus scanner checks the file), starts it with the original arguments and exits. A message shows that the update is being downloaded; the controls are disabled meanwhile. A running rename is finished first. |
| UP-5 | If downloading fails (e.g. no write permission in the folder of the executable, download error, wrong checksum, or any unexpected error), an error is shown with the details and the link to the release page, and the current version keeps running unchanged. |
| UP-6 | At every start, the `<name>.new` file left by an update is deleted (retried for a few seconds, as it may still be exiting), as well as a `<name>.old` file left by versions up to 1.0.3. |
| UP-7 | If the new version can't replace the executable, it keeps running as the application (it is the new version) and shows the error; the next update tries again. If it can't be started at all, the previous version shows the error and keeps running. |

## 11. Open questions

None.

## 12. Implementation status

All sections are implemented. On Windows the application has been tested manually, including renaming and the
self-update; on Linux and macOS only with automated tests and headless screenshots.

| Section | Code |
|---|---|
| 2 Start and input | `MediaRenamer.Core/Folders`, `MediaRenamer/ViewModels/MainWindowViewModel.cs` |
| 3 Video files and episode numbers | `MediaRenamer.Core/Episodes` |
| 4 TheMovieDB | `MediaRenamer.Core/Tmdb`, key embedding: `MediaRenamer/EmbeddedTmdbApiKey.targets` |
| 5 New file name | `MediaRenamer.Core/Naming` |
| 6 User interface | `MediaRenamer/Views`, `MediaRenamer/ViewModels` |
| 7 Renaming | `MediaRenamer.Core/Renaming`, `MediaRenamer/ViewModels/MainWindowViewModel.cs` |
| 8 Error handling | `MediaRenamer/ViewModels/MainWindowViewModel.cs`, `MediaRenamer/Views/MessageWindow.axaml` |
| 10 Updates | `MediaRenamer.Core/Updates`, `MediaRenamer/ViewModels/MainWindowViewModel.cs`, `MediaRenamer/App.axaml.cs` |
