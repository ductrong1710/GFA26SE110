import { useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useManagement } from '../../context/ManagementContext'
import { PERMISSIONS } from '../../config/permissions'
import { useApiQuery } from '../../services/api/useApiQuery'
import { listAll } from '../../services/api/resources'
import { api } from '../../services/api'
import { alertView } from '../../services/api/adapters'
import { localDate, displayTime } from '../../utils/display'
import { useUiText } from '../../utils/useUiText'

import { PageHeader, StatCard, FilterBar, DataTable, StatusBadge, AlertSeverityBadge, EmptyState } from '../../components/app/ui'
import ManagementDialog from '../../components/app/ManagementDialog'
import '../../styles/management.css'

const typeLabels = { SENSOR_THRESHOLD: 'Sensor threshold', SENSOR_DATA_TIMEOUT: 'Sensor offline', SENSOR_LOW_BATTERY: 'Low battery', MISSION_ERROR: 'Mission failed', COLLECTION_FAILED: 'Collection failed', GATEWAY_ERROR: 'Gateway error', GATEWAY_OFFLINE: 'Gateway offline', UAV_LOW_BATTERY: 'UAV low battery' }
const historyLabels = { OPEN: 'Alert detected', ACKNOWLEDGED: 'Acknowledged', CLOSED: 'Closed', REOPENED: 'Reopened', NOTE: 'Handling note' }
const emptyFilters = { severity: '', type: '', status: '', farm: '', zone: '', date: '' }
const measurement = (value, unit, t) => value === undefined || value === null ? t('Not supplied') : `${value}${unit ? ` ${unit}` : ''}`
const translations = {
  'Sensor threshold': 'Ngưỡng cảm biến',
  'Sensor offline': 'Cảm biến ngoại tuyến',
  'Low battery': 'Pin yếu',
  'Mission failed': 'Nhiệm vụ thất bại',
  'Collection failed': 'Thu thập thất bại',
  'Gateway error': 'Lỗi trạm kết nối',
  'Gateway offline': 'Trạm kết nối ngoại tuyến',
  'UAV low battery': 'Pin UAV yếu',
  'Alert detected': 'Phát hiện cảnh báo',
  Acknowledged: 'Đã xác nhận',
  Closed: 'Đã đóng',
  Reopened: 'Đã mở lại',
  'Not supplied': 'Không có dữ liệu',
  'No device linked': 'Không có thiết bị liên kết',
  Gateway: 'Trạm kết nối',
  Sensor: 'Cảm biến',
  'Alert details': 'Chi tiết cảnh báo',
  'Affected device': 'Thiết bị bị ảnh hưởng',
  'Farm / Zone': 'Trang trại / Khu vực',
  'Trigger value': 'Giá trị kích hoạt',
  'Configured threshold': 'Ngưỡng đã cấu hình',
  'minutes without data': 'phút không có dữ liệu',
  'Detected time': 'Thời điểm phát hiện',
  'Related sensor reading': 'Số liệu cảm biến liên quan',
  'View sensor data': 'Xem dữ liệu cảm biến',
  'No sensor reading linked': 'Không có số liệu cảm biến liên kết',
  'Related mission': 'Nhiệm vụ liên quan',
  'No mission linked': 'Không có nhiệm vụ liên kết',
  Handling: 'Xử lý',
  'Handling note': 'Ghi chú xử lý',
  'Administrator actions': 'Thao tác quản trị',
  Acknowledge: 'Xác nhận',
  'Close Alert': 'Đóng cảnh báo',
  'Record the investigation or follow-up…': 'Ghi lại quá trình điều tra hoặc bước xử lý tiếp theo…',
  'Add Handling Note': 'Thêm ghi chú xử lý',
  'Handling note added.': 'Đã thêm ghi chú xử lý.',
  'Alert status updated.': 'Đã cập nhật trạng thái cảnh báo.',
  History: 'Lịch sử',
  System: 'Hệ thống',
  MONITORING: 'GIÁM SÁT',
  Alerts: 'Cảnh báo',
  'Review incidents, understand their impact, and track resolution.': 'Xem xét sự cố, tìm hiểu ảnh hưởng và theo dõi quá trình xử lý.',
  'Read-only access': 'Quyền chỉ xem',
  'Summary covers the current filtered API results. Critical excludes closed alerts. Dates use Vietnam time and today is': 'Tóm tắt dựa trên kết quả API hiện được lọc. Cảnh báo nghiêm trọng không bao gồm cảnh báo đã đóng. Ngày theo giờ Việt Nam và hôm nay là',
  Open: 'Đang mở',
  Critical: 'Nghiêm trọng',
  'Closed Today': 'Đã đóng hôm nay',
  'Alert summary': 'Tóm tắt cảnh báo',
  'Alert filters': 'Bộ lọc cảnh báo',
  'Reset filters': 'Đặt lại bộ lọc',
  All: 'Tất cả',
  Severity: 'Mức độ nghiêm trọng',
  Type: 'Loại',
  Status: 'Trạng thái',
  Farm: 'Trang trại',
  Zone: 'Khu vực',
  Date: 'Ngày',
  'Alert management': 'Quản lý cảnh báo',
  Alert: 'Cảnh báo',
  Device: 'Thiết bị',
  'Trigger Value': 'Giá trị kích hoạt',
  'Detected At': 'Thời điểm phát hiện',
  'Select an alert to view details': 'Chọn cảnh báo để xem chi tiết',
  'Try again': 'Thử lại',
  'Loading…': 'Đang tải…',
  'No alerts found': 'Không tìm thấy cảnh báo',
  'No alerts match the current filters.': 'Không có cảnh báo nào phù hợp với bộ lọc hiện tại.',
  'UTC+7': 'UTC+7',
  'alerts match the current filters': 'cảnh báo khớp với bộ lọc hiện tại',
}

function AlertDrawer({ alert, onClose, onChanged }) {
  const t = useUiText(translations)
  const { can } = useAuth()
  const { management, changeManagement } = useManagement()
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [pending, setPending] = useState(false)
  const canManage = can(PERMISSIONS.ALERTS_MANAGE)
  const act = async (action) => {
    if (pending) return
    setPending(true)
    try { await changeManagement({ kind: 'alert', id: alert.id, action, note: action === 'NOTE' ? note : '' }); setError(''); setNotice(t(action === 'NOTE' ? 'Handling note added.' : 'Alert status updated.')); if (action === 'NOTE') setNote(''); onChanged() }
    catch (err) { setError(err.message) }
    finally { setPending(false) }
  }
  return <ManagementDialog title={t('Alert details')} onClose={onClose} drawer>
    <div className="management-drawer-body">
      <div className="management-badges"><AlertSeverityBadge severity={alert.severity} /><StatusBadge status={alert.status} label={alert.status === 'ACKNOWLEDGED' ? 'Acknowledged' : alert.status === 'CLOSED' ? 'Closed' : 'Open'} /></div>
      <h3 className="alert-drawer-title">{alert.title}</h3><p className="management-note">{alert.message}</p>
      <dl className="management-detail-grid">
        <div><dt>{t('Affected device')}</dt><dd>{alert.deviceName}</dd></div><div><dt>{t('Farm / Zone')}</dt><dd>{alert.farm?.name ?? t('Not supplied')} / {alert.zone?.name ?? t('Not supplied')}</dd></div>
        <div><dt>{t('Trigger value')}</dt><dd>{measurement(alert.triggeredValue, alert.unit, t)}</dd></div><div><dt>{t('Configured threshold')}</dt><dd>{alert.timeoutMinutes ? `${alert.timeoutMinutes} ${t('minutes without data')}` : measurement(alert.thresholdValue, alert.unit, t)}</dd></div>
        <div><dt>{t('Detected time')}</dt><dd><time dateTime={alert.openedAt}>{displayTime(alert.openedAt)} ({t('UTC+7')})</time></dd></div>
        <div><dt>{t('Related sensor reading')}</dt><dd>{alert.reading ? <>{measurement(alert.reading.value, alert.unit, t)}<br /><time dateTime={alert.reading.measuredAt}>{displayTime(alert.reading.measuredAt)}</time>{can(PERMISSIONS.SENSOR_DATA_VIEW) && <Link className="management-link" to="/app/sensor-data">{t('View sensor data')}</Link>}</> : t('No sensor reading linked')}</dd></div>
        <div><dt>{t('Related mission')}</dt><dd>{alert.mission ? can(PERMISSIONS.MISSIONS_VIEW) ? <Link className="management-link" to={`/app/missions/${alert.mission.id}`}>{alert.mission.code}</Link> : alert.mission.code : t('No mission linked')}</dd></div>
      </dl>
      {canManage && <section className="alert-handling" aria-label={t('Administrator actions')}><h3>{t('Handling')}</h3>
        <div className="management-row-actions">{alert.status === 'OPEN' && <button className="app-ui-button" disabled={pending} onClick={() => act('ACKNOWLEDGED')}>{t('Acknowledge')}</button>}{alert.status !== 'CLOSED' ? <button className="app-ui-button" disabled={pending} onClick={() => act('CLOSED')}>{t('Close Alert')}</button> : null}</div>
        <form onSubmit={(event) => { event.preventDefault(); act('NOTE') }} className="management-form"><label>{t('Handling note')}<textarea rows={3} disabled={pending} required value={note} maxLength={1000} onChange={(event) => setNote(event.target.value)} placeholder={t('Record the investigation or follow-up…')} /></label><button className="app-ui-button app-ui-button--primary" type="submit" disabled={pending || !note.trim()}>{t('Add Handling Note')}</button></form>
      </section>}
      {error && <p role="alert" className="management-error">{error}</p>}{notice && <p role="status" className="management-success">{notice}</p>}
      <section aria-label={t('History')}><h3>{t('History')}</h3><ol className="alert-history">{alert.history.map((event) => <li key={event.id}><strong>{historyLabels[event.action] ? t(historyLabels[event.action]) : event.action}</strong><p>{management.users.find(({ id }) => id === event.userId)?.fullName ?? (event.userId ? `User #${event.userId}` : t('System'))} · {displayTime(event.occurredAt)}</p>{event.note && <blockquote>{event.note}</blockquote>}</li>)}</ol></section>
    </div>
  </ManagementDialog>
}

function AlertsWorkspace() {

  const t = useUiText(translations)
  const { can } = useAuth()

  const { farmWorkspace } = useOutletContext()
  const [today] = useState(() => localDate(new Date().toISOString()))
  const [filters, setFilters] = useState(emptyFilters)
  const [selectedId, setSelectedId] = useState(null)
  const query = useApiQuery(`alerts:${JSON.stringify(filters)}`, signal => listAll('/api/alerts', { severity: filters.severity || undefined, alertType: filters.type || undefined, status: filters.status || undefined, farmId: filters.farm || undefined, zoneId: filters.zone || undefined, from: filters.date ? `${filters.date}T00:00:00+07:00` : undefined, to: filters.date ? `${filters.date}T23:59:59+07:00` : undefined }, signal))
  const detailQuery = useApiQuery(`alert:${selectedId}`, signal => api.get(`/api/alerts/${selectedId}`, {}, signal), Boolean(selectedId))
  const decorate = raw => { const a = alertView(raw); return { ...a, deviceName: a.sensorNodeId ? `${t('Sensor')} #${a.sensorNodeId}` : a.gatewayId ? `${t('Gateway')} #${a.gatewayId}` : a.uavId ? `UAV #${a.uavId}` : t('No device linked'), mission: a.missionId ? { id: a.missionId, code: `#${a.missionId}` } : null } }
  const records = (query.data ?? []).map(decorate)
  const rows = records
  const selected = detailQuery.data ? decorate(detailQuery.data) : null
  const summary = [['Open', records.filter(({ status }) => status === 'OPEN').length], ['Critical', records.filter(({ severity, status }) => severity === 'CRITICAL' && status !== 'CLOSED').length], ['Acknowledged', records.filter(({ status }) => status === 'ACKNOWLEDGED').length], ['Closed Today', records.filter(({ status, closedAt }) => status === 'CLOSED' && localDate(closedAt) === today).length]]
  const select = (field, label, choices) => <label>{t(label)}<select aria-label={t(label)} value={filters[field]} onChange={(event) => setFilters({ ...filters, [field]: event.target.value, ...(field === 'farm' ? { zone: '' } : {}) })}><option value="">{t('All')} {t(label).toLowerCase()}</option>{choices.map(([value, text]) => <option key={value} value={value}>{t(text)}</option>)}</select></label>
  return <div className="app-ui management-page">
    {query.loading ? <p role="status" className="operations-note">{t('Loading…')}</p> : query.error ? <div role="alert" className="management-error"><p>{query.error.message}</p>{query.reload && <button className="app-ui-button" onClick={query.reload}>{t('Try again')}</button>}</div> : null}
    {detailQuery.loading && Boolean(selectedId) && <p role="status" className="operations-note">{t('Loading…')}</p>}
    {detailQuery.error && <div role="alert" className="management-error"><p>{detailQuery.error.message}</p>{detailQuery.reload && <button className="app-ui-button" onClick={detailQuery.reload}>{t('Try again')}</button>}</div>}
    <PageHeader eyebrow={t('MONITORING')} title={t('Alerts')} description={t('Review incidents, understand their impact, and track resolution.')} actions={!can(PERMISSIONS.ALERTS_MANAGE) && <StatusBadge status="INFO" label={t('Read-only access')} />} />
    <p className="management-note">{t('Summary covers the current filtered API results. Critical excludes closed alerts. Dates use Vietnam time and today is')} {today}.</p>
    <section className="management-summary" aria-label={t('Alert summary')}>{summary.map(([label, value]) => <StatCard key={label} label={t(label)} value={value} />)}</section>
    <FilterBar label={t('Alert filters')} actions={<button className="app-ui-button" type="button" onClick={() => setFilters(emptyFilters)}>{t('Reset filters')}</button>}>
      {select('severity', 'Severity', ['CRITICAL', 'ERROR', 'WARNING', 'INFO'].map((value) => [value, value]))}
      {select('type', 'Type', Object.entries(typeLabels))}{select('status', 'Status', ['OPEN', 'ACKNOWLEDGED', 'CLOSED'].map((value) => [value, value]))}
      {select('farm', 'Farm', farmWorkspace.farms.map(({ id, name }) => [id, name]))}
      {select('zone', 'Zone', farmWorkspace.zones.filter(({ farmId }) => !filters.farm || farmId === Number(filters.farm)).map(({ id, name }) => [id, name]))}
      <label>{t('Date')}<input type="date" value={filters.date} onChange={(event) => setFilters({ ...filters, date: event.target.value })} /></label>
    </FilterBar>
    <DataTable caption={t('Alert management')} rows={rows} onRowClick={(alert) => setSelectedId(alert.id)} emptyState={<EmptyState title={t('No alerts found')} description={t('No alerts match the current filters.')} />} columns={[
      { key: 'severity', header: t('Severity'), render: (severity) => <AlertSeverityBadge severity={severity} /> },
      { key: 'title', header: t('Alert'), render: (title, alert) => <button className="management-table-link" onClick={() => setSelectedId(alert.id)}>{title}</button> },
      { key: 'deviceName', header: t('Device') },
      { key: 'farm', header: t('Farm / Zone'), render: (farm, alert) => <>{farm?.name ?? t('Not supplied')}<small className="management-cell-subtitle">{alert.zone?.name ?? t('Not supplied')}</small></> },
      { key: 'triggeredValue', header: t('Trigger Value'), render: (value, alert) => measurement(value, alert.unit, t) },
      { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> },
      { key: 'openedAt', header: t('Detected At'), render: (at) => <time dateTime={at}>{displayTime(at)}</time> },
    ]} footer={`${rows.length} ${t('alerts match the current filters')} · ${t('Select an alert to view details')}`} />
    {selected && <AlertDrawer key={selected.id} alert={selected} onChanged={() => { query.reload(); detailQuery.reload() }} onClose={() => { setSelectedId(null); query.reload() }} />}
  </div>
}

export default function AlertsPage() { return <AlertsWorkspace /> }
