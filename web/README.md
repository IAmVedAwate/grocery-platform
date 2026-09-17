# web

Next.js (App Router) + TypeScript client, scaffolded via `create-next-app` (Tailwind CSS, ESLint, `src/` layout, `@/*` import alias). TanStack Query and shadcn/ui are added when real UI work begins in Phase 1 — not part of this scaffold.

Navigation: Dashboard, Products, Inventory, Purchases, Suppliers, Sales/Checkout, Customers, Reports, Documents, AI Assistant, Users, Audit, Settings — rendered per the authenticated user's actual permissions. See [docs/PRD.md §12](../docs/PRD.md#12-web-application-requirements).

## Development

```bash
npm run dev      # start the dev server at http://localhost:3000
npm run build    # production build
npm run lint     # ESLint
```

`NEXT_PUBLIC_API_BASE_URL` (see `../.env.example`) points the client at the backend API.

Not yet wired to the backend or any real page beyond the default scaffold — first real pages land in Phase 1 (Slice 0) per [docs/ROADMAP.md](../docs/ROADMAP.md).
