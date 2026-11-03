import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages } from '../../services/api/workflow'
import { Field, Button, State, useAction } from './WorkflowUi'

export default function AllocationForm({ job, onDone }) {
  const query = useApiQuery(`installation-options:${job.id}`, async signal => {
    const [sensors, uavs, gateways] = await Promise.all([allPages('/api/sensor-nodes', { farmId: job.farmId }, signal), allPages('/api/uavs', {}, signal), allPages('/api/gateways', {}, signal)])
    return [...sensors.map(d => ({ ...d, key: `sensor-${d.id}`, sensorNodeId: d.id })), ...uavs.map(d => ({ ...d, key: `uav-${d.id}`, uavId: d.id })), ...gateways.map(d => ({ ...d, key: `gateway-${d.id}`, gatewayId: d.id }))].filter(d => d.isActive)
  })
  const [selected, setSelected] = useState({})
  const action = useAction(onDone)
  const slots = job.lines.flatMap(line => Array.from({ length: line.quantity }, (_, index) => ({ line, key: `${line.id}-${index}` })))
  return <State query={query}><form className="wf-form" onSubmit={event => {
    event.preventDefault(); const data = new FormData(event.currentTarget)
    action.run(() => api.put(`/api/installation-jobs/${job.id}/devices`, { expectedRevision: job.revision, note: data.get('note'), devices: slots.map(slot => {
      const device = query.data.find(d => d.key === selected[slot.key]); return { proposalLineId: slot.line.id, sensorNodeId: device.sensorNodeId ?? null, uavId: device.uavId ?? null, gatewayId: device.gatewayId ?? null }
    }) }))
  }}>
    {slots.map(slot => <Field key={slot.key} title={`${slot.line.productName ?? slot.line.deviceType} · ${slot.key}`}><select required value={selected[slot.key] ?? ''} onChange={e => setSelected(old => ({ ...old, [slot.key]: e.target.value }))}><option value="">Chọn thiết bị vật lý</option>{query.data?.map(device => <option key={device.key} value={device.key} disabled={Object.values(selected).includes(device.key) && selected[slot.key] !== device.key}>{device.name} · {device.code ?? device.deviceCode}</option>)}</select></Field>)}
    <Field title="Ghi chú cấp phát" name="note" required maxLength={2000} /><small>BE kiểm tra thiết bị còn khả dụng và không bị giữ cho ca khác khi lưu.</small>{action.errorView}<Button type="submit" disabled={action.busy || !slots.length}>Lưu thiết bị cấp phát</Button>
  </form></State>
}
