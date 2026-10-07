import { useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useOperations } from '../../context/OperationsContext'
import { useManagement } from '../../context/ManagementContext'
import { getReportTypes, canExportReport } from '../../config/reports'
import { PERMISSIONS } from '../../config/permissions'
import { buildReport, REPORT_DEFAULT_RANGE } from '../../data/mock/reportSelectors'
import { reportCsv } from '../../data/mock/reportCsv'
import { analyticsTypes } from '../../data/mock/sensorAnalytics'
import { PageHeader, SectionCard, ChartCard, FilterBar, StatCard, DataTable, EmptyState, StatusBadge } from '../../components/app/ui'
import EnvironmentalChart from '../../components/app/EnvironmentalChart'
import '../../styles/analytics.css'
import '../../styles/reports.css'

function ReportTable({ item, pageSize = 20 }) {
  const [page, setPage] = useState(0)
  const pages = Math.max(1, Math.ceil(item.rows.length / pageSize))
  return <SectionCard title={item.title}><DataTable caption={item.title} rows={item.rows.slice(page * pageSize, (page + 1) * pageSize)} columns={item.columns.map((column) => ({ ...column,
    render: (value) => ['status', 'severity'].includes(column.key) ? <StatusBadge status={value} /> : value === null || value === undefined ? 'Unavailable' : value,
  }))} emptyState={<EmptyState title="No matching records" description="Try another farm, zone or date range." />} footer={pages > 1 ? <div className="analytics-pagination"><span>{item.rows.length} rows · Page {page + 1} of {pages}</span><button className="app-ui-button" disabled={page === 0} onClick={() => setPage(page - 1)}>Previous</button><button className="app-ui-button" disabled={page === pages - 1} onClick={() => setPage(page + 1)}>Next</button></div> : `${item.rows.length} rows`} /></SectionCard>
}

function ReportsWorkspace() {
  const { activeRole, can } = useAuth()
  const { operations } = useOperations()
  const { management } = useManagement()
  const { currentFarm, farmWorkspace } = useOutletContext()
  const types = getReportTypes(activeRole)
  const [filters, setFilters] = useState({ ...REPORT_DEFAULT_RANGE, farmId: currentFarm.id, zoneId: '', type: types[0]?.id ?? '', metricId: '1' })
  const [preview, setPreview] = useState(null)
  const [revision, setRevision] = useState(0)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const change = (values) => { setFilters({ ...filters, ...values }); setError(''); setNotice('') }
  const dirty = preview && JSON.stringify(filters) !== JSON.stringify(preview.filters)
  const generate = (event) => {
    event.preventDefault()
    try { setPreview(buildReport(activeRole, filters, operations, farmWorkspace, management)); setRevision(revision + 1); setError(''); setNotice('Report preview updated from the shared local data.') } catch (cause) { setError(cause.message) }
  }
  const exportCsv = () => {
    try {
      const csv = reportCsv(preview, activeRole)
      const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }))
      const link = document.createElement('a')
      link.href = url; link.download = `${preview.type}-report-farm-${preview.filters.farmId}.csv`
      document.body.appendChild(link); link.click(); link.remove()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
      setNotice('CSV export created from the generated preview, including all matching rows.')
    } catch (cause) { setError(cause.message) }
  }
  if (!types.length) return <EmptyState title="Reports unavailable" description="Your active role does not have report access." />
  return <div className="app-ui analytics-page reports-page">
    <PageHeader eyebrow="FARM INSIGHTS" title="Reports" description="Build a local report preview from farm, zone, and date filters." actions={<StatusBadge status="INFO" label={can(PERMISSIONS.REPORTS_GENERATE) ? 'Local report workspace' : 'Read-only report preview'} />} />
    <FilterBar label="Report filters" onSubmit={generate} actions={<button type="submit" className="app-ui-button app-ui-button--primary">{can(PERMISSIONS.REPORTS_GENERATE) ? 'Generate Report' : 'Update Preview'}</button>}>
      <label>Report Type<select aria-label="Report Type" value={filters.type} onChange={(event) => change({ type: event.target.value })}>{types.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}</select></label>
      <label>Farm<select aria-label="Farm" value={filters.farmId} onChange={(event) => change({ farmId: Number(event.target.value), zoneId: '' })}>{farmWorkspace.farms.map((farm) => <option key={farm.id} value={farm.id}>{farm.name}</option>)}</select></label>
      <label>Zone<select aria-label="Zone" value={filters.zoneId} onChange={(event) => change({ zoneId: event.target.value })}><option value="">All zones</option>{farmWorkspace.zones.filter(({ farmId }) => farmId === Number(filters.farmId)).map((zone) => <option key={zone.id} value={zone.id}>{zone.name}</option>)}</select></label>
      <label>From<input aria-label="From" type="datetime-local" required value={filters.from} onChange={(event) => change({ from: event.target.value })} /></label><label>To<input aria-label="To" type="datetime-local" required value={filters.to} onChange={(event) => change({ to: event.target.value })} /></label>
      {filters.type === 'sensor' && <label>Chart metric<select aria-label="Chart metric" value={filters.metricId} onChange={(event) => change({ metricId: event.target.value })}>{analyticsTypes.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}</select></label>}
    </FilterBar>
    <p className="analytics-note">Dates use Vietnam time (UTC+7), up to 31 days. This workspace uses the fixed demo data snapshot of 6 October 2026, 08:18. Filters are applied only when you update the preview.</p>
    {error && <p role="alert" className="analytics-error">{error}</p>}{notice && <p role="status" className="reports-notice">{notice}</p>}
    {dirty && <p role="status" className="reports-pending">Filters have changed. Update the preview before exporting.</p>}
    {!preview ? <EmptyState title="Your report preview will appear here" description="Choose a report type and filters, then generate or update the preview." /> : <div className="reports-preview" key={revision}>
      <SectionCard title={preview.title} description={`${preview.farm} · ${preview.zone} · ${preview.filters.from.replace('T', ' ')} to ${preview.filters.to.replace('T', ' ')} (Vietnam)`} actions={canExportReport(activeRole, preview.type) && <><button className="app-ui-button" disabled={Boolean(dirty)} onClick={exportCsv}>Export CSV</button><button className="app-ui-button" disabled={Boolean(dirty)} onClick={() => setNotice('PDF export is a prototype. No PDF file was generated. Use Export CSV for a downloadable report.')}>Export PDF (Prototype)</button></>}>
        <p className="analytics-note">{preview.highLevel ? 'High-level summary; individual operational records are not included.' : 'Read-only preview of matching shared records.'} This is a snapshot: regenerate to include subsequent local edits.</p>
        <div className="reports-kpis" aria-label="Report summary">{preview.kpis.map(([label, value]) => <StatCard key={label} label={label} value={value} />)}</div>
      </SectionCard>
      {preview.chart && <><ChartCard title={`Historical chart · ${preview.chart.type.name} (${preview.chart.type.unit})`} caption="Hourly means from synchronized readings; missing hours remain gaps."><EnvironmentalChart series={preview.chart.series} type={preview.chart.type} threshold={preview.chart.threshold} /></ChartCard><ChartCard title="Zone comparison" caption="Uses the same farm, selected zone and date range as this report. Choose All zones to compare the whole farm."><EnvironmentalChart series={preview.chart.comparison.series} type={preview.chart.type} zones={preview.chart.comparison.zones} /></ChartCard></>}
      <div className={preview.type === 'alert' || preview.type === 'device' ? 'reports-summary-grid' : 'reports-tables'}>{preview.tables.map((item) => <ReportTable key={item.title} item={item} />)}</div>
      {preview.detail && <ReportTable item={preview.detail} />}
      <SectionCard title="Report definitions"><ul className="reports-definitions">{preview.notes.map((note) => <li key={note}>{note}</li>)}</ul></SectionCard>
    </div>}
  </div>
}

export default function ReportsPage() {
  const { activeRole } = useAuth()
  return <ReportsWorkspace key={activeRole} />
}
