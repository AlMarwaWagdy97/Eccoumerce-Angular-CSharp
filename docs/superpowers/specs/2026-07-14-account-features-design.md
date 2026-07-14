# Design: Authentication & Account features (Profile, Orders, Tracking, Favorites, Addresses)

**Date:** 2026-07-14
**Status:** Draft for user review
**Predecessor:** `2026-07-09-monorepo-merge-design.md` (Phase 0 executes it)

## Goal

Deliver a working authentication + account experience across the .NET backend
and Angular frontend: **Login, Register, Logout, Profile (My Info), My Orders,
Order Tracking, My Favorites, My Addresses** — with the account area restyled to
match the supplied reference screenshot, then both apps run and each flow tested
for happy-path **and** error responses.

The bulk of these features already exist as **uncommitted, AI-generated
scaffolding** that compiles but is unfinished (placeholder data, no create-order
path, logic misplaced in `AuthController`, unmigrated tables, unstyled UI). This
work audits, finishes, refactors, styles, and verifies that scaffolding — it is
not a greenfield build.

## Decisions (locked with user)

| Topic | Decision |
| --- | --- |
| Orders data | **Real end-to-end**: add create-order endpoint, checkout POSTs to it; then seed sample orders for display. |
| Auth scope | **Minimal**: wire existing JWT login/register + token storage + route guard + logout. **No** password reset or email confirmation. |
| Sequencing | **Finish the monorepo merge first**; all feature work happens in the merged `D:\ECom` repo (`backend/`, `frontend/`). |
| Backend structure | **Refactor** inline `AuthController` logic into dedicated controllers + services (follow existing `ICategoryService`/`Result<T>` pattern). |
| Account sidebar | **Five items**: Account (profile), Orders, Address, Cards, Favorites. Address CRUD in scope; Tracking is a click-through detail off Orders. |
| Cards | **Saved-card metadata only** (brand, last-4, expiry, cardholder) — add/list/delete. **No** real charging or payment processing. Net-new entity + CRUD + UI. |
| Sidebar scope | **Account area only** — account pages use the left sidebar from the reference; the storefront (Home/Shop/Cart/etc.) keeps its existing top navbar. |
| Visual target | Reference screenshot: left vertical sidebar, cream background, forest-green accent, card-based forms. Scoped to the account area. |

## Current state (verified 2026-07-14)

- **Backend builds** (`dotnet build` → 0 errors; 2 NuGet vulnerability warnings, pre-existing).
- **Frontend builds** (`ng build --configuration development` → succeeds, prerenders 11 routes; only the known Sass `@import` deprecation warning).
- **Already scaffolded (uncommitted):**
  - Backend: `AuthController` with inline `profile`/`orders`/`orders/{n}/tracking`/`favorites` endpoints; entities `Order`, `OrderItem`, `Address`, `Favorite`, `Cart`, `Review`, `ProductImage`; matching DbSets and EF configs; contracts `ProfileResponse`, `OrderSummaryResponse`, `FavoriteResponse`; migrations `InitialEcommerce`, `AddCart`, `AddFavorites`.
  - Frontend: components + routes for `login`, `register`, `profile`, `orders`, `favorites`, `tracking` (routes wired in `app.routes.ts`).
- **Confirmed gaps:**
  - **No migration creates the `Orders`/`OrderItems`/`Addresses` tables** — the entities/DbSets exist but are unmigrated, so those queries fail at runtime until a migration is added and applied.
  - `AuthController.Profile` builds `ProfileResponse` with a placeholder `CreatedAt` (`user.Id != null ? DateTime.UtcNow : DateTime.UtcNow`) and there is **no profile update** endpoint.
  - **No create-order endpoint**; checkout is simulated client-side, so My Orders/Tracking have no real data source.
  - No Address CRUD endpoints; no favorites/orders **service** layer (logic sits in the controller).
  - **No `Card` entity/endpoints at all** — the saved-cards feature is entirely net-new (entity, config, migration, service, controller, UI).
  - Frontend auth is unwired: no token storage, HTTP bearer interceptor, route guard, or logout; account pages are unprotected and likely not calling the API yet.
  - Account pages are unstyled relative to the reference.
- `Address` entity fields: `Id, UserId, FullName, Phone, Line1, Line2?, City, State, Country, PostalCode?, IsDefault`.

## Phase 0 — Finish the monorepo merge

Execute the approved `2026-07-09-monorepo-merge-design.md` (with the corrected
starting-state note: **both** repos have uncommitted work, both get committed
before the subtree merge). Result: single git repo at `D:\ECom` with `backend/`
and `frontend/`, both histories preserved, this spec + the merge spec tracked as
root files. Dev commands afterward:

```powershell
dotnet run --project backend/Ecommerce     # https://localhost:7297
npm start                                   # from frontend/  -> http://localhost:4200
```

## Phase 1 — Backend: refactor, finish, migrate, seed

Follow repo conventions: thin controllers → `Scoped` services returning
`Result`/`Result<T>`, DTOs as `record`s with FluentValidation validators,
Mapster for mapping, `ApiResponse<T>` envelope, per-domain `*Errors` classes.

1. **Auth**: keep `AuthController` to `register`, `login`, `refresh`,
   `revoke-refresh-token`, and add **`logout`** (revoke current refresh token).
   Move all non-auth logic out.
2. **Profile** — `ProfileController` + `IProfileService`:
   - `GET api/Profile` → real `ProfileResponse` (drop the placeholder `CreatedAt`; if a created date is wanted, add a persisted column rather than faking it).
   - `PUT api/Profile` → update `FirstName`, `LastName`, `PhoneNumber` (validated); returns the updated profile.
3. **Orders** — `OrdersController` + `IOrderService`:
   - `POST api/Orders` → create an order from a **posted line-item list + chosen address** (the authoritative cart is the client-side `localStorage` cart, so checkout sends its contents): validate products/prices server-side, snapshot the shipping address onto the order, compute subtotal/shipping/total, generate `OrderNumber`, set `Status = Pending`. This is what makes My Orders real.
   - `GET api/Orders` → the caller's orders (summary list).
   - `GET api/Orders/{orderNumber}` → order detail with items.
   - `GET api/Orders/{orderNumber}/tracking` → structured tracking (status + timeline derived from `Status`/`CreatedOn`), 404 if not owned.
4. **Favorites** — `FavoritesController` + `IFavoriteService`: list / add / remove (idempotent add, 404 on missing product).
5. **Addresses** — `AddressesController` + `IAddressService`: `GET/POST/PUT/DELETE api/Addresses`, plus set-default behavior (`IsDefault` unique per user).
6. **Cards (net-new)** — `Card` entity (`Id, UserId, CardholderName, Brand,
   Last4, ExpiryMonth, ExpiryYear, IsDefault`) + EF config + `CardsController` +
   `ICardService`: `GET/POST/DELETE api/Cards`, set-default. **Store safe
   metadata only** — never a full PAN or CVV; the client sends only brand/last-4/
   expiry/cardholder. No charging.
7. **Migration**: add one EF migration covering `Orders`, `OrderItems`,
   `Addresses`, `Cards` (and any config not yet migrated); `dotnet ef database update`.
8. **Seed**: a dev seeder inserting a few sample orders (varied statuses),
   favorites, an address, and a saved card for the test user, so the UI shows
   content immediately.

All new failure cases go through `Result` + a domain `*Errors` entry (e.g.
`OrderErrors`, `AddressErrors`), surfaced as `ApiResponse`/`ProblemDetails`.

## Phase 2 — Frontend: auth wiring + account shell + panels

Signal-based, matching Angular 22 conventions (standalone components,
`inject()`/`signal()`/`computed()`, `XServices` naming, new control-flow syntax,
external templates/styles).

1. **`AuthService`** (`providedIn: root`, signals): `login`, `register`,
   `logout`; stores JWT + refresh token in `localStorage` (guarded by
   `isPlatformBrowser`); exposes `isLoggedIn`/`currentUser` signals.
2. **HTTP interceptor**: attach `Authorization: Bearer` to API requests; on 401,
   attempt refresh once, else clear session and redirect to `/auth/login`.
   (Note the existing `api.interceptor` base-URL behavior and the `/api/Auth`
   route prefix — reconcile so auth calls hit the real routes.)
3. **Route guard**: protect the whole `/account/**` subtree (Account, Orders,
   Tracking, Address, Cards, Favorites); redirect unauthenticated users to login
   with a return URL.
4. **`AccountLayoutComponent`** — shared shell matching the screenshot:
   - Left vertical sidebar; active item = filled forest-green rounded pill with
     icon + chevron; inactive plain. Items: **Account, Orders, Address, Cards,
     Favorites**. The storefront's top navbar is unchanged (account-area only).
   - Right content card. Account-scoped SCSS tokens: cream background
     (~`#F7F4EE`), forest-green accent (~`#2C5545`), beige input fills, ~14px
     radii. Scoped to the account area so the rest of the site keeps its palette.
   - Account routes become children of this layout (e.g. `/account`,
     `/account/orders`, `/account/address`, `/account/cards`,
     `/account/favorites`, with tracking as `/account/orders/:orderNumber`).
5. **Panels** (each calls its real endpoint, with loading/empty/error states):
   - **My Info**: avatar-initial badge, Change Photo (stub/deferred), Full Name /
     Email / Phone fields, Save Changes → `PUT api/Profile`.
   - **My Orders**: list with status badges; row → **Tracking** detail page.
   - **My Favorites**: grid reusing `ProductCardComponent`; remove action.
   - **Address**: list + add/edit/delete form; set default.
   - **Cards**: list saved cards (brand + •••• last-4 + expiry); add form
     capturing cardholder/brand/last-4/expiry only; delete; set default.
6. **Checkout**: replace the simulated order with `POST api/Orders`, then route
   to the created order / My Orders.
7. **Login/Register**: wire forms to `AuthService`, show server validation and
   error messages, redirect on success.

## Phase 3 — Run & test responses and errors

Run backend then frontend; drive each flow in the browser (Playwright) and
record actual responses:

- **Auth**: register (success + duplicate-email 400), login (success + wrong
  password 400), logout clears session.
- **Guard**: visiting `/profile` logged-out → redirect to login (401 path).
- **Profile**: load, edit, save → persisted; invalid input → validation error
  shown.
- **Orders**: place an order via checkout → appears in My Orders; open Tracking;
  request a non-existent order number → 404 surfaced.
- **Favorites**: add/remove; duplicate add is idempotent.
- **Addresses**: add/edit/delete/set-default.
- **Cards**: add/list/delete/set-default; confirm no full card number is ever
  sent or stored (metadata only).

Report each response + error as observed, not assumed.

## Out of scope

- Password reset, email confirmation, social login.
- Real photo upload for the avatar (Change Photo is a placeholder).
- Payment processing (orders are Cash-on-Delivery / Pending).
- Re-theming the whole storefront to forest-green (account area only).
- Admin-side account features (`admin/` tree remains unrouted).

## Risks & mitigations

| Risk | Mitigation |
| --- | --- |
| Merge (Phase 0) is destructive | Backup already taken; both repos also on GitHub; verify before proceeding. |
| Unmigrated Orders/Addresses tables cause runtime 500s | Phase 1 adds the migration and applies it before frontend wiring. |
| Route-prefix/casing mismatch (`/api/Auth` vs frontend base) documented in CLAUDE.md | Reconcile explicitly in the interceptor step; verify in Phase 3. |
| Forest-green palette clashes with the site's teal | Scope new tokens to the account area only. |
| Scaffolding may hide more stubs than audited | Phase 1/2 begin with a per-file audit; treat unknowns as tasks, not assumptions. |
