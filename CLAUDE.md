# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

This is a **monorepo** combining what were originally two separately-versioned
projects, merged via `git subtree` (both projects' full commit history is
preserved — see `docs/superpowers/specs/2026-07-09-monorepo-merge-design.md`).
Each subproject keeps its **own, more detailed `CLAUDE.md`**; read the relevant
one before working in that subtree:

- **`backend/`** — ASP.NET Core **.NET 10** Web API (categories, products, cart, orders, favorites, addresses, JWT auth). See [backend/CLAUDE.md](backend/CLAUDE.md). Formerly its own repo, remote `github.com/AlMarwaWagdy97/Ecommerce-C-`.
- **`frontend/`** — Angular 22 storefront (standalone components, SSR, signals). See [frontend/CLAUDE.md](frontend/CLAUDE.md). Formerly its own repo, remote `github.com/AlMarwaWagdy97/Ecommerce-angular`.

The two are the API and the client for the same e-commerce app; they are
developed together and now version-controlled together. A change to one often
has a counterpart in the other (a new endpoint needs a frontend service call; a
new response field needs an interface update).

## Running the full stack (dev)

Start the backend first, then the frontend — the frontend calls the API on startup and during SSR prerender.

```powershell
# Terminal 1 — backend (from repo root)
dotnet run --project backend/Ecommerce   # https://localhost:7297  (Scalar API docs at /scalar/v1)

# Terminal 2 — frontend (from frontend/)
npm start                                 # http://localhost:4200
```

The frontend's `Environment.apiUrl` is hardcoded to **`https://localhost:7297/api`** in dev ([frontend/src/environments/environment.ts]). If you change the backend port (`backend/Ecommerce/Properties/launchSettings.json`) you must update it here too.

## Frontend ↔ backend contract

Both sides define parallel shapes and there are real inconsistencies to watch for when wiring them up:

- **Product-by-slug.** Backend serves product detail at `GET api/Products/{slug}`. The frontend's `getProductBySlug()` currently assumes `/products/slug/{slug}` — a known mismatch flagged in the frontend CLAUDE.md; reconcile the two when touching product detail.
- **Casing.** Backend routes are PascalCase (`/api/Products`, `/api/Categories`); the frontend calls a mix of `/Products` and `/products`. Match the actual backend route, which is case-insensitive on the server but should be kept consistent.
- **Response envelope.** Backend wraps list responses in `ApiResponse<T> = { StatusCode, Message, Data }`; the frontend unwraps via `.pipe(map(r => r.data))`. Detail endpoints are unwrapped defensively (`res?.data ?? res`) because envelope usage is inconsistent across backend actions.
- **Cart is duplicated, not shared.** The backend has a full cart API (`api/Cart`, DB-backed, `CartService`), but the frontend implements its **own client-side cart** in `localStorage` (`CartServices`) and does not call the backend cart endpoints. Checkout is still simulated client-side as of this writing — see `docs/superpowers/specs/2026-07-14-account-features-design.md` for the in-progress work to make orders real. Confirm which cart is authoritative before building on either.

For per-project commands, architecture, and conventions, defer to the two subproject CLAUDE.md files rather than duplicating here.

## In-progress work

See `docs/superpowers/specs/2026-07-14-account-features-design.md` for the
active design covering authentication wiring, Profile/My Account, Orders +
Tracking, Favorites, Addresses, and Cards (saved-card metadata), including a
left-sidebar account shell replacing the top navbar **for the account area
only**.
