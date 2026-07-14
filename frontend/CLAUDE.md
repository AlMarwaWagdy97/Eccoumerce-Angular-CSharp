# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

- `npm start` / `ng serve` — dev server at `http://localhost:4200/` (default config: development, source maps, no optimization).
- `npm run build` / `ng build` — production build by default (output hashing, budgets enforced) to `dist/`.
- `npm run watch` — incremental development build (`--watch --configuration development`).
- `npm test` / `ng test` — run unit tests with **Vitest** (builder `@angular/build:unit-test`, jsdom). There is no Karma/Jasmine.
- `npm run serve:ssr:Ecommerce` — run the built SSR server (`node dist/Ecommerce/server/server.mjs`).

Run a single test with Vitest's filtering, e.g. `ng test -- -t "<test name>"` or by file path.

> A full `ng build` also **prerenders** routes, which executes services that call the backend. If the .NET API isn't running you'll see `HttpErrorResponse` / `ECONNREFUSED` / self-signed-cert logs during prerender — these are non-fatal; the build still completes because components handle load errors with empty/loading states.

## Architecture

Angular 22 standalone-component app (no NgModules) with **SSR enabled** (`outputMode: server`, Express entry [src/server.ts](src/server.ts)). Bootstrapping splits into [app.config.ts](src/app/app.config.ts) (browser) and [app.config.server.ts](src/app/app.config.server.ts) (server, merged). The router uses **`withComponentInputBinding()`**, so route params are delivered as component `input()` signals (see below).

### Reactivity model — signals first

New/feature code is **signal-based**, not `subscribe`-into-fields or heavy RxJS:
- Components use `inject()`, `signal()`, `computed()`, `effect()`, and `input()` (never constructor DI or `@Input()` in new code).
- **Route params arrive as inputs**: e.g. `slug = input.required<string>()` on `ProductDetailsComponent` (route `products/:slug`), `id = input.required<string>()` on `SingleCategoryComponent` (route `categories/:id`). An `effect()` re-fetches when the param changes. Param inputs are always **strings** — convert (`Number(this.id())`) before use.
- Services expose either **`Observable`** (HTTP-backed: `ProductServices`, `CategoryServices`) or **readonly signals** (client state: `CartServices` via `signal.asReadonly()` + `computed`). Match the existing style of whichever service you touch.

### SSR render modes — [app.routes.server.ts](src/app/app.routes.server.ts)

Default is `RenderMode.Prerender` for `**`, but any route whose content **cannot be known at build time** is explicitly listed as `RenderMode.Client`:
- `products/:slug`, `categories/:id` — dynamic params needing a backend fetch (can't enumerate at build).
- `cart`, `checkout` — depend on client-only state (`localStorage`); prerendering them would cause hydration mismatches.

**When you add a route that reads route params from the backend or reads `localStorage`/browser-only state, add it here as `Client`** or the prod build's prerender step will fail or hydrate incorrectly.

### Two parallel feature trees: `site/` and `admin/`

[src/app/site/](src/app/site/) (storefront) and [src/app/admin/](src/app/admin/) (admin panel) are **independent mirrored trees**, each with its own `core/services/`, `features/auth|layouts|pages/`, and `shared/interface|scss/`. They intentionally duplicate rather than share — e.g. `ProductServices` exists in both with different shapes. When changing one side, check whether the mirrored file on the other side needs the same change; don't assume they're shared.

> **Note:** [app.routes.ts](src/app/app.routes.ts) currently only wires up the `site/` tree. The `admin/` components exist but are not yet routed.

### Within the `site/` tree

- `features/layouts/` — `MainLayoutComponent` / `AuthLayoutComponent` are the two route shells (each hosts a `<router-outlet>`); navbar/footer/not-found live here. The navbar reads `CartServices.count()` for its badge.
- `features/pages/` — routed pages. Note the route split: `products` = **Shop** (list, `ProductsComponent`) vs `products/:slug` = **details** (`ProductDetailsComponent`); `categories` = grid vs `categories/:id` = `SingleCategoryComponent`.
- `shared/component/` — reusable presentational components (site only): `hero`, `category-slider`, and **`product-card`** (`ProductCardComponent`, `input.required<ProductInterface>()`) — the single clickable card reused by Home, Shop, Single-Category, and Related sections; it `routerLink`s to `/products/:slug`. Prefer reusing it over hand-writing card markup.
- `shared/interface/` — API/data model interfaces.

### Cart / checkout

- **`CartServices`** ([site/core/services/cart-services.ts](src/app/site/core/services/cart-services.ts)) is a `providedIn: 'root'` **client-side** store: signal of `CartItemInterface[]`, persisted to `localStorage` under `shopdemo_cart`, **guarded by `isPlatformBrowser`** (returns `[]` on the server). It exposes `add/increment/decrement/remove/clear` and computed `count`, `subtotal` (original prices), `discount` (sale savings), `shipping` (free over $50, else $5.99), `total`.
- There is **no backend cart or orders endpoint**. `CheckoutComponent.placeOrder()` validates a reactive form, then **simulates** the order (`cart.clear()` + confirmation). The `TODO` there marks the single place to POST the order when an endpoint exists.

### HTTP / API layer

- [api.interceptor.ts](src/app/api.interceptor.ts) prefixes every **relative** request URL with `Environment.apiUrl`; absolute URLs (`http(s)://`) pass through. Services call paths like `/Products`, `/Categories` — not full URLs.
- Backend is an external .NET API (`https://localhost:7297/api` in dev — [environment.ts](src/environments/environment.ts); `api.shopdemo.com/api` in prod). Import the config object as `Environment` (capital E).
- **List** endpoints return `ApiResponseInterface<T> = { statusCode, message, data: T[] }`; services `.pipe(map(r => r.data))` to unwrap.
- **Detail** endpoints (`getProductById`, `getProductBySlug`) are typed to `ProductDetailsInterface` and **defensively unwrap**: `map(res => (res?.data ?? res))` — the backend may return the object directly or wrapped. `ProductDetailsInterface` is a richer shape than the list `ProductInterface` (adds `images[]`, `stockQuantity`, `reviews[]`, `rating`, `categoryTitle`).
- ⚠️ `getProductBySlug()` assumes `GET /products/slug/{slug}` — **verify/adjust to the real backend route** (it's a single line in [product-services.ts](src/app/site/core/services/product-services.ts)).
- Endpoint **casing is inconsistent** across the codebase (`/Products` vs `/products`, `/Categories/${id}`) — match the actual backend route, don't assume lowercase.

## Conventions

- **Angular 22 file naming**: components/services use bare names, NOT the legacy `.component.ts` suffix — e.g. `home.ts` + `home.html` + `home.scss`, class `HomeComponent`. Services are named `XServices` (plural, e.g. `ProductServices`, `CartServices`).
- Components are standalone with explicit `imports: [...]`; templates and styles are always external files (`templateUrl`/`styleUrl`), `inlineStyleLanguage: scss`. Remember to add `RouterLink`, `CurrencyPipe`, `ReactiveFormsModule`, etc. to `imports` when the template uses them.
- Templates use the new control-flow syntax (`@if`/`@for`/`@empty`), not `*ngIf`/`*ngFor`.
- Styling: **SCSS**, plus global **Bootstrap 5** and **Font Awesome** (wired via `angular.json` styles/scripts, not imports). `flowbite` is also a dependency. Bootstrap Icons (`bi bi-*`) are used throughout templates. Shared design tokens live in each tree's `shared/scss/_variables.scss` (teal-based palette; `.btn-teal-action`, `.text-teal`, `fs-7` are recurring custom classes). Note: `src/styles.scss` still uses `@import` (deprecated in Dart Sass, builds with a warning).
