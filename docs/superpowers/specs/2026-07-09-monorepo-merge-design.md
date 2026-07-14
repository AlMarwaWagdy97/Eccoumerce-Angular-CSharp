# Design: Merge backend + frontend into one repo

**Date:** 2026-07-09
**Status:** Approved (pending implementation plan)

## Goal

Combine the two currently-separate git repositories into a single git repository
rooted at `D:\ECom`, preserving the full commit history of both, using
`git subtree`. The result is a monorepo with two subdirectories:

- `backend/`  — from `Ecommerce_Backend` (ASP.NET Core .NET 10 Web API; GitHub remote `AlMarwaWagdy97/Ecommerce-C-`)
- `frontend/` — from `Ecommerce_Frontend` (Angular 22 storefront; GitHub remote `AlMarwaWagdy97/Ecommerce-angular`)

The merged repo is **local only** — no new remote is configured or pushed in this
work. Installed dependencies (`frontend/node_modules`) are preserved so no
reinstall is required.

## Decisions (locked)

| Decision | Choice |
| --- | --- |
| History | Preserve **both** histories via `git subtree add`. |
| Location | Initialize the new repo **in place at `D:\ECom`**. |
| Layout | Rename subfolders to `backend/` and `frontend/`. |
| Pending frontend edits | **Commit** them first (preserve in history). |
| Remote | Local only — user creates/pushes a remote later themselves. |
| Merge mechanism | `git subtree` (available out of the box; `git-filter-repo` is not installed). |

## Starting state (verified 2026-07-09)

- `D:\ECom` is a plain container directory, **not** a git repo. It holds the
  root `CLAUDE.md` (written earlier, references the current `Ecommerce_Backend/`
  and `Ecommerce_Frontend/` paths) and `docs/`.
- `D:\ECom\Ecommerce_Backend` — git repo, branch `main`, 2 commits, **with
  uncommitted work** (a full Cart/Order feature: `Contracts/Cart/`,
  `CartController`, `CartService`/`ICartService`, `CartErrors`, new entities
  `Cart`/`CartItem`/`Order`/`OrderItem`/`Address`/`Review`/`ProductImage`/
  `NewsletterSubscription` + their EF configs, the `AddCart` migration, plus
  modified `ProductsController`/`CategoriesController`/`DependacyInjection`/
  `Program.cs`/`ProductService`/`ApplicationDbContext`/`Product.cs` and the
  backend's own untracked `CLAUDE.md`). Tracks **59** files; does **not** track
  `bin/`, `obj/`, `.vs/`. *(Corrected 2026-07-09: earlier draft wrongly called
  this tree clean.)*
- `D:\ECom\Ecommerce_Frontend` — git repo, branch `main`, 1 commit, **with
  uncommitted modifications** (`angular.json`, `package.json`, `package-lock.json`,
  `src/app/app.config.ts`, `src/app/app.routes.ts`, `src/app/app.routes.server.ts`,
  `src/app/site/core/services/product-services.ts`, navbar files, and possibly
  more). Tracks **59** files; does **not** track `node_modules/`.
- Both subprojects have their own `CLAUDE.md` and their own `.gitignore`.

## Approach chosen

**Approach A — `git subtree add`.** Rejected alternative: `git-filter-repo`
rewrite (cleanest history but requires a `pip install` and rewrites all commit
SHAs; unnecessary at 2 + 1 commits).

Trade-off accepted: with subtree, historical commits keep their original
root-relative paths and a merge commit stitches them under the prefix going
forward. `git log <prefix>/` and `git log --follow` still work correctly.

## Procedure

### Step 1 — Preserve pending work + backup

1. Zip-backup `D:\ECom` to a location outside the tree (rollback net). The
   originals also remain on their GitHub remotes.
2. In `D:\ECom\Ecommerce_Frontend`: `git add -A` and commit the pending changes
   (e.g. message `Save pending frontend changes before monorepo merge`). After
   this, the frontend working tree is clean and all work is in history.
3. In `D:\ECom\Ecommerce_Backend`: `git add -A` and commit the pending Cart/Order
   work (e.g. message `Save pending backend changes before monorepo merge`) so
   the backend also comes into the merge complete. `subtree add` only imports
   committed history, so this step is required — not optional.

### Step 2 — Build the merged repo in a temp directory

Work in a clean temp dir **outside** `D:\ECom` (e.g. the session scratchpad) so
that git never sees the source repos as nested/gitlinked children.

```
git init                                   # ensure default branch is 'main'
git commit --allow-empty -m "Root of merged Ecommerce monorepo"
git subtree add --prefix=backend  "D:/ECom/Ecommerce_Backend"  main
git subtree add --prefix=frontend "D:/ECom/Ecommerce_Frontend" main
```

Result: temp repo has `backend/` and `frontend/` each carrying full history plus
a stitching merge commit. (An initial empty commit gives `subtree add` a base to
merge onto.)

### Step 3 — Add root-level files

In the temp repo root:

1. Copy `D:\ECom\CLAUDE.md` in and **rewrite its path references**:
   `Ecommerce_Backend/` → `backend/`, `Ecommerce_Frontend/` → `frontend/`
   (including the markdown link targets and the dev-workflow "from ..." lines).
2. Add a minimal root `.gitignore` for cross-cutting cruft only
   (`.vs/`, `.idea/`, `.DS_Store`, `Thumbs.db`). The per-project `.gitignore`
   files already travel inside each subtree and continue to cover
   `node_modules/`, `bin/`, `obj/`, `dist/`.
3. Commit these root files.

### Step 4 — Swap into place at `D:\ECom` (preserve `node_modules`)

1. Rename `D:\ECom\Ecommerce_Backend` → `D:\ECom\backend` and
   `D:\ECom\Ecommerce_Frontend` → `D:\ECom\frontend`. Renaming (not
   re-checkout) keeps all working files, including installed
   `frontend/node_modules` and any `bin/`/`obj/` build output.
2. Delete the nested `.git` folders now inside `backend/` and `frontend/` — the
   authoritative history lives in the merged repo.
3. Move `temp/.git` into `D:\ECom\.git`, and move the temp root files
   (`CLAUDE.md`, `.gitignore`, plus the empty-root marker already in history)
   into `D:\ECom`, replacing the old root `CLAUDE.md`.

After this, `D:\ECom` is the merged repo. The renamed working folders contain a
superset of the tracked files (tracked files + ignored artifacts); git sees the
tracked files as matching `HEAD`.

### Step 5 — Verify

Run and confirm:

- `git status` — clean. `node_modules/`, `bin/`, `obj/`, `.vs/` do **not** appear
  (proves ignore rules survived the merge). If tracked files show as modified,
  the swap left a content mismatch — investigate before proceeding.
- `git log --oneline --graph --all` — shows commits from **both** original repos.
- `git log --oneline -- backend/` — shows the backend's commits
  (`Category, products api`, `first commit`).
- `git log --oneline -- frontend/` — shows the frontend's commits (incl. the
  Step 1 "pending changes" commit).
- `git ls-files | wc -l` — roughly `118` (59 + 59) plus the added root files.
- Spot-check that `backend/Ecommerce/Ecommerce.csproj` and
  `frontend/package.json` exist at their new paths.

## Rollback

Keep the Step 1 zip backup until all Step 5 checks pass. If the swap goes wrong,
restore `D:\ECom` from the zip; both projects also remain intact on their GitHub
remotes.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Git treating source repos as nested submodules | Build the merged repo in a temp dir outside `D:\ECom`; sources referenced by absolute path. |
| Losing installed `node_modules` (large re-download) | Rename working folders in place rather than doing a fresh checkout. |
| Losing uncommitted frontend work | Commit it in Step 1 before any merge. |
| Ignore rules not carried over | Per-project `.gitignore` files are tracked and travel inside each subtree; verified in Step 5 via clean `git status`. |
| Stale root `CLAUDE.md` paths after rename | Rewrite path references in Step 3. |

## Out of scope

- Creating or pushing to a new GitHub remote (user will do this later).
- Any build-tooling unification (shared CI, root `package.json`, task runner).
- Deduplicating the front/back cart or reconciling the API contract mismatches
  documented in the root `CLAUDE.md`.
- Deleting the original GitHub repositories.
