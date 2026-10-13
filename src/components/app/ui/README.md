# Authenticated application primitives

Import named exports from `src/components/app/ui`, or import an individual
component. Each entry loads the scoped stylesheet it needs. There are no new
UI frameworks, chart libraries, network requests, or domain pages.

| Component | Main props |
| --- | --- |
| `PageHeader` | `title`, `description`, `eyebrow`, `breadcrumbs`, `actions` |
| `StatCard` | `label`, `value`, `unit`, `icon`, `description`, `trend`, `footer` |
| `MetricCard` | StatCard props plus `status`, `lastUpdated`, and optional children in the footer |
| `StatusBadge` | `status`, optional localized `label` |
| `AlertSeverityBadge` | `severity`, optional `label`; shares StatusBadge conventions |
| `DeviceStatus` | `status`, optional `name`, `lastSeen` display text |
| `BatteryIndicator` | numeric `value` (0–100), `charging`, `label`, `showValue` |
| `ProgressBar` | numeric `value`, `max` (default 100), `label`, `tone`, `showValue` |
| `FilterBar` | labeled controls as children, `actions`, `onSubmit`, accessible `label` |
| `DataTable` | `columns`, `rows`, `rowKey`, `caption`, `loading`, `error`, `emptyState`, `sort`, `onSortChange`, `footer` |
| `EmptyState` | `title`, `description`, `icon`, `actions` |
| `SectionCard` | `title`, `description`, `actions`, `children`, `footer` |
| `ChartCard` | SectionCard props plus chart children, `caption`, `loading`, `error`, `errorAction` |

Roots accept `className` and normal HTML/ARIA attributes. A SectionCard title is
an `h2` with a unique accessible ID. PageHeader supplies the page's `h1`.
Optional action props accept React nodes so callers keep control of navigation,
permissions, and event handlers. Use buttons with explicit `type`.

## Tokens and layout

`src/styles/app-tokens.css` defines `--ui-*` variables only on `.app-shell` and
`.app-ui`. `app-ui.css` styles only the new component classes. It does not change
the sidebar, topbar, existing public components, or global CSS variables.

Tokens include the primary `#176346`, page `#f6f8f7`, white cards, sidebar
`#102b25`, semantic colors, 14px card radius, 1px borders, spacing, control height,
table padding, and an optional subtle shadow. Cards use borders without shadows
by default. The desktop/tablet page-padding tokens are 32px/24px (16px on mobile).

Use `app-ui-stack` for vertical spacing and `app-ui-grid` for responsive cards.
The existing AppShell already owns its page padding; do not add a second padded
wrapper inside it. For standalone content, `app-ui app-ui-page` supplies the
page surface and responsive padding. `app-ui-button` and
`app-ui-button--primary` style caller-provided actions inside these primitives.

## Status and telemetry conventions

`uiState.js` is the single status mapping for badges and device/severity states:

| Tone | Statuses |
| --- | --- |
| Green / `success` | ACTIVE, ONLINE, SUCCESS, COMPLETED |
| Blue / `info` | READY, IN_PROGRESS, SYNCING, INFO |
| Amber / `warning` | PENDING, PARTIAL, WARNING, LOW_BATTERY, MAINTENANCE |
| Red / `danger` | FAILED, ERROR, CRITICAL, OFFLINE |
| Gray / `neutral` | DRAFT, INACTIVE, CANCELLED; unknown statuses |

Status keys accept case-insensitive strings, spaces, or hyphens. Unknown values
never imply success. Visible text accompanies color. Battery level is red at
0–10%, amber above 10 through 20%, and green above 20%; these are presentation
defaults, not backend alert-generation rules. Charging does not conceal a low
battery. Battery and progress measurements clamp to their bounds; missing,
non-finite, and non-numeric values remain unavailable instead of becoming zero.
Pass numbers rather than numeric strings. StatCard/MetricCard also preserve zero
and show `—` for absent or non-finite numeric values.

Progress exposes a labeled `progressbar` and battery a labeled `meter`; unknown
battery values have a text alternative instead of a fabricated measurement.
Pass localized display strings to `lastUpdated`/`lastSeen`; components do not
invent timestamps or apply timezone conversions.

## Controlled table and filter example

```jsx
import { useState } from 'react'
import { DataTable, FilterBar, DeviceStatus } from './components/app/ui'

const columns = [
  { key: 'name', header: 'Sensor node', sortable: true },
  { key: 'status', header: 'Status', render: (value) => <DeviceStatus status={value} /> },
  { key: 'readings', header: 'Readings', align: 'end' },
]

function SensorTable({ rows }) {
  const [query, setQuery] = useState('')
  const [sort, setSort] = useState({ key: 'name', direction: 'asc' })
  const visibleRows = rows
    .filter((row) => row.name.toLowerCase().includes(query.trim().toLowerCase()))
    .sort((a, b) => a.name.localeCompare(b.name) * (sort.direction === 'asc' ? 1 : -1))

  return <div className="app-ui-stack">
    <FilterBar>
      <label>Find sensor<input type="search" value={query} onChange={(event) => setQuery(event.target.value)} /></label>
    </FilterBar>
    <DataTable caption="Sensor nodes" columns={columns} rows={visibleRows}
      sort={sort} onSortChange={setSort} />
  </div>
}
```

Columns require unique `key` and `header`; `render(value, row)` handles rich
cells. Default cells display primitives or `—`. `rowKey` defaults to `id`, or
accepts a function returning a unique stable key. `caption` is an accessible
string. Sorting is controlled: a sortable header emits `{ key, direction }`;
the caller sorts or requests rows. The table never silently filters, paginates,
fetches, or performs domain actions. Provide pagination controls as `footer`.
The scrollable table region is keyboard focusable on narrow screens.

`loading` takes precedence over `error`, followed by empty and populated states.
Pass a user-facing string to `error` and an optional retry button as `errorAction`.
FilterBar prevents native page reloads; the caller controls its inputs and handles
the optional submit event. Label every input/select.

## Cards and charts

`trend` is `{ value: '+4%', label: 'versus last period', tone: 'success' }`;
the caller decides whether an increase is good or bad. Icons accept React nodes.
ChartCard wraps a supplied chart in a figure and optional caption, with loading,
error, and empty states. Provide accessible descriptions on the actual SVG,
canvas, or chart library element; the wrapper does not label arbitrary data for
you. No charts, readings, or datasets are fabricated by these components.

Validation: `npm run test:ui`, `npm run build`, `npm run lint`.
