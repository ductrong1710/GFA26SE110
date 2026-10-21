# Geographic Farm/Zone boundaries

The existing Farms & Zones dialog now supports drawing with Leaflet-Geoman and manual coordinate entry. Enter the four default rows and choose Apply Coordinates, or draw a polygon and edit its vertices. GeoJSON boundary state is canonical; unfinished manual edits disable Save. Applying valid input updates the polygon, centroid and Turf area. Map edits update manual rows without replacing the active Leaflet layer. Clear Boundary removes geometry while retaining the legacy center.

Zones show an uneditable parent Farm outline. Frontend Turf difference checks the entire polygon, including edges across concave boundaries. Backend NetTopologySuite independently validates the polygon and containment. Shared edges are allowed. Missing parent boundaries are explicitly reported as unavailable containment validation. A farm boundary update cannot exclude existing zone polygons.

GeoJSON is longitude-first, closed, one exterior ring, three or more distinct vertices. Manual UI starts with four points; drawn polygons with additional vertices round-trip through all coordinate rows. Holes, zero area, invalid ranges and self-intersections are rejected. Legacy records retain center-only fields and the labeled schematic fallback; no polygon is fabricated. Geographic views show farm/zone polygons and sensor coordinates, with accessible zone selection buttons. No browser geolocation or tile API key is used.

## Deployment

Install frontend dependencies with `npm ci`. Apply backend migration `20261020085024_GeographicBoundaries` before using the new API binaries. See backend DATABASE.md for the command and connection configuration. No application database has been changed by this implementation; integration tests use a temporary isolated PostgreSQL cluster.

## Changed files

Frontend:
- package.json, package-lock.json
- src/utils/geo.js
- src/components/app/BoundaryEditorMap.jsx, GeographicFarmMap.jsx, FarmRecordDialog.jsx, FarmZoneMap.jsx, FarmOverviewMap.jsx
- src/styles/boundary-map.css
- src/pages/app/FarmsPage.jsx, FarmOwnerDashboard.jsx
- src/services/api/adapters.js, useFarmWorkspace.js
- src/data/mock/farmWorkspace.js
- tests/geo.test.js, boundary-editor.test.jsx, api-adapters.test.js, farm-workspace.test.js

Backend:
- src/FarmMonitoring.Application/FarmMonitoring.Application.csproj
- src/FarmMonitoring.Application/Features/Farms/BoundaryGeometry.cs, FarmContracts.cs, FarmService.cs
- src/FarmMonitoring.Application/Interfaces/IFarmRepository.cs
- src/FarmMonitoring.Domain/Entities/Farm.cs, Zone.cs
- src/FarmMonitoring.Infrastructure/Persistence/Configurations/FarmConfiguration.cs, ZoneConfiguration.cs
- src/FarmMonitoring.Infrastructure/Persistence/Repositories/FarmRepository.cs
- src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261020085024_GeographicBoundaries.cs, corresponding Designer and AppDbContextModelSnapshot.cs
- tests/FarmMonitoring.UnitTests/BoundaryTests.cs
- tests/FarmMonitoring.IntegrationTests/FarmZoneTests.cs, BoundaryMigrationTests.cs, FarmMembershipMigrationTests.cs
- scripts/Test-Postgres.ps1 (optional build configuration for avoiding running Debug API DLL locks)
- API.md, DATABASE.md, BUSINESS_RULES.md

## Verification commands

Frontend: `npm test`, `npm run lint`, `npm run build`.
Backend on Windows: `./scripts/Test-Postgres.ps1 -PostgresBin <PostgreSQL-bin-directory> -Configuration Release`.
The production build emits Vite's size advisory for the main JS chunk. The feature does not implement mission or sensor geofencing.

## Current location

The boundary editor and geographic overview provide a Current location button. Location is requested only on a click, with browser permission, a 15-second timeout and no cached fix. The map shows a blue position marker, accuracy radius and numeric coordinates; it does not save the location or change any boundary/centroid. Location requires HTTPS or localhost. Permission denial, timeout or unsupported browsers leave manual editing available. Closing the view ignores any late response. This uses the browser's reported location, whose accuracy varies by device.
