# React + Vite

## Application UI primitives

The 13 reusable authenticated-app components live in `src/components/app/ui`.
See [component APIs and usage](src/components/app/ui/README.md) for table/card
composition, accessibility, state handling, and the scoped `--ui-*` design tokens.
Run `npm run test:ui` for status and telemetry presentation checks.

## Routing foundation

`src/main.jsx` supplies `BrowserRouter`; `src/App.jsx` declares the routes.
Existing public pages remain in `src/pages` and retain their CSS and forms:
`/`, `/thiet-bi`, `/thiet-bi/:id`, `/gio-hang`, `/dang-nhap`, and `/dang-ky`.
Unknown URLs render the 404 page. Product IDs that do not exist retain the
existing product-not-found view.

Application paths are listed in `src/config/appRoutes.js` beneath `/app`:
`dashboard`, `farms`, `sensors`, `sensors/:id`, `sensor-data`, `sensor-data/compare-zones`, `devices`,
`missions`, `missions/create`, `missions/:id`, `sync`, `alerts`, `reports`,
`users`, and `settings`. These render placeholders in `AppShell`; `/app`
redirects to `/app/dashboard` after authentication.

`AuthProvider` supplies the current user and login/logout actions. `ProtectedRoute`
redirects guests to `/dang-nhap`, preserving the requested location (including
query and hash) in `location.state.from`. Successful login returns to that `/app`
location, or `/app/dashboard` for a direct login.

### Mock accounts for frontend testing

Run `npm run dev`, open `/dang-nhap`, and use one of these public demo accounts.
All four passwords are `Demo@123`.

| Role | Email |
| --- | --- |
| ADMINISTRATOR | `admin@smartfarm.test` |
| UAV_DEVICE_OPERATOR | `operator@smartfarm.test` |
| FARM_OWNER | `user@smartfarm.test` |
| AGRICULTURAL_ENGINEER | `engineer@smartfarm.test` |

Fixtures live in `src/data/mock/accounts.js`. These are the four human UI roles
for the frontend demo, not a migration of backend roles. The app header shows
the active role; the profile dropdown includes logout and a clearly labeled
**Demo only · Preview role** selector for all four role experiences.

The mock session stores only the fixture ID and active preview role in
`sessionStorage`, survives reloads in the same tab, and is removed on logout.
Existing sessions using the old fixture-ID key still work. Passwords and fabricated JWTs are not
stored in the session. Invalid credentials display an error. Registration forms
remain unchanged and do not create additional mock accounts.

Mock login is enabled by default in Vite development mode and disabled by default
in production builds. Set `VITE_ENABLE_MOCK_AUTH=false` to disable it locally.
For a deliberate demo build, set `VITE_ENABLE_MOCK_AUTH=true` at build time (for
example in `.env.local`) and rebuild; remove it before building for real users.
This is a frontend simulation, with no backend API calls. When integrating the
backend, replace `src/services/mockAuth.js` and the provider's session handling
with real authentication; enforce authorization on the backend.

Internal page navigation uses `Link`/`NavLink`/`useNavigate`. Native fragment-only
anchors and external contact links remain intact. `RouteScroll` handles section
targets after client-side page navigation and starts other pages at the top.

For production hosting, configure the web server to serve `index.html` for
frontend URLs that do not match static files, including nested `/app/*` and
`/thiet-bi/*` URLs. Keep API and asset requests outside that fallback. Vite's
development and preview servers already support frontend deep links.

Validation: `npm run build`, `npm run lint`, and `npm run test:permissions`.

### Frontend permissions

- `src/config/roles.js`: the four human roles and active-role validation.
- `src/config/permissions.js`: action permissions and alert visibility scope.
- `src/config/navigation.js`: the single navigation list filtered by permissions.
- `src/config/appRoutes.js`: required permissions for each app route.
- `src/context/AuthContext.jsx` and `AuthProvider.jsx`: session context, `can`,
  `canAny`, `activeRole`, `alertScope`, and role switching.
- `src/components/app/ProtectedRoute.jsx`: guest redirects and Access Denied for
  authenticated users who cannot access the requested page.

Users have `roles: []` (assigned memberships) and one `activeRole`. Permissions
come only from the active role, never the union of assigned roles. `switchRole`
accepts assigned roles only. `switchDemoRole` is an explicit override gated by
mock mode and `user.isMock`; it preserves the assigned roles and only previews
another human role. It is not a production role-assignment mechanism.

| Module/action | Farm Owner | Administrator | UAV / Device Operator | Agricultural Engineer |
| --- | --- | --- | --- | --- |
| Dashboard | View | View | View | View |
| Farms & Zones | Read | Manage | — | Read |
| Sensor Nodes | Read | — | Manage | Read |
| Sensor Data | View | View | — | View + Compare Zones |
| UAVs & Gateways | — | View status | Manage | — |
| Missions | Read | View status | Create / manage / monitor | — |
| Data Sync | — | — | Manage | — |
| Alerts | Read | Manage | Related only | Read |
| Reports | View / generate | View / generate | — | Read |
| Users & Roles / Settings | — | Manage | — | — |

Sidebar, search results, and the Settings profile link follow the same rules.
Read access never implies write access: `/app/missions/create` is operator-only;
`/app/sensor-data/compare-zones` is an engineer-only placeholder. Switching role
on an unauthorized page shows Access Denied within the existing shell, with a
Dashboard link. The sidebar and topbar remain mounted.

Future page actions must use checks such as `can(PERMISSIONS.FARMS_MANAGE)` and alert data must respect
`alertScope` (`all`, `related`, or `null`). There are no domain operations or live
alert records yet. The Sensor Collection Engine is not in the human role list
and receives no navigation, permissions, or dashboard. Real authorization and
record ownership checks must be implemented when integrating the backend.

### Shared application shell

All `/app` pages, including application 404s, share one `AppShell` layout route.
`AppSidebar` and `AppTopbar` stay mounted while its `Outlet` changes pages. Shell
styles live in `src/styles/app-shell.css` and are scoped to the authenticated UI.
The 256px fixed desktop sidebar becomes a single overlay drawer below 1024px,
with Escape/backdrop dismissal, focus trapping, and an inert background. Both
the sidebar navigation and the main content scroll independently.

`src/config/navigation.js` defines the sidebar and page-search destinations.
Nested mission and sensor URLs keep their parent navigation item active.
The farm selector uses `src/data/mock/farms.js`, preserves its selection during
navigation, and exposes `currentFarm` through Outlet context for future pages.
Search currently finds workspace pages. Connection status shows demo/offline
state, and notifications have an empty state; these do not simulate live data.

### Centralized domain fixtures

The Farm Owner preview at `/app/dashboard` uses the existing shared shell and farm
selector. It includes farm-scoped indicators, a schematic SVG map, environment
trends, mission progress, unresolved alerts, and recent activity. All navigation
is read-only. Operator and engineer dashboards remain placeholders. Totals come from
the linked fixtures (Green Valley: 11/12 online sensors, one active mission, six
unresolved alerts); times use the fixed demo snapshot. The greeting uses the
signed-in user's name. Mission details still use the existing placeholder route.

The Administrator dashboard shows all-farm user/device/alert/mission/sync indicators,
system status, farm coverage, device health, mission status, and recorded user activity.
It exposes monitoring links without mission creation or UAV controls.

`/app/farms` is one shared page. `FARMS_MANAGE` enables the four add/edit dialogs and
confirmed deletion; owners and engineers see the same hierarchy and details read-only.
Local farm/zone state lives in the existing shell, survives internal navigation and
demo role switching, and resets on refresh/logout. Edits update the topbar selector
and dashboard farm summaries. Seed fixtures are never mutated. Linked sensors,
zones, devices, and mission history prevent deletion until dependencies are removed;
new empty records can be deleted. Maps are schematic, not surveyed polygons.
Run `npm run test:farms` for validation, deletion safeguards, permissions, and state tests.

Linked farm, sensor, device, mission, sync, alert, notification, and user fixtures
are exported from `src/data/mock/index.js`. Use the shared selectors for relationship
lookups and progress. See the [mock data guide](src/data/mock/README.md) for the fixed
snapshot, accepted versus buffered readings, and frontend status vocabulary.
Run `npm run test:mock-data` to check fixture integrity. The existing four demo
login credentials are preserved; public pages and shell styling are unchanged.

Router reference: [React Router declarative routing](https://reactrouter.com/start/declarative/routing).

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend using TypeScript with type-aware lint rules enabled. Check out the [TS template](https://github.com/vitejs/vite/tree/main/packages/create-vite/template-react-ts) for information on how to integrate TypeScript and Oxlint's TypeScript related rules in your project.
