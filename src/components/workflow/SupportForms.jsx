import { useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages } from '../../services/api/workflow'
import { Field, Button, State, useAction } from './WorkflowUi'
import EvidenceUpload from './EvidenceUpload'
import AssignmentForm from './AssignmentForm'

export function NewSupportForm({ type, onDone }) {
  const { farmWorkspace } = useOutletContext()
  const [farmId, setFarmId] = useState('')
  const [images, setImages] = useState([])
  const [channels, setChannels] = useState([])
  const sensors = useApiQuery(`crop-channels:${farmId}`, async signal => {
    const nodes = await allPages('/api/sensor-nodes', { farmId: Number(farmId) }, signal)
    return (await Promise.all(nodes.map(async node => (await allPages(`/api/sensor-nodes/${node.id}/channels`, {}, signal)).map(channel => ({ ...channel, nodeName: node.name }))))).flat()
  }, type === 'crop' && Boolean(farmId))
  const action = useAction(onDone)
  return <form className="wf-form" onSubmit={event => {
    event.preventDefault(); const form = new FormData(event.currentTarget)
    const path = type === 'device' ? '/api/device-incidents' : '/api/crop-condition-reports'
    action.run(() => api.post(path, { farmId: Number(farmId), description: form.get('description'), images,
      ...(type === 'device' ? { rentalDeviceAssignmentId: null } : { crop: form.get('crop'), zoneId: null, sensorChannelIds: channels }),
    }))
  }}>
    <Field title="Nông trại"><select required value={farmId} onChange={e => { setFarmId(e.target.value); setChannels([]) }}><option value="">Chọn nông trại</option>{farmWorkspace.farms.map(farm => <option key={farm.id} value={farm.id}>{farm.name}</option>)}</select></Field>
    {type === 'crop' && <Field title="Loại cây" name="crop" required maxLength={200} />}
    {type === 'crop' && farmId && <fieldset><legend>Cảm biến liên quan (nếu có, tối đa 50 kênh)</legend><State query={sensors}>{sensors.data?.map(channel => <label key={channel.id}><input type="checkbox" checked={channels.includes(channel.id)} disabled={!channels.includes(channel.id) && channels.length >= 50} onChange={e => setChannels(old => e.target.checked ? [...old, channel.id] : old.filter(id => id !== channel.id))} />{channel.nodeName} · {channel.name ?? channel.channelCode}</label>)}{!sensors.data?.length && <p>Chưa có cảm biến thuộc nông trại này.</p>}</State></fieldset>}
    <Field title={type === 'device' ? 'Hiện tượng bạn đang gặp' : 'Triệu chứng và tình trạng cây'}><textarea name="description" required maxLength={4000} rows={4} placeholder={type === 'device' ? 'Ví dụ: cảm biến không cập nhật dữ liệu, UAV không gửi thông tin…' : ''} /></Field>
    {type === 'device' && <p className="wf-muted">Không cần mã máy hay số serial. Bộ phận kỹ thuật sẽ xác định thiết bị và nguyên nhân.</p>}
    <EvidenceUpload title="Ảnh minh họa (không bắt buộc)" value={images} onChange={setImages} />{action.errorView}<Button primary type="submit" disabled={action.busy}>Gửi báo cáo hỗ trợ</Button>
  </form>
}
export function IncidentAssignment({ incident, onDone }) {
  const [assignmentId, setAssignmentId] = useState(incident.rentalDeviceAssignmentId ?? '')
  const [type, setType] = useState('REPAIR')
  const query = useApiQuery(`incident-equipment:${incident.farmId}`, async signal => {
    const rentals = (await allPages('/api/rental-contracts', {}, signal)).filter(row => row.farmId === incident.farmId && row.status !== 'ENDED')
    const details = await Promise.all(rentals.map(row => api.get(`/api/rental-contracts/${row.id}`, {}, signal)))
    return details.flatMap(row => row.devices).filter(row => !row.returnedAt)
  }, !incident.rentalDeviceAssignmentId)
  return <><Field title="Loại công việc"><select value={type} onChange={e => setType(e.target.value)}><option value="REPAIR">Sửa chữa</option><option value="MAINTENANCE">Bảo trì</option></select></Field>
    {!incident.rentalDeviceAssignmentId && <State query={query}><Field title="Thiết bị được xác định qua kiểm tra"><select required value={assignmentId} onChange={e => setAssignmentId(e.target.value)}><option value="">Chọn thiết bị thực tế</option>{query.data?.map(row => <option key={row.id} value={row.id}>{row.name} · {row.code}</option>)}</select></Field></State>}
    {assignmentId ? <AssignmentForm path={`/api/device-incidents/${incident.id}/jobs`} type={type} kind="service" assignmentId={assignmentId} onDone={onDone} /> : <p>Chọn thiết bị để tiếp tục phân công.</p>}
  </>
}
