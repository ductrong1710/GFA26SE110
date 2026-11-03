# Smart Farm frontend

React application built with Vite. The authenticated workspace uses the ASP.NET
backend as its source of truth; it does not sign in with demo accounts or load
mock records into production pages.

## Run locally

CI runs lint, tests and the production build on pushes and pull requests to
`FrontEnd`. See [CI details](docs/CI.md).

Copy `.env.example` to `.env.local`, set `VITE_API_BASE_URL` to the backend API
origin (the development default is `http://localhost:5043`), and start the
backend with its PostgreSQL configuration. Trust the local HTTPS certificate
and allow the frontend origin in the backend CORS policy. Then run:

```sh
npm ci
npm run dev
```

Administrators create user accounts. The frontend maps supported backend roles
to application roles and only allows switching between roles assigned to the
signed-in user. Access tokens and user information are managed through
`src/services/api/authStorage.js`; refresh-token session handling and the
Axios client live in `src/services/api/`. API URL and timeout defaults are in
`src/config/api.js`.

The shared Axios client adds the access token, unwraps API response data,
refreshes sessions when possible, clears auth state on an unrecoverable 401,
and shows React Toastify notifications for write requests. `ToastContainer`
is mounted in `src/main.jsx`; use `src/utils/toast.js` for explicit UI notices.

## Backend-backed pages

Farm and zone records, users, sensor nodes and channels, thresholds, devices,
missions, readings, alerts, notifications, dashboards, and reports are loaded
from backend endpoints. Pages do not substitute fixture data when a request
fails; they show loading, empty, or error states.

Some prototype features do not have a supported human-facing backend API and
are disabled: global settings, browser-managed device synchronization/retry,
self-registration, and commerce/cart flows. Gateway data synchronization is
performed by the physical device. Farm and zone deletion is not offered because
the backend integration does not support it.

The fixtures in `src/data/mock/` remain for isolated unit tests and data-shape
checks only. They are not imported by the authenticated application pages.

## Project structure

- `src/config/`: API settings, routes, navigation, roles, and permissions.
- `src/services/api/`: Axios client, auth storage, session, adapters, API
  resources, and data hooks.
- `src/context/`: authentication and backend-backed page state.
- `src/pages/` and `src/components/`: routes and UI.
- `src/utils/toast.js`: React Toastify helper.

The reusable authenticated UI primitives are documented in
`src/components/app/ui/README.md`. `src/config/appRoutes.js` lists the `/app`
routes; `src/App.jsx` defines public and protected routes.

## Validation

```sh
npm test
npm run lint
npm run build
```

Use `npm run test:mock-data` to validate the isolated fixture selectors.

### Maintenance schedules
Administrators can manage `/app/maintenance-schedules`: create/edit/disable recurring service policy, choose a device type or an unreturned rental device, set a responsible technician and inspect generated work. List/search and rental selection are paginated. Conflicts preserve the draft until explicit reload; disabling overdue work does not require a new date. Occurrence history shows the policy/device recorded for each visit and links to its service job. All writes use the backend and its current revision; server authorization remains authoritative. Actual browser end-to-end verification is still pending.

### Crop condition support
`/app/crop-support` is available to Owner, Agricultural Engineer and Admin. Owner submits farm/optional zone/crop/symptoms, bounded evidence images and up to50 attached sensor channels. Catalog loading follows all API pages; changing farm/zone clears incompatible sensor selections. Detail shows original evidence, communication history, status and paged live sensor context. Assigned Engineer can claim/request information/recommend/resolve; reporter Owner can reply while awaiting information; Admin monitors without processing controls. API checks remain authoritative. Failed writes preserve notes/images with explicit reload, and mutation/image-reading state disables competing controls. Uses real crop-support endpoints; no AI diagnosis or fabricated survey/job. Actual browser end-to-end verification remains pending.

### Browser verification with a disposable database
On Windows with PostgreSQL binaries, .NET EF tooling and Microsoft Edge installed, run from `Code/backend`:

```powershell
./scripts/Test-Postgres.ps1 -PostgresBin 'D:/IDECode/PostgreSQL/18/bin' -Configuration Release -NoRestore -BrowserTests
```
The existing PostgreSQL harness creates a fresh loopback cluster and builds the backend. The browser runner migrates only that disposable database, starts the actual API on5189 and Vite on5179, disables email delivery, and runs Playwright using Edge. Both ports must be free. Fixture accounts/farm are prepared through API using the retained sample-admin migration; the actual Owner/Engineer business actions use browser forms and separate sessions. Cleanup stops API/Vite/PostgreSQL; application databases are untouched. Failure screenshots/traces stay in ignored `test-results/`. Current browser coverage: Flow5 crop report -> information request -> reply -> recommendation -> resolution/history. Other business chains and image-upload/browser variants remain pending.
