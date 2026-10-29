import { useState } from 'react'
import { Link, useNavigate, useOutletContext, useParams } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { getMissionMonitoring, allowedMissionStatuses, supportsTelemetry } from '../../services/api/monitoring'
import { displayTime } from '../../utils/display'
import { useUiText } from '../../utils/useUiText'
import { isTello } from '../../services/api/telloPlanning'

import { PageHeader, SectionCard, StatusBadge, DataTable, EmptyState, ProgressBar, BatteryIndicator } from '../../components/app/ui'
import MissionMonitoringMap from '../../components/app/MissionMonitoringMap'
import MissionLaunchPanel from '../../components/app/MissionLaunchPanel'
import ManagementDialog from '../../components/app/ManagementDialog'
import '../../styles/missions.css'
import '../../styles/mission-monitoring.css'

const translations = {
  'Report Mission Status': 'Báo cáo trạng thái nhiệm vụ',
  'Add Operational Note': 'Thêm ghi chú vận hành',
  'This updates the mission through the authorized backend workflow.': 'Thao tác này cập nhật nhiệm vụ thông qua quy trình được máy chủ cho phép.',
  'Reported status': 'Trạng thái được báo cáo',
  'The backend validates lifecycle transitions and completion requirements.': 'Máy chủ xác thực các chuyển đổi trạng thái và yêu cầu hoàn thành.',
  'Operational note': 'Ghi chú vận hành',
  'Describe the observed outcome and reason for this update.': 'Mô tả kết quả quan sát được và lý do cập nhật này.',
  'Add field observations or handover details.': 'Thêm ghi nhận tại hiện trường hoặc thông tin bàn giao.',
  Cancel: 'Hủy',
  'Save Status Report': 'Lưu báo cáo trạng thái',
  'Save Note': 'Lưu ghi chú',
  'Mission updated successfully.': 'Đã cập nhật nhiệm vụ thành công.',
  'Farm not selected': 'Chưa chọn trang trại',
  'Mission monitoring': 'Giám sát nhiệm vụ',
  'Back to Missions': 'Quay lại danh sách nhiệm vụ',
  'Resume Draft': 'Tiếp tục bản nháp',
  'Start time': 'Thời gian bắt đầu',
  'Not started': 'Chưa bắt đầu',
  'Elapsed time': 'Thời gian đã trôi qua',
  'Snapshot · Vietnam time': 'Ảnh chụp dữ liệu · Giờ Việt Nam',
  'Live telemetry unavailable. Mission status may be updated manually.': 'Không có dữ liệu đo từ xa trực tiếp. Có thể cập nhật trạng thái nhiệm vụ thủ công.',
  'Mission Map': 'Bản đồ nhiệm vụ',
  'Planned route and recorded positions. Individual waypoint completion is not supplied by the API.': 'Lộ trình dự kiến và các vị trí đã ghi nhận. API không cung cấp trạng thái hoàn thành từng điểm tham chiếu.',
  'Recorded telemetry': 'Dữ liệu đo từ xa đã ghi nhận',
  'No current position': 'Không có vị trí hiện tại',
  'Current waypoint': 'Điểm tham chiếu hiện tại',
  'Current waypoint: not reported': 'Chưa có báo cáo về điểm tham chiếu hiện tại',
  'Individual waypoint completion is unavailable.': 'Không có dữ liệu hoàn thành từng điểm tham chiếu.',
  'Waypoint status': 'Trạng thái điểm tham chiếu',
  Current: 'Hiện tại',
  'Mission Status': 'Trạng thái nhiệm vụ',
  Status: 'Trạng thái',
  'Sensor Targets': 'Mục tiêu cảm biến',
  Gateway: 'Trạm kết nối',
  Unassigned: 'Chưa phân công',
  'Battery at mission end': 'Pin khi kết thúc nhiệm vụ',
  'UAV battery': 'Pin UAV',
  'planned waypoints · Completion unavailable': 'điểm tham chiếu dự kiến · Không có dữ liệu hoàn thành',
  'Collection Success rate': 'Tỷ lệ thu thập thành công',
  'Successful attempts / finished attempts. Retries count; pending collections do not.': 'Lần thử thành công / tổng lần thử đã kết thúc. Tính cả lần thử lại; không tính lượt thu thập đang chờ.',
  'Position recorded': 'Vị trí được ghi nhận lúc',
  'No live position stream is connected.': 'Chưa kết nối luồng vị trí trực tiếp.',
  'Update Mission Status': 'Cập nhật trạng thái nhiệm vụ',
  'Record observations only. No flight commands are sent.': 'Chỉ ghi nhận quan sát. Không gửi lệnh bay.',
  'Collection Progress': 'Tiến độ thu thập',
  'collected records · Collection does not imply synchronization to the server.': 'bản ghi đã thu thập · Việc thu thập không đồng nghĩa dữ liệu đã đồng bộ lên máy chủ.',
  'Collection progress': 'Tiến độ thu thập',
  Sensor: 'Cảm biến',
  'Unknown sensor': 'Cảm biến không xác định',
  'Collection Point': 'Điểm thu thập',
  'Not configured': 'Chưa cấu hình',
  Attempts: 'Lần thử',
  'View attempts': 'Xem các lần thử',
  'View details': 'Xem chi tiết',
  Records: 'Bản ghi',
  'Last Error': 'Lỗi gần nhất',
  'Recovered on retry': 'Đã khôi phục khi thử lại',
  'View Error Reason': 'Xem nguyên nhân lỗi',
  'None recorded': 'Không có dữ liệu ghi nhận',
  'No sensor targets': 'Không có mục tiêu cảm biến',
  'No telemetry records': 'Không có bản ghi đo từ xa',
  'Mission telemetry has not been recorded yet.': 'Dữ liệu đo từ xa của nhiệm vụ chưa được ghi nhận.',
  'Resume the draft to select sensors for this mission.': 'Tiếp tục bản nháp để chọn cảm biến cho nhiệm vụ này.',
  'Historical positions reported by the device. Missing values are unavailable.': 'Các vị trí trong quá khứ do thiết bị báo cáo. Giá trị còn thiếu không khả dụng.',
  'Mission telemetry history': 'Lịch sử dữ liệu đo từ xa của nhiệm vụ',
  'Recorded At': 'Thời điểm ghi nhận',
  Latitude: 'Vĩ độ',
  Longitude: 'Kinh độ',
  'Altitude (m)': 'Độ cao (m)',
  'Battery (%)': 'Pin (%)',
  Waypoint: 'Điểm tham chiếu',
  'Flight Status': 'Trạng thái chuyến bay',
  'Mission Timeline': 'Dòng thời gian nhiệm vụ',
  'Recorded events in chronological order. Times use Vietnam time.': 'Các sự kiện được ghi theo thứ tự thời gian. Thời gian theo giờ Việt Nam.',
  'Mission timeline events': 'Sự kiện theo dòng thời gian nhiệm vụ',
  'Reported by': 'Người báo cáo',
  'Mission has not started': 'Nhiệm vụ chưa bắt đầu',
  'Collection and waypoint events will appear when recorded.': 'Sự kiện thu thập và điểm tham chiếu sẽ hiển thị sau khi được ghi nhận.',
  'Operational Notes': 'Ghi chú vận hành',
  'Original planning notes and appended operator observations.': 'Ghi chú lập kế hoạch ban đầu và các quan sát bổ sung của người vận hành.',
  'No planning notes.': 'Không có ghi chú lập kế hoạch.',
  'No additional operational notes.': 'Không có ghi chú vận hành bổ sung.',
  'Collection Attempts': 'Các lần thử thu thập',
  Attempt: 'Lần thử',
  'Collection point not configured': 'Điểm thu thập chưa được cấu hình',
  'Recovered on a later retry.': 'Đã khôi phục ở lần thử lại sau.',
  'No recorded attempt details': 'Không có chi tiết lần thử được ghi nhận',
  'No error reason was recorded for this target.': 'Không có nguyên nhân lỗi nào được ghi nhận cho mục tiêu này.',
  'Mission not found': 'Không tìm thấy nhiệm vụ',
  'This mission is unavailable or outside your authorized workspace.': 'Nhiệm vụ này không khả dụng hoặc nằm ngoài không gian làm việc được cấp quyền.',
  'Try again': 'Thử lại',
  'Loading…': 'Đang tải…',
  Unavailable: 'Không khả dụng',
  Started: 'Bắt đầu',
  Finished: 'Kết thúc',
  'Records collected': 'Bản ghi đã thu thập',
  Unknown: 'Không xác định',
}

function MissionReportDialog({ mission, statuses, onSave, onClose, mode }) {
  const t = useUiText(translations)
  const [note, setNote] = useState('')
  const [status, setStatus] = useState(statuses[0] ?? '')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const submit = async (event) => { event.preventDefault(); setPending(true); try { await onSave({ note, status: mode === 'status' ? status : undefined }); onClose() } catch (cause) { setError(cause.message) } finally { setPending(false) } }
  return <ManagementDialog title={t(mode === 'status' ? 'Report Mission Status' : 'Add Operational Note')} onClose={onClose}><form className="monitor-report-form" onSubmit={submit}>
    <p>{mission.code} · {t('This updates the mission through the authorized backend workflow.')}</p>
    {error && <p role="alert" className="mission-error">{error}</p>}
    {mode === 'status' && <><label>{t('Reported status')}<select aria-label={t('Reported status')} value={status} onChange={(event) => setStatus(event.target.value)} required>{statuses.map((value) => <option key={value} value={value}>{value.replaceAll('_', ' ')}</option>)}</select></label><p className="mission-muted">{t('The backend validates lifecycle transitions and completion requirements.')}</p></>}
    <label>{t('Operational note')}<textarea autoFocus required maxLength={2000} rows={5} value={note} onChange={(event) => setNote(event.target.value)} placeholder={t(mode === 'status' ? 'Describe the observed outcome and reason for this update.' : 'Add field observations or handover details.')} /></label>
    <div className="mission-actions"><button className="app-ui-button" type="button" onClick={onClose}>{t('Cancel')}</button><button className="app-ui-button app-ui-button--primary" type="submit" disabled={pending}>{t(mode === 'status' ? 'Save Status Report' : 'Save Note')}</button></div>
  </form></ManagementDialog>
}

function MissionMonitor({ data }) {
  const t = useUiText(translations)
  const { operations, startMissionPlan, reportMission } = useOperations()
  const { farmWorkspace } = useOutletContext()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [detailId, setDetailId] = useState(null)
  const [dialog, setDialog] = useState(null)
  const [notice, setNotice] = useState('')
  const [actionError, setActionError] = useState('')
  const [resuming, setResuming] = useState(false)
  const { mission, farm, uav, gateway, progress, rows, waypoints, telemetry, currentWaypoint } = data
  const detail = rows.find(({ id }) => id === detailId)
  const manage = can(PERMISSIONS.MISSIONS_MANAGE)
  const statuses = allowedMissionStatuses(mission, operations)
  const save = async (change) => { await reportMission({ missionId: mission.id, ...change }, farmWorkspace); setNotice(t('Mission updated successfully.')) }
  return <div className="app-ui missions-page mission-monitoring">
    <PageHeader eyebrow={mission.code} title={mission.name} description={`${farm?.name ?? t('Farm not selected')} · ${t('Mission monitoring')}`} actions={<><Link className="app-ui-button" to="/app/missions">{t('Back to Missions')}</Link>{mission.status === 'PENDING' && can(PERMISSIONS.MISSIONS_CREATE) && <button className="app-ui-button app-ui-button--primary" disabled={resuming} onClick={async () => { setResuming(true); setActionError(''); try { await startMissionPlan(mission); navigate('/app/missions/create') } catch (e) { setActionError(e.message) } finally { setResuming(false) } }}>{t('Resume Draft')}</button>}</>} />
    <div className="monitor-meta"><StatusBadge status={mission.status} /><div><span>{t('Start time')}</span><strong>{mission.startedAt ? displayTime(mission.startedAt) : t('Not started')}</strong></div><div><span>{t('Elapsed time')}</span><strong>{data.elapsed}</strong></div><div><span>{t('Snapshot · Vietnam time')}</span><strong>{displayTime(telemetry?.recordedAt ?? mission.updatedAt ?? mission.createdAt)}</strong></div></div>
    {actionError && <p role="alert">{actionError}</p>}{notice && <p role="status" className="mission-success">{notice}</p>}
    {isTello(uav) && <MissionLaunchPanel key={`${mission.id}:${mission.planRevision}`} missionId={mission.id} planRevision={mission.planRevision} canManage={manage} />}
    {uav && !supportsTelemetry(uav) && <p className="monitor-telemetry-message" role="status">{t('Live telemetry unavailable. Mission status may be updated manually.')}</p>}
    <div className="monitor-main-grid"><SectionCard title={t('Mission Map')} description={t('Planned route and recorded positions. Individual waypoint completion is not supplied by the API.')} actions={<StatusBadge status={telemetry ? 'INFO' : 'INACTIVE'} label={t(telemetry ? 'Recorded telemetry' : 'No current position')} />}>
      <MissionMonitoringMap data={data} />
      <div className="monitor-waypoint-summary"><strong>{currentWaypoint ? `${t('Current waypoint')}: ${currentWaypoint.sequenceNo} of ${progress.totalWaypoints}` : t('Current waypoint: not reported')}</strong><span>{t('Individual waypoint completion is unavailable.')}</span></div>
      <ol className="monitor-waypoints" aria-label={t('Waypoint status')}>{waypoints.map((waypoint) => <li key={waypoint.id} data-state={waypoint.id === currentWaypoint?.id ? 'current' : waypoint.status}><span>{waypoint.sequenceNo}</span><small>{waypoint.id === currentWaypoint?.id ? t('Current') : waypoint.status.toLowerCase()}</small></li>)}</ol>
    </SectionCard><SectionCard title={t('Mission Status')} className="monitor-status-panel">
      <dl className="monitor-equipment"><div><dt>UAV</dt><dd>{uav?.code ?? t('Unassigned')}<small>{uav?.name}</small></dd></div><div><dt>{t('Gateway')}</dt><dd>{gateway?.code ?? t('Unassigned')}<small>{gateway?.name}</small></dd></div><div><dt>{t(mission.completedAt ? 'Battery at mission end' : 'UAV battery')}</dt><dd><BatteryIndicator value={data.battery} /></dd></div></dl>
      <p className="mission-muted">{progress.totalWaypoints} {t('planned waypoints · Completion unavailable')}</p>
      <ProgressBar label={`${t('Sensor Targets')} ${progress.collectedTargets}/${progress.totalTargets}`} value={progress.collectedTargets} max={progress.totalTargets} />
      <ProgressBar label={t('Collection Success rate')} value={data.successRate} tone="info" />
      <p className="mission-muted">{t('Successful attempts / finished attempts. Retries count; pending collections do not.')}</p>
      <p className="mission-muted">{telemetry ? `${t('Position recorded')} ${displayTime(telemetry.recordedAt)}` : t('No live position stream is connected.')}</p>
      {mission.failureReason && <p className="mission-error">{mission.failureReason}</p>}
      {manage && <div className="monitor-operator-actions">{statuses.length > 0 && <button className="app-ui-button" onClick={() => setDialog('status')}>{t('Update Mission Status')}</button>}<small>{t('Record observations only. No flight commands are sent.')}</small></div>}
    </SectionCard></div>
    <SectionCard title={t('Collection Progress')} description={`${rows.reduce((sum, row) => sum + row.records, 0)} ${t('collected records · Collection does not imply synchronization to the server.')}`}>
      <DataTable caption={t('Collection progress')} rows={rows} columns={[
        { key: 'node', header: t('Sensor'), render: (node) => <div><strong>{node?.deviceCode ?? t('Unknown sensor')}</strong><small className="monitor-cell-subtitle">{node?.name}</small></div> },
        { key: 'point', header: t('Collection Point'), render: (point) => point?.name ?? t('Not configured') },
        { key: 'attempts', header: t('Attempts'), render: (attempts, row) => attempts.length ? <button className="monitor-link" aria-label={`${t('View attempts')} ${row.node?.deviceCode}`} onClick={() => setDetailId(row.id)}>{attempts.length} · {t('View details')}</button> : '0' },
        { key: 'records', header: t('Records') }, { key: 'status', header: t('Status'), render: (value) => <StatusBadge status={value} /> },
        { key: 'lastError', header: t('Last Error'), render: (error, row) => error ? <div><span>{error.errorCode}</span>{row.errorRecovered && <small className="monitor-cell-subtitle">{t('Recovered on retry')}</small>}<button className="monitor-link monitor-cell-subtitle" onClick={() => setDetailId(row.id)}>{t('View Error Reason')}</button></div> : row.status === 'FAILED' ? <button className="monitor-link" onClick={() => setDetailId(row.id)}>{t('View Error Reason')}</button> : t('None recorded') },
      ]} emptyState={<EmptyState title={t('No sensor targets')} description={t('Resume the draft to select sensors for this mission.')} />} />
    </SectionCard>
    <SectionCard title={t('Recorded telemetry')} description={t('Historical positions reported by the device. Missing values are unavailable.')}>
      <DataTable caption={t('Mission telemetry history')} rows={operations.telemetry ?? []} columns={[
        { key: 'recordedAt', header: t('Recorded At'), render: displayTime },
        { key: 'latitude', header: t('Latitude'), render: value => value ?? t('Unavailable') },
        { key: 'longitude', header: t('Longitude'), render: value => value ?? t('Unavailable') },
        { key: 'altitudeM', header: t('Altitude (m)'), render: value => value ?? t('Unavailable') },
        { key: 'batteryPercent', header: t('Battery (%)'), render: value => value ?? t('Unavailable') },
        { key: 'currentWaypointNo', header: t('Waypoint'), render: value => value ?? t('Unavailable') },
        { key: 'flightStatus', header: t('Flight Status'), render: value => value ?? t('Unavailable') },
      ]} emptyState={<EmptyState title={t('No telemetry records')} description={t('Mission telemetry has not been recorded yet.')} />} />
    </SectionCard>
    <div className="monitor-bottom-grid"><SectionCard title={t('Mission Timeline')} description={t('Recorded events in chronological order. Times use Vietnam time.')}>
      {data.timeline.length ? <ol className="monitor-timeline" tabIndex={0} aria-label={t('Mission timeline events')}>{data.timeline.map((event) => <li key={event.id}><span className="monitor-event-dot" data-status={event.status} /><div><strong>{event.title}</strong><p>{event.description}</p>{event.author && <small>{t('Reported by')} {event.author}</small>}<time dateTime={event.at}>{displayTime(event.at)}</time></div></li>)}</ol> : <EmptyState title={t('Mission has not started')} description={t('Collection and waypoint events will appear when recorded.')} />}
    </SectionCard><SectionCard title={t('Operational Notes')} description={t('Original planning notes and appended operator observations.')}>
      <p className="monitor-note-text">{mission.notes || t('No planning notes.')}</p>{data.notes.map((note) => <article className="monitor-note" key={note.id}><p>{note.note}</p><small>{note.actorName} · {displayTime(note.at)}</small></article>)}{!data.notes.length && <p className="mission-muted">{t('No additional operational notes.')}</p>}
    </SectionCard></div>
    {detail && <ManagementDialog title={`${detail.node?.deviceCode} · ${t('Collection Attempts')}`} drawer onClose={() => setDetailId(null)}><p>{detail.point?.name ?? t('Collection point not configured')}</p>{detail.lastError && <p className="mission-error">{detail.lastError.errorCode}: {detail.lastError.errorMessage}{detail.errorRecovered ? ` ${t('Recovered on a later retry.')}` : ''}</p>}{detail.attempts.length ? detail.attempts.map((attempt) => <article className="monitor-attempt" key={attempt.id}><header><strong>{t('Attempt')} {attempt.attemptNo}</strong><StatusBadge status={attempt.displayStatus} /></header><dl><dt>{t('Started')}</dt><dd>{displayTime(attempt.startedAt)}</dd><dt>{t('Finished')}</dt><dd>{displayTime(attempt.finishedAt)}</dd><dt>{t('Records collected')}</dt><dd>{attempt.recordsReceived}</dd><dt>{t('Gateway')}</dt><dd>{operations.gateways.find(({ id }) => id === attempt.gatewayId)?.code ?? t('Unknown')}</dd></dl>{attempt.errorMessage && <p>{attempt.errorCode} · {attempt.errorMessage}</p>}</article>) : <EmptyState title={t('No recorded attempt details')} description={t('No error reason was recorded for this target.')} />}</ManagementDialog>}
    {manage && dialog && <MissionReportDialog mission={mission} statuses={statuses} mode={dialog} onSave={save} onClose={() => setDialog(null)} />}
  </div>
}

export default function MissionDetailPage() {
  const t = useUiText(translations)
  const { id } = useParams()
  const { operations, loading, error, reload } = useOperations()
  const { farmWorkspace } = useOutletContext()
  const data = getMissionMonitoring(Number(id), operations, farmWorkspace)
  if (loading || error) return loading ? <p role="status" className="operations-note">{t('Loading…')}</p> : <div role="alert" className="management-error"><p>{error.message}</p>{reload && <button className="app-ui-button" onClick={reload}>{t('Try again')}</button>}</div>
  if (!data) return <EmptyState title={t('Mission not found')} description={t('This mission is unavailable or outside your authorized workspace.')} actions={<Link to="/app/missions">{t('Back to Missions')}</Link>} />
  return <MissionMonitor key={data.mission.id} data={data} />
}
