# Frontend CI

`.github/workflows/frontend-ci.yml` runs on pushes and pull requests to `FrontEnd`
(case-sensitive), and supports manual dispatch. The repository root on this branch
contains `package.json`; the workflow does not use a nested frontend directory.

The Ubuntu runner uses Node.js 22, caches npm downloads using `package-lock.json`,
and runs `npm ci`, `npm run lint`, `npm test`, then `npm run build`. Tests include
the existing user-management suite plus boundaries, gateway credentials and Tello.
A failed command fails the job. Successful builds upload `dist/` for 14 days.

No backend, database or GitHub Secrets are needed. This workflow does not deploy.
Push the resolved merge and workflow to activate CI. Manual dispatch requires the
workflow on the repository default branch. Optionally require the `Lint, test and
build` check in GitHub branch protection after its first run.
