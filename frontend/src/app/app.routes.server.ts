import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  // Dynamic routes depend on backend data that isn't available at build time,
  // so render them on the client instead of prerendering.
  {
    path: 'products/:slug',
    renderMode: RenderMode.Client
  },
  {
    path: 'categories/:id',
    renderMode: RenderMode.Client
  },
  // Cart & checkout depend on client-only state (localStorage), so render on the client.
  {
    path: 'cart',
    renderMode: RenderMode.Client
  },
  {
    path: 'checkout',
    renderMode: RenderMode.Client
  },
  // The account area requires a logged-in user (localStorage token) and always
  // fetches from the backend, so none of it can be prerendered.
  {
    path: 'account',
    renderMode: RenderMode.Client
  },
  {
    path: 'account/orders',
    renderMode: RenderMode.Client
  },
  {
    path: 'account/orders/:orderNumber',
    renderMode: RenderMode.Client
  },
  {
    path: 'account/address',
    renderMode: RenderMode.Client
  },
  {
    path: 'account/cards',
    renderMode: RenderMode.Client
  },
  {
    path: 'account/favorites',
    renderMode: RenderMode.Client
  },
  // Legacy redirect with a dynamic param — still needs its own render-mode
  // entry even though app.routes.ts only uses it to redirect into /account/**.
  {
    path: 'orders/:orderNumber/tracking',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Prerender
  }
];
