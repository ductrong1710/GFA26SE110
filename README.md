# Smart Farm frontend

React application built with Vite. The authenticated workspace uses the ASP.NET
backend as its source of truth; it does not sign in with demo accounts or load
mock records into production pages.

## Run locally

Copy `.env.example` to `.env.local`, set `VITE_API_BASE_URL` to the backend API
origin (the development default is `https://localhost:7043`), and start the
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
