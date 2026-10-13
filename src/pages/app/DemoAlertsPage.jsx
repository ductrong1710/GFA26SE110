import { useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useManagement } from '../../context/ManagementContext'
import { useOperations } from '../../context/OperationsContext'
import { PERMISSIONS } from '../../config/permissions'
import { getAlertsForUser } from '../../data/mock/selectors'
import { getAlertDetails, filterAlerts, localDate, displayTime } from '../../data/mock/alertSelectors'
import { MOCK_NOW } from '../../data/mock/scenario'
import { PageHeader, StatCard, FilterBar, DataTable, StatusBadge, AlertSeverityBadge } from '../../components/app/ui'
import ManagementDialog from '../../components/app/ManagementDialog'
import '../../styles/management.css'

const typeLabels = { SENSOR_THRESHOLD: 'Sensor threshold', SENSOR_DATA_TIMEOUT: 'Sensor offline', SENSOR_LOW_BATTERY: 'Low battery', MISSION_ERROR: 'Mission failed', COLLECTION_FAILED: 'Collection failed', GATEWAY_ERROR: 'Gateway error' }
const historyLabels = { OPEN: 'Alert detected', ACKNOWLEDGED: 'Acknowledged', CLOSED: 'Closed', REOPENED: 'Reopened', NOTE: 'Handling note' }
const emptyFilters = { severity: '', type: '', status: '', farm: '', zone: '', date: '' }
const measurement = (value, unit) => value === undefined || value === null ? 'Not applicable' : `${value}${unit ? ` ${unit}` : ''}`

function AlertDrawer({ alert, onClose }) {
  const { can } = useAuth()
  const { management, changeManagement } = useManagement()
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const canManage = can(PERMISSIONS.ALERTS_MANAGE)
  const act = (action) => {
    try { changeManagement({ kind: 'alert', id: alert.id, action, note: action === 'NOTE' ? note : '' }); setError(''); setNotice(action === 'NOTE' ? 'Handling note added.' : 'Alert status updated.'); if (action === 'NOTE') setNote('') }
    catch (err) { setError(err.message) }
  }
  return <ManagementDialog title="Alert details" onClose={onClose} drawer>
    <div className="management-drawer-body">
      <div className="management-badges"><AlertSeverityBadge severity={alert.severity} /><StatusBadge status={alert.status} label={alert.status === 'ACKNOWLEDGED' ? 'Acknowledged' : alert.status === 'CLOSED' ? 'Closed' : 'Open'} /></div>
      <h3 className="alert-drawer-title">{alert.title}</h3><p className="management-note">{alert.message}</p>
      <dl className="management-detail-grid">
        <div><dt>Affected device</dt><dd>{alert.deviceName}</dd></div><div><dt>Farm / Zone</dt><dd>{alert.farm?.name ?? 'Unknown farm'} / {alert.zone?.name ?? 'Farm-wide'}</dd></div>
        <div><dt>Trigger value</dt><dd>{measurement(alert.triggeredValue, alert.unit)}</dd></div><div><dt>Configured threshold</dt><dd>{alert.timeoutMinutes ? `${alert.timeoutMinutes} minutes without data` : measurement(alert.thresholdValue, alert.unit)}</dd></div>
        <div><dt>Detected time</dt><dd><time dateTime={alert.openedAt}>{displayTime(alert.openedAt)} (UTC+7)</time></dd></div>
        <div><dt>Related sensor reading</dt><dd>{alert.reading ? <>{measurement(alert.reading.value, alert.unit)}<br /><time dateTime={alert.reading.measuredAt}>{displayTime(alert.reading.measuredAt)}</time>{can(PERMISSIONS.SENSOR_DATA_VIEW) && <Link className="management-link" to="/app/sensor-data">View sensor data</Link>}</> : 'No sensor reading linked'}</dd></div>
        <div><dt>Related mission</dt><dd>{alert.mission ? can(PERMISSIONS.MISSIONS_VIEW) ? <Link className="management-link" to={`/app/missions/${alert.mission.id}`}>{alert.mission.code} · {alert.mission.name}</Link> : `${alert.mission.code} · ${alert.mission.name}` : 'No mission linked'}</dd></div>
      </dl>
      {canManage && <section className="alert-handling" aria-label="Administrator actions"><h3>Handling</h3>
        <div className="management-row-actions">{alert.status === 'OPEN' && <button className="app-ui-button" onClick={() => act('ACKNOWLEDGED')}>Acknowledge</button>}{alert.status !== 'CLOSED' ? <button className="app-ui-button" onClick={() => act('CLOSED')}>Close Alert</button> : <button className="app-ui-button" onClick={() => act('REOPENED')}>Reopen</button>}</div>
        <form onSubmit={(event) => { event.preventDefault(); act('NOTE') }} className="management-form"><label>Handling note<textarea rows={3} value={note} maxLength={1000} onChange={(event) => setNote(event.target.value)} placeholder="Record the investigation or follow-up…" /></label><button className="app-ui-button app-ui-button--primary" type="submit">Add Handling Note</button></form>
      </section>}
      {error && <p role="alert" className="management-error">{error}</p>}{notice && <p role="status" className="management-success">{notice}</p>}
      <section aria-label="Alert history"><h3>History</h3><ol className="alert-history">{alert.history.map((event) => <li key={event.id}><strong>{historyLabels[event.action] ?? event.action}</strong><p>{management.users.find(({ id }) => id === event.userId)?.fullName ?? 'System'} · {displayTime(event.occurredAt)}</p>{event.note && <blockquote>{event.note}</blockquote>}</li>)}</ol></section>
    </div>
  </ManagementDialog>
}

export default function AlertsPage() {
  const { operations } = useOperations()
  const { user, activeRole, can } = useAuth()
  const { management } = useManagement()
  const { farmWorkspace } = useOutletContext()
  const [filters, setFilters] = useState(emptyFilters)
  const [selectedId, setSelectedId] = useState(null)
  const records = getAlertsForUser(user.id, activeRole, management.alerts).map((alert) => getAlertDetails(alert, farmWorkspace, operations)).sort((a, b) => b.openedAt.localeCompare(a.openedAt))
  const rows = filterAlerts(records, filters)
  const selected = records.find(({ id }) => id === selectedId)
  const summary = [['Open', records.filter(({ status }) => status === 'OPEN').length], ['Critical', records.filter(({ severity, status }) => severity === 'CRITICAL' && status !== 'CLOSED').length], ['Acknowledged', records.filter(({ status }) => status === 'ACKNOWLEDGED').length], ['Closed Today', records.filter(({ status, closedAt }) => status === 'CLOSED' && localDate(closedAt) === localDate(MOCK_NOW)).length]]
  const select = (field, label, choices) => <label>{label}<select aria-label={label} value={filters[field]} onChange={(event) => setFilters({ ...filters, [field]: event.target.value, ...(field === 'farm' ? { zone: '' } : {}) })}><option value="">All {label.toLowerCase()}</option>{choices.map(([value, text]) => <option key={value} value={value}>{text}</option>)}</select></label>
  return <div className="app-ui management-page">
    <PageHeader eyebrow="MONITORING" title="Alerts" description="Review incidents, understand their impact, and track resolution." actions={!can(PERMISSIONS.ALERTS_MANAGE) && <StatusBadge status="INFO" label="Read-only access" />} />
    <p className="management-note">Summary covers your permitted alerts before filters. Critical excludes closed alerts. Dates use Vietnam time and the demo snapshot of {localDate(MOCK_NOW)}.</p>
    <section className="management-summary" aria-label="Alert summary">{summary.map(([label, value]) => <StatCard key={label} label={label} value={value} />)}</section>
    <FilterBar label="Alert filters" actions={<button className="app-ui-button" type="button" onClick={() => setFilters(emptyFilters)}>Reset filters</button>}>
      {select('severity', 'Severity', ['CRITICAL', 'ERROR', 'WARNING', 'INFO'].map((value) => [value, value]))}
      {select('type', 'Type', Object.entries(typeLabels))}{select('status', 'Status', ['OPEN', 'ACKNOWLEDGED', 'CLOSED'].map((value) => [value, value]))}
      {select('farm', 'Farm', farmWorkspace.farms.map(({ id, name }) => [id, name]))}
      {select('zone', 'Zone', farmWorkspace.zones.filter(({ farmId }) => !filters.farm || farmId === Number(filters.farm)).map(({ id, name }) => [id, name]))}
      <label>Date<input type="date" value={filters.date} onChange={(event) => setFilters({ ...filters, date: event.target.value })} /></label>
    </FilterBar>
    <DataTable caption="Alert management" rows={rows} onRowClick={(alert) => setSelectedId(alert.id)} columns={[
      { key: 'severity', header: 'Severity', render: (severity) => <AlertSeverityBadge severity={severity} /> },
      { key: 'title', header: 'Alert', render: (title, alert) => <button className="management-table-link" onClick={() => setSelectedId(alert.id)}>{title}</button> },
      { key: 'deviceName', header: 'Device' },
      { key: 'farm', header: 'Farm / Zone', render: (farm, alert) => <>{farm?.name}<small className="management-cell-subtitle">{alert.zone?.name ?? 'Farm-wide'}</small></> },
      { key: 'triggeredValue', header: 'Trigger Value', render: (value, alert) => measurement(value, alert.unit) },
      { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> },
      { key: 'openedAt', header: 'Detected At', render: (at) => <time dateTime={at}>{displayTime(at)}</time> },
    ]} footer={`${rows.length} alerts match the current filters · Select an alert to view details`} />
    {selected && <AlertDrawer key={selected.id} alert={selected} onClose={() => setSelectedId(null)} />}
  </div>
}
