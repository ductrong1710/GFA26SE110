import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { displayTime } from '../../utils/display'
import { SectionCard } from './ui'

const labels = {
  WAITING_FOR_GATEWAY: 'Waiting for the gateway to download this plan and report ready.',
  AWAITING_CONFIRMATION: 'Gateway ready. Confirm the aircraft and flight area before departure.',
  WAITING_FOR_TIME: 'Confirmed. The gateway will start preparing at the scheduled time.',
  READY_TO_START: 'Confirmed and due. Waiting for gateway acceptance.',
  CONSUMED: 'Gateway accepted the launch. Takeoff must be verified from aircraft telemetry.',
  MISSED: 'Launch window expired. This mission will not start late; create or reschedule a mission.',
  TERMINAL: 'Mission closed. No launch will be sent.',
  UNAVAILABLE: 'Launch is unavailable for this mission status.',
}

export default function MissionLaunchPanel({ missionId, planRevision, canManage }) {
  const query = useApiQuery(`mission-launch:${missionId}`, signal => api.get(`/api/missions/${missionId}/launch`, {}, signal), true, 5000)
  const [aircraft, setAircraft] = useState(false)
  const [area, setArea] = useState(false)
  const [confirmedVersion, setConfirmedVersion] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const launch = query.data
  const version = launch ? `${launch.planRevision}:${launch.session}` : ''
  const sameVersion = confirmedVersion === version
  const changeCheck = (kind, checked) => {
    if (!sameVersion) { setAircraft(false); setArea(false); setConfirmedVersion(version) }
    if (kind === 'aircraft') setAircraft(checked)
    else setArea(checked)
  }
  const confirm = async event => {
    event.preventDefault()
    if (!sameVersion || !aircraft || !area || planRevision !== launch.planRevision) return
    setPending(true); setError('')
    try {
      const updated = await api.post(`/api/missions/${missionId}/launch/approve`, { confirmAircraftReady: aircraft, confirmAreaClear: area, planRevision: launch.planRevision, session: launch.session })
      query.replaceData(updated)
      setAircraft(false); setArea(false)
    } catch (cause) { setError(cause.message) }
    finally { setPending(false) }
  }
  return <SectionCard title="Scheduled flight launch">
    <p role="status">{launch ? labels[launch.state] ?? launch.state : 'Loading launch readiness…'}</p>
    {launch && <p>Scheduled: {displayTime(launch.scheduledStartAt)} · Start deadline: {displayTime(launch.expiresAt)}</p>}
    <p>Keep the laptop connected to Internet Wi-Fi or its hotspot. Confirm within 10 minutes before departure. The gateway switches to Tello automatically after accepting the launch.</p>
    <p>While connected to Tello, the gateway cannot receive an immediate stop command from this web page. Keep the aircraft in sight and the local landing control accessible.</p>
    {(query.error || error) && <p role="alert">{error || query.error.message}</p>}
    {launch && planRevision !== launch.planRevision && <p role="status">The mission plan changed. Waiting for the updated route before confirmation.</p>}
    {canManage && planRevision === launch?.planRevision && launch?.state === 'AWAITING_CONFIRMATION' && <form className="monitor-report-form" onSubmit={confirm}>
      <label><input type="checkbox" checked={sameVersion && aircraft} onChange={e => changeCheck('aircraft', e.target.checked)} /> Aircraft is powered on, on the ground, and ready for flight</label>
      <label><input type="checkbox" checked={sameVersion && area} onChange={e => changeCheck('area', e.target.checked)} /> Flight area is clear and I will supervise the flight</label>
      <button className="app-ui-button app-ui-button--primary" type="submit" disabled={pending || !!query.error || !sameVersion || !aircraft || !area}>Confirm scheduled flight</button>
    </form>}
  </SectionCard>
}
