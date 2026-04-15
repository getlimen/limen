# Working conventions — getlimen

These are the rules everyone (humans or AI assistants) follows when contributing to any repo in the `getlimen` organization. Adapted from `PianoNic/KotifyClient` conventions; see also the per-repo `CLAUDE.md`.

---

## Issues

- **Title:** short, imperative, sentence case.
  Examples: `Add provisioning key endpoint`, `Fix cert renewal race on Kestrel reload`.
- **Body:** 1–2 sentences explaining **what** and **why**. No checklists, no markdown headers, no "acceptance criteria" sections. Match the style of existing closed issues.
- **One issue = one focused change.** A plan (e.g. *Plan 01 Foundation*) counts as a focused change.
- **Every issue MUST have at least one label.** Pick the most accurate from the repo's existing labels (`enhancement`, `bug`, `documentation`, `refactor`, `feature`, `plan`, `infra`). If none fit, ask before inventing. Never open an unlabeled issue.

## Branches

- **Pattern:** `<type>/<issueNumber>_<PascalCaseName>`
  - Types: `feature`, `fix`, `chore`, `docs`, `refactor`
- **Examples:**
  - `feature/12_Plan01Foundation`
  - `fix/15_CertRenewalRaceOnReload`
  - `docs/20_ClarifyRoleFlagsInHandoff`
- **One branch per issue.** No multi-issue branches.
- Never work directly on `main`.

## Commits

### Title (subject line)

- **Format:** `<type>: <short imperative>` OR plain `<short imperative>` when the type is obvious from context.
- **Types:** `feat`, `fix`, `chore`, `docs`, `test`, `refactor`, `ci`, `build`, `perf`.
- **Do NOT use scope-parens.** No `feat(foo):`, no `fix(bar):` — keep the prefix plain.
- **Sentence case.** Imperative mood ("Add X", not "Added X" or "Adds X").
- **Target 50 chars, hard cap 72.** Longer titles get truncated in GitHub listings.
- **No trailing period.**
- **No AI / Claude attribution anywhere** — not in title, not in body, not in trailers. Hard rule.

Acceptable forms:

```
feat: Add restorePaused option to transferPlaybackHere
fix: Keep enum values() and valueOf() methods under proguard
docs: Document restorePaused option for transferPlaybackHere
refactor: Extract route-table builder from YARP config provider
Add Song.searchSuggestions + extend SearchResult with playlists
Default hasPremium to false when product field is absent
```

Bare titles (no `type:`) are fine when the change's nature is self-evident. Prefer the explicit `type:` form for anything ambiguous.

### Body

- **Required for any non-trivial commit.** Trivial commits (typo fixes, formatting) can skip it.
- **Wrap at ~72 columns** (classic Git style — renders well in `git log`, GitHub, emails).
- Explain **what** changed and **why**. The diff already shows *how*; don't repeat it in prose.
- When a commit touches several distinct areas, **group with short labeled paragraphs**:

  ```
  searchDesktop:
  - Generalize section extraction so adding new sections is one line...

  searchSuggestions (new):
  - Song.searchSuggestions(query) hits the v2/query pathfinder...

  Live verifier:
  - DtoVerifySearchSuggestions.kt exercises both endpoints...
  ```

- **Call out caveats** explicitly — default behavior, opt-in flags, rate-limiting expectations, required caller-side work.
- **Blank line** between title and body. Blank line between paragraphs.

### Footer

- **End with `Closes #<issue>`** when the commit closes a ticket. GitHub auto-closes on merge.
- **Squash-merge inserts `(#<pr>)` into the title automatically.** Don't add it by hand; let GitHub do it.
- If a commit fixes multiple issues: `Closes #12, closes #15`.
- No other trailers (no `Signed-off-by` required in this org; no `Co-Authored-By` — see AI attribution rule).

### Full example (body style to mimic)

```
feat: Add restorePaused option to transferPlaybackHere

PlayerConnect.transferBetween hardcoded restore_paused: "restore" so
every transfer auto-resumes playback. The Spotify web player uses
restore_paused: "pause" during its cold-start protocol — claim the
device without starting audio, so it can fetch metadata + load DRM +
buffer locally before sending the actual play command. Without this,
consumers can't mirror the web-player flow and end up with a brief
'ghost playback on the previous device' window.

Add a restorePaused parameter to both transferPlaybackHere and
transferPlaybackTo that flips the option. Default stays false to keep
the existing behavior.

Closes #52
```

### Squash-merge note

Individual commits on a feature branch can be casual work-in-progress ("wip: try X", "fix tests", "address review") — they get **squashed into one** on merge. The **PR title + PR body** become the squash commit's subject + body on `main`, so write your PR description as if it were the final commit message (because it is).

## Pull requests

- **Title mirrors** the commit / issue title.
- **Body:** 1–2 sentences + `Closes #<issue>`. No AI attribution.
- **Every PR MUST have at least one label.** Same label rules as issues. After creating: `gh pr create --label <name>` — verify with `gh pr view <n> --json labels`.
- **Merge strategy: squash-merge only.** Repo settings enforce this. Author approves their own PRs after CI is green.
- Branch is auto-deleted on merge (repo setting).
- One commit lands on `main` per PR; the squash commit's subject mirrors the PR title.

## Use generators and CLI tools — best practices first

**Rule:** IF a CLI tool / generator exists for the thing you're about to create, USE IT. Don't hand-roll files that a standard generator can produce.

Examples:
- New .NET project → `dotnet new <template>` (`classlib`, `web`, `worker`, `xunit`, ...)
- New solution → `dotnet new slnx`
- New Angular app → `ng new` / `ng generate component` / `ng generate service`
- New EF migration → `dotnet ef migrations add <name>`
- Add package to csproj → `dotnet add package <pkg>`
- New GitHub repo → `gh repo create`
- Create issue / PR / release → `gh issue create` / `gh pr create` / `gh release create`
- Docker image → `docker init` if you need a starter
- Tailwind init → `npx tailwindcss init`
- Git worktree → `git worktree add`

**Rationale:**
- Generators follow current best-practice defaults (nullability, analyzers, file-scoped namespaces, etc.) automatically.
- Less bike-shedding about boilerplate.
- Generated artifacts match what the community expects.
- Less error surface than hand-typing scaffolding.

**If you don't know the generator command:** search online before typing out files by hand. The 2 minutes spent learning the right command saves 20 minutes of subtly-wrong boilerplate later. The [context7 MCP](https://github.com/upstash/context7) or web search tools are available — use them. Do not blindly guess CLI syntax; verify.

## Test-driven development

- Write the failing test before the implementation.
- Run the test, see it fail with a clear message.
- Implement the minimal code to pass.
- Run the test, see it pass.
- Commit once green.
- Refactor only with tests passing.

## Code style

- **C#:** file-scoped namespaces, nullable reference types, `TreatWarningsAsErrors=true`. Enforced via `Directory.Build.props` + `.editorconfig`.
- **TypeScript / Angular:** standalone components, signals, new control flow syntax. Prettier defaults.
- **Naming:** business-domain language from the spec. Do not invent parallel vocabulary — if the spec says "Node" and "Service", the code says `Node` and `Service`.

## Clean architecture — strict layer rules

- **Domain** — ONLY database entity models. Nothing else. No value objects, no enums, no services, no interfaces.
- **Infrastructure** — ALL DB code + ALL external integrations (WS/HTTP clients, OIDC, Quartz, Docker API, subprocesses, Kestrel/YARP wiring, ACME).
- **Application** — EVERYTHING else: services, commands, queries, DTOs, validators, interfaces, Mediator behaviors.

**Application layout:**
- Top-level `Commands/` and `Queries/` folders (capitalized), organized by feature.
- **One file per command or query**, containing both the record type AND its handler. Do NOT split.

## Labels to create on every new repo

Seed labels so contributors never hit the "no label exists yet" problem. Use `gh label create`:

- `feature` (green)
- `bug` (red)
- `documentation` (blue)
- `refactor` (yellow)
- `enhancement` (green, softer shade)
- `plan` (purple) — for issues tracking a full implementation plan
- `infra` (gray) — CI, Docker, release pipeline
- `good first issue` (green-blue)
- `help wanted` (orange)

## Verifying conventions before merge

Before squash-merging your own PR:

```bash
gh pr view <n> --json labels,title,body,state  # must have label; body mentions Closes #<issue>
gh pr checks <n>                                # CI must be green
gh pr review <n> --approve                      # self-approve
gh pr merge <n> --squash --delete-branch        # squash-merge, remove branch
```

---

## Source of truth

When in doubt: `KotifyClient` (by PianoNic) is the canonical reference for how these conventions play out in practice. Glance at recent PRs/issues there if the pattern is unclear.

For the deeper architectural context of Limen specifically, see `docs/HANDOFF.md` and `docs/superpowers/specs/`.
