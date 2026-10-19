import { useState } from 'react'
import ManagementDialog from './ManagementDialog'
import { GATEWAY_TYPES, deviceAssignmentLocked } from '../../services/api/selectors'

export default function EquipmentDialog({ kind, record, mode = 'edit', workspace, operations, currentFarm, onSave, onClose }) {
  const [pending, setPending] = useState(false)
  const originalFarm = kind === 'sensor' ? workspace.zones.find(({ id }) => id === record?.zoneId)?.farmId : record?.farmId
  const farmId = originalFarm ?? currentFarm.id ?? workspace.farms[0]?.id ?? ''
  const [values, setValues] = useState(() => ({ farmId, zoneId: workspace.zones.find((zone) => zone.farmId === farmId)?.id ?? '',
    deviceCode: '', code: '', name: '', model: '', gatewayType: 'RASPBERRY_PI', softwareVersion: '', isActive: true, status: 'OFFLINE', latitude: '', longitude: '', localX: '', localY: '',
    ...record, ...(mode === 'toggle' ? { isActive: !record.isActive } : {}) }))
  const [error, setError] = useState('')
  const label = kind === 'sensor' ? 'Sensor' : kind === 'uav' ? 'UAV' : 'Gateway'
  const title = mode === 'assign' ? kind === 'sensor' ? 'Assign to Zone' : 'Assign / Unassign Gateway'
    : mode === 'toggle' ? `${values.isActive ? 'Enable' : 'Disable'} ${label}` : `${record ? 'Edit' : 'Register'} ${label}`
  const change = (key, value) => setValues({ ...values, [key]: value })
  const text = (key, name, props = {}) => <label>{name}<input value={values[key] ?? ''} onChange={(event) => change(key, event.target.value)} maxLength={120} {...props} /></label>
  const select = (key, name, options, props = {}) => <label>{name}<select aria-label={name} value={values[key] ?? ''} onChange={(event) => change(key, event.target.value)} {...props}>{options.map(([value, title]) => <option key={value} value={value}>{title}</option>)}</select></label>
  const zones = workspace.zones.filter((zone) => zone.farmId === Number(values.farmId))
  const assignmentLocked = kind === 'gateway' && record && deviceAssignmentLocked('gateway', record.id, operations.missions)
  const chooseFarm = <label>Farm<select aria-label="Farm" required disabled={Boolean(record)} value={values.farmId} onChange={(event) => setValues({ ...values, farmId: event.target.value, zoneId: workspace.zones.find((zone) => zone.farmId === Number(event.target.value))?.id ?? '' })}>{workspace.farms.map((farm) => <option key={farm.id} value={farm.id}>{farm.name}</option>)}</select></label>
  return <ManagementDialog title={title} onClose={onClose}><form onSubmit={async (event) => { event.preventDefault(); setPending(true); try { await onSave(values) } catch (err) { setError(err.message) } finally { setPending(false) } }}>
    <div className="management-form">
      {mode === 'toggle' ? <p>{values.isActive ? 'Enable' : 'Disable'} <strong>{record.deviceCode ?? record.code}</strong> for future collection? Existing readings and contact status are retained.</p>
        : mode === 'assign' && kind === 'gateway' ? <>
          <p>{record.code} · {workspace.farms.find(({ id }) => id === record.farmId)?.name}</p>
          {select('uavId', 'Assigned UAV', [['', 'Unassigned'], ...operations.uavs.filter((uav) => uav.farmId === record.farmId).map((uav) => [uav.id, `${uav.code} — ${uav.name}`])], { disabled: assignmentLocked })}
          {assignmentLocked && <p className="management-note">This gateway is part of an active mission. Its assignment is locked until the mission ends.</p>}
        </> : <>
          {mode !== 'assign' && <>{text(kind === 'sensor' ? 'deviceCode' : 'code', 'Device Code', { required: true, disabled: Boolean(record), maxLength: 40 })}{text('name', 'Name', { required: true })}</>}
          {kind === 'sensor' && chooseFarm}
          {kind === 'sensor' ? <>
            {select('zoneId', 'Zone', [['', 'Select a zone'], ...zones.map((zone) => [zone.id, zone.name])], { required: true })}
          </> : kind === 'uav' ? <>
            {text('model', 'Model', { required: true })}
          </> : <>{select('gatewayType', 'Device Type', GATEWAY_TYPES.map((item) => [item, item.replaceAll('_', ' ')]))}{text('softwareVersion', 'Software Version', { required: true, maxLength: 40 })}<p className="management-note">Register the gateway first, then assign it to a UAV from the authorized inventory.</p></>}
          {!record && <p className="management-note">New equipment has no telemetry yet. Registration does not simulate a connection or create sensor channels.</p>}
        </>}
      {mode !== 'toggle' && mode !== 'assign' && <>{text('status', 'Observed status', { required: true })}{kind === 'sensor' && <>{text('latitude', 'Latitude', { type: 'number', step: 'any' })}{text('longitude', 'Longitude', { type: 'number', step: 'any' })}{text('localX', 'Local X', { type: 'number', step: 'any' })}{text('localY', 'Local Y', { type: 'number', step: 'any' })}</>}</>}
      {error && <p className="management-error" role="alert">{error}</p>}
    </div>
    <footer><button type="button" className="app-ui-button" onClick={onClose}>Cancel</button><button type="submit" className="app-ui-button app-ui-button--primary" disabled={pending || (mode === 'assign' && assignmentLocked)}>Save</button></footer>
  </form></ManagementDialog>
}
