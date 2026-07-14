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
  {
    path: 'orders/:orderNumber/tracking',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Prerender
  }
];
