import { useState } from 'react'
import { Link, useNavigate, useOutletContext } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { emptyMissionPlan, groupCollectionPoints, availableUavs, availableGateways, validateMissionPlan } from '../../data/mock/missionPlanning'
import { getGatewayLastSync } from '../../data/mock/operationsSelectors'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, SectionCard, DataTable, StatusBadge, BatteryIndicator, EmptyState } from '../../components/app/ui'
import MissionRouteMap from '../../components/app/MissionRouteMap'
import WaypointEditor from '../../components/app/WaypointEditor'
import '../../styles/missions.css'

const steps = ['Select Farm', 'Select Sensor Nodes', 'Define Collection Points', 'Define Waypoints', 'Review Route', 'Assign UAV', 'Assign Mobile Gateway', 'Review & Validate']

export default function CreateMissionPage() {
  const { operations, missionWizard, setMissionWizard, savePlannedMission } = useOperations()
  const { farmWorkspace } = useOutletContext()
  const navigate = useNavigate()
  const { step, plan } = missionWizard
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const update = (values) => { setMissionWizard({ step, plan: { ...plan, ...values } }); setError(''); setNotice('') }
  const go = (value) => { setMissionWizard({ step: value, plan }); setError(''); setNotice('') }
  const farm = farmWorkspace.farms.find(({ id }) => id === Number(plan.farmId))
  const nodes = operations.sensorNodes.filter((node) => farmWorkspace.zones.find(({ id }) => id === node.zoneId)?.farmId === farm?.id)
  const selected = nodes.filter(({ id }) => plan.sensorIds.includes(id))
  const errors = validateMissionPlan(plan, operations, farmWorkspace)
  const setPoints = (points) => update({ points, waypoints: [] })
  const changePoint = (id, values) => setPoints(plan.points.map((point) => point.id === id ? { ...point, ...values } : point))
  const save = (draft) => { try { const id = savePlannedMission(farmWorkspace, draft); if (draft) setNotice('Draft saved. You can leave this page and resume it from Missions during this session.'); else navigate(`/app/missions/${id}`) } catch (cause) { setError(cause.message) } }
  const next = () => { const problems = errors.slice(0, step).flat(); if (problems.length) setError(problems.join(' ')); else go(step + 1) }
  const routeMap = <MissionRouteMap farm={farm} nodes={selected} points={plan.points} waypoints={plan.waypoints} />
  const chosenUav = operations.uavs.find(({ id }) => id === Number(plan.uavId))
  const chosenGateway = operations.gateways.find(({ id }) => id === Number(plan.gatewayId))
  return <div className="app-ui missions-page">
    <PageHeader eyebrow="MISSION PLANNING" title={plan.missionId ? 'Continue Mission Draft' : 'Create Mission'} description="Build a sensor collection plan, then validate its route and equipment." actions={<Link className="app-ui-button" to="/app/missions">Back to Missions</Link>} />
    <ol className="mission-steps" aria-label="Mission planning steps">{steps.map((label, index) => <li key={label}><button aria-current={step === index + 1 ? 'step' : undefined} disabled={index + 1 > step} onClick={() => go(index + 1)}><span>{index + 1}</span>{label}</button></li>)}</ol>
    <p className="mission-muted">Step {step} of 8 · Saved locally for this session. Refreshing the browser resets demo changes.</p>
    {notice && <p role="status" className="mission-success">{notice}</p>}{error && <p role="alert" className="mission-error">{error}</p>}
    <SectionCard title={steps[step - 1]}>
      {step === 1 && <><label className="mission-field">Mission name<input maxLength={120} value={plan.name} onChange={(event) => update({ name: event.target.value })} placeholder="e.g. Afternoon Soil Collection" /></label><div className="mission-choices">{farmWorkspace.farms.map((item) => <label className="mission-choice" key={item.id}><input type="radio" name="farm" checked={Number(plan.farmId) === item.id} onChange={() => update({ ...emptyMissionPlan(), missionId: plan.missionId, name: plan.name, farmId: item.id })} /><strong>{item.name}</strong><span>{item.description}</span><small>{farmWorkspace.zones.filter(({ farmId }) => farmId === item.id).length} zones · {item.latitude}, {item.longitude}</small></label>)}</div></>}
      {step === 2 && <><p>{farm?.name} · {selected.length} selected. Disabled sensors cannot be targeted.</p><DataTable caption="Select sensor targets" rows={nodes} columns={[
        { key: 'select', header: 'Select', render: (_, node) => <input type="checkbox" aria-label={`Select ${node.deviceCode}`} disabled={!node.isActive} checked={plan.sensorIds.includes(node.id)} onChange={(event) => update({ sensorIds: event.target.checked ? [...plan.sensorIds, node.id] : plan.sensorIds.filter((id) => id !== node.id), points: [], waypoints: [] })} /> },
        { key: 'deviceCode', header: 'Device Code' }, { key: 'name', header: 'Name' }, { key: 'zoneId', header: 'Zone', render: (id) => farmWorkspace.zones.find((zone) => zone.id === id)?.name },
        { key: 'status', header: 'Status', render: (value) => <StatusBadge status={value} /> }, { key: 'batteryPercent', header: 'Battery', render: (value) => <BatteryIndicator value={value} /> },
      ]} /></>}
      {step === 3 && <><p>Group nearby sensors into collection points. Demo grouping uses 100 m proximity; each target must be within 150 m of its point. Editing points clears the waypoint plan.</p>
        <div className="mission-actions"><button className="app-ui-button" onClick={() => setPoints(groupCollectionPoints(selected))}>Auto-group Sensors</button><button className="app-ui-button" onClick={() => setPoints([...plan.points, { id: Math.max(0, ...plan.points.map(({ id }) => id)) + 1, name: `Collection Point ${plan.points.length + 1}`, latitude: farm.latitude, longitude: farm.longitude, sensorIds: [] }])}>Add Collection Point</button></div>
        {routeMap}<div className="mission-choices">{plan.points.map((point) => <div className="mission-point" key={point.id}><label>Name<input aria-label={`Point ${point.id} name`} value={point.name} onChange={(event) => changePoint(point.id, { name: event.target.value })} /></label><label>Latitude<input aria-label={`Point ${point.id} latitude`} type="number" step="any" value={point.latitude} onChange={(event) => changePoint(point.id, { latitude: event.target.value })} /></label><label>Longitude<input aria-label={`Point ${point.id} longitude`} type="number" step="any" value={point.longitude} onChange={(event) => changePoint(point.id, { longitude: event.target.value })} /></label><small>{point.sensorIds.length} sensor targets</small><button className="app-ui-button" onClick={() => setPoints(plan.points.filter(({ id }) => id !== point.id))}>Remove {point.name}</button></div>)}</div>
        <div className="mission-assignments">{selected.map((node) => <label key={node.id}>{node.deviceCode}<select aria-label={`Collection point for ${node.deviceCode}`} value={plan.points.find(({ sensorIds }) => sensorIds.includes(node.id))?.id ?? ''} onChange={(event) => setPoints(plan.points.map((point) => ({ ...point, sensorIds: [...point.sensorIds.filter((id) => id !== node.id), ...(point.id === Number(event.target.value) ? [node.id] : [])] })))}><option value="">Unassigned</option>{plan.points.map((point) => <option key={point.id} value={point.id}>{point.name}</option>)}</select></label>)}</div></>}
      {step === 4 && <WaypointEditor farm={farm} points={plan.points} waypoints={plan.waypoints} onChange={(waypoints) => update({ waypoints })} />}
      {step === 5 && <>{routeMap}<p>{plan.waypoints.length} waypoints · {plan.points.length} collection points · {selected.length} sensor targets</p><ol className="mission-route-list">{plan.waypoints.map((waypoint) => <li key={waypoint.id}>{waypoint.action.replaceAll('_', ' ')} · {waypoint.coordinateType} ({waypoint.x}, {waypoint.y}) · {waypoint.altitude} m</li>)}</ol>{errors[4].map((message) => <p className="mission-error" key={message}>{message}</p>)}</>}
      {step === 6 && <><p>Available equipment in {farm?.name}. UAVs must be ready, not currently on a mission, and have more than 20% battery.</p><div className="mission-choices">{availableUavs(operations, plan.farmId).map((uav) => <label className="mission-choice" key={uav.id}><input type="radio" name="uav" checked={Number(plan.uavId) === uav.id} onChange={() => update({ uavId: uav.id, gatewayId: '' })} /><strong>{uav.code} · {uav.name}</strong><StatusBadge status={uav.status} /><BatteryIndicator value={uav.batteryPercent} /><span>GPS: {uav.gpsSupport ? 'Supported' : 'No'} · Telemetry: {uav.telemetrySupport ? 'Supported' : 'No'}</span></label>)}</div>{!availableUavs(operations, plan.farmId).length && <EmptyState title="No available UAVs" description="Save a draft and return when equipment is ready." />}</>}
      {step === 7 && <><p>Online gateways in this farm, unassigned or paired with {chosenUav?.code}.</p><div className="mission-choices">{availableGateways(operations, plan.farmId, plan.uavId).map((gateway) => <label className="mission-choice" key={gateway.id}><input type="radio" name="gateway" checked={Number(plan.gatewayId) === gateway.id} onChange={() => update({ gatewayId: gateway.id })} /><strong>{gateway.code} · {gateway.name}</strong><StatusBadge status={gateway.status} /><span>Assigned UAV: {operations.uavs.find(({ id }) => id === gateway.uavId)?.code ?? 'Unassigned'}</span><small>Last sync: {displayTime(getGatewayLastSync(gateway.id, operations.syncBatches))}</small></label>)}</div>{!availableGateways(operations, plan.farmId, plan.uavId).length && <EmptyState title="No compatible gateway" description="Choose another UAV or save a draft until a gateway is available." />}</>}
      {step === 8 && <><div className="mission-form-grid"><label>Mission name<input maxLength={120} value={plan.name} onChange={(event) => update({ name: event.target.value })} /></label><label>Scheduled time (Vietnam, optional)<input type="datetime-local" step="60" value={plan.scheduledLocal} onChange={(event) => update({ scheduledLocal: event.target.value })} /></label><label>Operational notes<textarea value={plan.notes} onChange={(event) => update({ notes: event.target.value })} /></label></div>
        <p>{farm?.name} · {selected.length} targets · {plan.points.length} collection points · {plan.waypoints.length} waypoints</p><p>UAV: {chosenUav?.code ?? 'Unassigned'} · Gateway: {chosenGateway?.code ?? 'Unassigned'}</p>{routeMap}
        <ul className="mission-validation">{steps.map((label, index) => <li key={label}><StatusBadge status={errors[index].length ? 'ERROR' : 'SUCCESS'} label={errors[index].length ? 'Needs attention' : 'Valid'} /><div><strong>{label}</strong>{errors[index].map((message) => <p key={message}>{message}</p>)}</div>{index < 7 && <button className="app-ui-button" onClick={() => go(index + 1)}>Review</button>}</li>)}</ul><p>Saving a validated plan sets it to READY. It does not start the mission. Scheduling uses the demo snapshot of 6 October 2026, 08:18 Vietnam time.</p></>}
    </SectionCard>
    <footer className="mission-footer"><button className="app-ui-button" disabled={step === 1} onClick={() => go(step - 1)}>Back</button><span className="mission-footer-spacer" /><button className="app-ui-button" onClick={() => save(true)}>Save Draft</button>{step < 8 ? <button className="app-ui-button app-ui-button--primary" onClick={next}>Next</button> : <button className="app-ui-button app-ui-button--primary" disabled={errors.flat().length > 0} onClick={() => save(false)}>Save Mission</button>}</footer>
  </div>
}
