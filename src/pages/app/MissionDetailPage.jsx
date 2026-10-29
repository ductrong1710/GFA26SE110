import { useState } from 'react'
import { Link, useNavigate, useOutletContext, useParams } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { getMissionMonitoring, allowedMissionStatuses, supportsTelemetry } from '../../services/api/monitoring'
import { displayTime } from '../../utils/display'
import ApiState from '../../components/app/ApiState'
import { isTello } from '../../services/api/telloPlanning'

import { PageHeader, SectionCard, StatusBadge, DataTable, EmptyState, ProgressBar, BatteryIndicator } from '../../components/app/ui'
import MissionMonitoringMap from '../../components/app/MissionMonitoringMap'
import MissionLaunchPanel from '../../components/app/MissionLaunchPanel'
import ManagementDialog from '../../components/app/ManagementDialog'
import '../../styles/missions.css'
import '../../styles/mission-monitoring.css'

function MissionReportDialog({ mission, statuses, onSave, onClose, mode }) {
  const [note, setNote] = useState('')
  const [status, setStatus] = useState(statuses[0] ?? '')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const submit = async (event) => { event.preventDefault(); setPending(true); try { await onSave({ note, status: mode === 'status' ? status : undefined }); onClose() } catch (cause) { setError(cause.message) } finally { setPending(false) } }
  return <ManagementDialog title={mode === 'status' ? 'Report Mission Status' : 'Add Operational Note'} onClose={onClose}><form className="monitor-report-form" onSubmit={submit}>
    <p>{mission.code} · This updates the mission through the authorized backend workflow.</p>
    {error && <p role="alert" className="mission-error">{error}</p>}
    {mode === 'status' && <><label>Reported status<select aria-label="Reported status" value={status} onChange={(event) => setStatus(event.target.value)} required>{statuses.map((value) => <option key={value} value={value}>{value.replaceAll('_', ' ')}</option>)}</select></label><p className="mission-muted">The backend validates lifecycle transitions and completion requirements.</p></>}
    <label>Operational note<textarea autoFocus required maxLength={2000} rows={5} value={note} onChange={(event) => setNote(event.target.value)} placeholder={mode === 'status' ? 'Describe the observed outcome and reason for this update.' : 'Add field observations or handover details.'} /></label>
    <div className="mission-actions"><button className="app-ui-button" type="button" onClick={onClose}>Cancel</button><button className="app-ui-button app-ui-button--primary" type="submit" disabled={pending}>{mode === 'status' ? 'Save Status Report' : 'Save Note'}</button></div>
  </form></ManagementDialog>
}

function MissionMonitor({ data }) {
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
  const save = async (change) => { await reportMission({ missionId: mission.id, ...change }, farmWorkspace); setNotice('Mission updated successfully.') }
  return <div className="app-ui missions-page mission-monitoring">
    <PageHeader eyebrow={mission.code} title={mission.name} description={`${farm?.name ?? 'Farm not selected'} · Mission monitoring`} actions={<><Link className="app-ui-button" to="/app/missions">Back to Missions</Link>{mission.status === 'PENDING' && can(PERMISSIONS.MISSIONS_CREATE) && <button className="app-ui-button app-ui-button--primary" disabled={resuming} onClick={async () => { setResuming(true); setActionError(''); try { await startMissionPlan(mission); navigate('/app/missions/create') } catch (e) { setActionError(e.message) } finally { setResuming(false) } }}>Resume Draft</button>}</>} />
    <div className="monitor-meta"><StatusBadge status={mission.status} /><div><span>Start time</span><strong>{mission.startedAt ? displayTime(mission.startedAt) : 'Not started'}</strong></div><div><span>Elapsed time</span><strong>{data.elapsed}</strong></div><div><span>Snapshot · Vietnam time</span><strong>{displayTime(telemetry?.recordedAt ?? mission.updatedAt ?? mission.createdAt)}</strong></div></div>
    {actionError && <p role="alert">{actionError}</p>}{notice && <p role="status" className="mission-success">{notice}</p>}
    {isTello(uav) && <MissionLaunchPanel key={`${mission.id}:${mission.planRevision}`} missionId={mission.id} planRevision={mission.planRevision} canManage={manage} />}
    {uav && !supportsTelemetry(uav) && <p className="monitor-telemetry-message" role="status">Live telemetry unavailable. Mission status may be updated manually.</p>}
    <div className="monitor-main-grid"><SectionCard title="Mission Map" description="Planned route and recorded positions. Individual waypoint completion is not supplied by the API." actions={<StatusBadge status={telemetry ? 'INFO' : 'INACTIVE'} label={telemetry ? 'Recorded telemetry' : 'No current position'} />}>
      <MissionMonitoringMap data={data} />
      <div className="monitor-waypoint-summary"><strong>{currentWaypoint ? `Current waypoint: ${currentWaypoint.sequenceNo} of ${progress.totalWaypoints}` : 'Current waypoint: not reported'}</strong><span>Individual waypoint completion is unavailable.</span></div>
      <ol className="monitor-waypoints" aria-label="Waypoint status">{waypoints.map((waypoint) => <li key={waypoint.id} data-state={waypoint.id === currentWaypoint?.id ? 'current' : waypoint.status}><span>{waypoint.sequenceNo}</span><small>{waypoint.id === currentWaypoint?.id ? 'Current' : waypoint.status.toLowerCase()}</small></li>)}</ol>
    </SectionCard><SectionCard title="Mission Status" className="monitor-status-panel">
      <dl className="monitor-equipment"><div><dt>UAV</dt><dd>{uav?.code ?? 'Unassigned'}<small>{uav?.name}</small></dd></div><div><dt>Gateway</dt><dd>{gateway?.code ?? 'Unassigned'}<small>{gateway?.name}</small></dd></div><div><dt>{mission.completedAt ? 'Battery at mission end' : 'UAV battery'}</dt><dd><BatteryIndicator value={data.battery} /></dd></div></dl>
      <p className="mission-muted">{progress.totalWaypoints} planned waypoints · Completion unavailable</p>
      <ProgressBar label={`Sensor Targets ${progress.collectedTargets}/${progress.totalTargets}`} value={progress.collectedTargets} max={progress.totalTargets} />
      <ProgressBar label="Collection Success rate" value={data.successRate} tone="info" />
      <p className="mission-muted">Successful attempts / finished attempts. Retries count; pending collections do not.</p>
      <p className="mission-muted">{telemetry ? `Position recorded ${displayTime(telemetry.recordedAt)}` : 'No live position stream is connected.'}</p>
      {mission.failureReason && <p className="mission-error">{mission.failureReason}</p>}
      {manage && <div className="monitor-operator-actions">{statuses.length > 0 && <button className="app-ui-button" onClick={() => setDialog('status')}>Update Mission Status</button>}<small>Record observations only. No flight commands are sent.</small></div>}
    </SectionCard></div>
    <SectionCard title="Collection Progress" description={`${rows.reduce((sum, row) => sum + row.records, 0)} collected records · Collection does not imply synchronization to the server.`}>
      <DataTable caption="Collection progress" rows={rows} columns={[
        { key: 'node', header: 'Sensor', render: (node) => <div><strong>{node?.deviceCode ?? 'Unknown sensor'}</strong><small className="monitor-cell-subtitle">{node?.name}</small></div> },
        { key: 'point', header: 'Collection Point', render: (point) => point?.name ?? 'Not configured' },
        { key: 'attempts', header: 'Attempts', render: (attempts, row) => attempts.length ? <button className="monitor-link" aria-label={`View attempts for ${row.node?.deviceCode}`} onClick={() => setDetailId(row.id)}>{attempts.length} · View details</button> : '0' },
        { key: 'records', header: 'Records' }, { key: 'status', header: 'Status', render: (value) => <StatusBadge status={value} /> },
        { key: 'lastError', header: 'Last Error', render: (error, row) => error ? <div><span>{error.errorCode}</span>{row.errorRecovered && <small className="monitor-cell-subtitle">Recovered on retry</small>}<button className="monitor-link monitor-cell-subtitle" onClick={() => setDetailId(row.id)}>View Error Reason</button></div> : row.status === 'FAILED' ? <button className="monitor-link" onClick={() => setDetailId(row.id)}>View Error Reason</button> : 'None recorded' },
      ]} emptyState={<EmptyState title="No sensor targets" description="Resume the draft to select sensors for this mission." />} />
    </SectionCard>
    <SectionCard title="Recorded telemetry" description="Historical positions reported by the device. Missing values are unavailable.">
      <DataTable caption="Mission telemetry history" rows={operations.telemetry ?? []} columns={[
        { key: 'recordedAt', header: 'Recorded At', render: displayTime },
        { key: 'latitude', header: 'Latitude', render: value => value ?? 'Unavailable' },
        { key: 'longitude', header: 'Longitude', render: value => value ?? 'Unavailable' },
        { key: 'altitudeM', header: 'Altitude (m)', render: value => value ?? 'Unavailable' },
        { key: 'batteryPercent', header: 'Battery (%)', render: value => value ?? 'Unavailable' },
        { key: 'currentWaypointNo', header: 'Waypoint', render: value => value ?? 'Unavailable' },
        { key: 'flightStatus', header: 'Flight Status', render: value => value ?? 'Unavailable' },
      ]} />
    </SectionCard>
    <div className="monitor-bottom-grid"><SectionCard title="Mission Timeline" description="Recorded events in chronological order. Times use Vietnam time.">
      {data.timeline.length ? <ol className="monitor-timeline" tabIndex={0} aria-label="Mission timeline events">{data.timeline.map((event) => <li key={event.id}><span className="monitor-event-dot" data-status={event.status} /><div><strong>{event.title}</strong><p>{event.description}</p>{event.author && <small>Reported by {event.author}</small>}<time dateTime={event.at}>{displayTime(event.at)}</time></div></li>)}</ol> : <EmptyState title="Mission has not started" description="Collection and waypoint events will appear when recorded." />}
    </SectionCard><SectionCard title="Operational Notes" description="Original planning notes and appended operator observations.">
      <p className="monitor-note-text">{mission.notes || 'No planning notes.'}</p>{data.notes.map((note) => <article className="monitor-note" key={note.id}><p>{note.note}</p><small>{note.actorName} · {displayTime(note.at)}</small></article>)}{!data.notes.length && <p className="mission-muted">No additional operational notes.</p>}
    </SectionCard></div>
    {detail && <ManagementDialog title={`${detail.node?.deviceCode} · Collection Attempts`} drawer onClose={() => setDetailId(null)}><p>{detail.point?.name ?? 'Collection point not configured'}</p>{detail.lastError && <p className="mission-error">{detail.lastError.errorCode}: {detail.lastError.errorMessage}{detail.errorRecovered ? ' Recovered on a later retry.' : ''}</p>}{detail.attempts.length ? detail.attempts.map((attempt) => <article className="monitor-attempt" key={attempt.id}><header><strong>Attempt {attempt.attemptNo}</strong><StatusBadge status={attempt.displayStatus} /></header><dl><dt>Started</dt><dd>{displayTime(attempt.startedAt)}</dd><dt>Finished</dt><dd>{displayTime(attempt.finishedAt)}</dd><dt>Records collected</dt><dd>{attempt.recordsReceived}</dd><dt>Gateway</dt><dd>{operations.gateways.find(({ id }) => id === attempt.gatewayId)?.code ?? 'Unknown'}</dd></dl>{attempt.errorMessage && <p>{attempt.errorCode} · {attempt.errorMessage}</p>}</article>) : <EmptyState title="No recorded attempt details" description="No error reason was recorded for this target." />}</ManagementDialog>}
    {manage && dialog && <MissionReportDialog mission={mission} statuses={statuses} mode={dialog} onSave={save} onClose={() => setDialog(null)} />}
  </div>
}

export default function MissionDetailPage() {
  const { id } = useParams()
  const { operations, loading, error, reload } = useOperations()
  const { farmWorkspace } = useOutletContext()
  const data = getMissionMonitoring(Number(id), operations, farmWorkspace)
  if (loading || error) return <ApiState loading={loading} error={error} reload={reload} />
  if (!data) return <EmptyState title="Mission not found" description="This mission is unavailable or outside your authorized workspace." actions={<Link to="/app/missions">Back to Missions</Link>} />
  return <MissionMonitor key={data.mission.id} data={data} />
}

