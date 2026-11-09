# Frontend audit — 7 October 2026

## Scope and outcome

The public website and authenticated application retain their existing design.
React 19, JavaScript/JSX, React Router, ordinary CSS and Recharts remain the stack.
There is one AppShell, one AppSidebar and one AppTopbar. DashboardPage selects one
of four distinct role views; these are not duplicate shells or parallel routes.

## Fixes made in this audit

- Reduced desktop main padding from 36px to 32px. The fixed sidebar remains 256px.
- Fixed mobile navigation focus: the opening visibility transition previously
  left focus on the document body. Visibility now changes immediately while the
  slide animation remains. The trap also recovers if focus starts outside it.
- Added a page error boundary inside the shell, so rendering/lazy-loading errors
  leave navigation mounted. Navigating to another route resets the failed page.
- Removed the obsolete placeholder page and its imports. Invalid roles use the
  Access Denied view; unknown app paths use the application 404.
- Extracted the waypoint editor from the mission wizard, preserving its inputs,
  validation, route planning actions and state ownership.
- Replaced one unused conditional expression in the public registration wizard
  with equivalent if/else logic. Its UI and workflow are unchanged; lint is clean.

## Files created during this audit

- `src/components/app/AppPageBoundary.jsx`
- `src/components/app/WaypointEditor.jsx`
- `scripts/audit-frontend.cjs`
- `FRONTEND_AUDIT.md`

## Files modified during this audit

- `src/App.jsx`: removed obsolete placeholder fallback/import.
- `src/layouts/AppShell.jsx`: page error containment and mobile focus recovery.
- `src/styles/app-shell.css`: 32px desktop padding and immediate drawer visibility.
- `src/pages/app/DashboardPage.jsx`: explicit denied fallback.
- `src/pages/app/CreateMissionPage.jsx`: extracted waypoint editor.
- `src/components/EngineerRegistrationWizard.jsx`: behavior-preserving lint cleanup.
- `README.md`: current route implementation and audit instructions.

Removed: `src/pages/app/AppPlaceholderPage.jsx`. Earlier application work already
present in the working tree was retained. The shared mock-data export barrel is
used by tests and remains intentionally available.

## Routes verified

Public: `/`, `/thiet-bi`, `/thiet-bi/:id`, `/gio-hang`, `/dang-nhap`, `/dang-ky`.

Authenticated, under the same shell:

| Route | Implemented view |
| --- | --- |
| `/app/dashboard` | Active-role dashboard |
| `/app/farms` | Farms and zones |
| `/app/sensors` | Sensor list |
| `/app/sensors/:id` | Sensor details/readings/history |
| `/app/sensor-data` | Sensor analytics |
| `/app/sensor-data/compare-zones` | Engineer zone comparison |
| `/app/devices` | UAV and gateway inventory |
| `/app/missions` | Mission list |
| `/app/missions/create` | Eight-step planning wizard |
| `/app/missions/:id` | Mission monitoring |
| `/app/sync` | Offline synchronization batches |
| `/app/alerts` | Alerts and detail drawer |
| `/app/reports` | Role-aware report previews/CSV |
| `/app/users` | User and role management |
| `/app/settings` | Local system settings |

`/app` redirects to the shared dashboard. Unknown app paths retain the shell and
show a 404. Guests are redirected to login; disallowed roles see Access Denied.
Sensor and mission detail/create paths keep their parent sidebar link active.

## Permissions verified

| Role | Allowed capabilities |
| --- | --- |
| Farm Owner | Monitoring dashboard; read farms, sensors, data, missions and alerts; high-level reports/exports |
| Administrator | System dashboard; manage farms/zones, alerts, users and settings; read device/mission status and sensor data; full reports |
| UAV Device Operator | Operations dashboard; manage sensors/devices; plan/manage/monitor missions; retry sync; related alerts; technical sensor data; mission/device reports |
| Agricultural Engineer | Environmental dashboard; read farms/sensors/data/environmental alerts; compare zones; sensor/environment report previews without generation/export permissions |

Navigation, route guards, action visibility and local mutation helpers use the
central permission model. Multiple assigned roles are supported; activeRole is
the sole active permission set. The demo role selector is explicitly demo-only.
The Sensor Collection Engine has no human role or dashboard. TAKEOFF/LAND occur
only as planned waypoint data; there are no direct UAV flight controls.

## Verification

- `npm run build` passes.
- `npm run lint` passes with no warnings.
- `node --test tests/*.test.js`: all 67 tests pass. These cover linked fixtures,
  permission boundaries, CRUD constraints, mission validation/statuses, rejected
  records and idempotent sync, sensor analytics and report permissions/CSV.
- Browser audit covers all 15 app routes across four roles at 1440, 1280 and
  1024px; all 1,822 assertions pass. Checks include persistent DOM instances of sidebar/topbar, 256px sidebar,
  menu spacing/icon sizes, topbar styling, active parents, labels, page overflow,
  table scroll containers and chart dimensions.
- All eight wizard steps are checked at those desktop widths, including invalid
  altitude feedback and saving a valid mission. Public pages are checked for
  successful rendering and horizontal overflow at all three widths.
- Farm/user/sensor dialogs, alert drawers and operational-note dialogs are checked
  for initial focus, focus containment, Escape and focus restoration. Mobile
  navigation is checked at 768px and 390px, including closing on link navigation.
- Filtered empty tables and a deliberately failed lazy page load are exercised.
  Shared DataTable/ChartCard also expose loading, empty and error presentations;
  route Suspense handles asynchronous module loading.
- Mock records and relationship selectors remain centralized under `src/data/mock`.
  No duplicate sidebar implementation or unused page components remain. Scoped app
  CSS retains DM Sans and primary green `#176346` without public CSS overrides.

Browser checks use headless Microsoft Edge, not an exhaustive screen-reader or
cross-browser certification. No backend/network integration is simulated as real.

### Reproduce the browser audit

Start `npm run dev -- --host 127.0.0.1 --port 5190 --strictPort`. With Playwright
available in a separate tooling installation, run in PowerShell:

```powershell
$env:PLAYWRIGHT_MODULE = 'C:/path/to/tooling/node_modules/playwright'
node scripts/audit-frontend.cjs
```

Alternatively, omit PLAYWRIGHT_MODULE if it resolves normally. `AUDIT_URL` changes
the server URL and `AUDIT_BROWSER` changes the installed browser channel (default:
msedge). Mock authentication must be enabled, as it is by default in development.
The checks use isolated browser contexts and do not modify an existing session.

## Remaining backend integration points

1. Replace mockAuth/AuthProvider fixture sessions with backend login, JWT refresh,
   logout and server-enforced roles/farm scopes. Remove demo role overrides.
2. Replace OperationsProvider, ManagementProvider and farm workspace local mutations
   with API reads/writes, server validation, persistence, pagination and conflicts.
3. Connect mission progress, device telemetry, collection attempts and sync outcomes
   to backend events/polling. Keep flight control outside this web application.
4. Implement gateway authentication, offline upload, duplicate detection and
   per-record rejection responses on the backend; mock retries are not real uploads.
5. Persist settings, threshold defaults, alert history and notification delivery.
6. Replace fixed-snapshot analytics/report selectors with authorized server queries;
   PDF remains an explicitly labeled prototype. CSV is generated locally.
7. Connect public registration/review workflows separately from authorization.
   Demo directory edits do not create login credentials. Domain edits currently
   reset on reload; only the mock session/active role persist in localStorage.
