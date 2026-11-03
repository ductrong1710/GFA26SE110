import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, time } from '../../services/api/workflow'
import { Workspace, Panel, State, Field, Button, Badge, EvidenceGallery, JobLink, useAction } from '../../components/workflow/WorkflowUi'
import { NewSupportForm, IncidentAssignment } from '../../components/workflow/SupportForms'

export default function SupportPage() {
  const { activeRole } = useAuth()
  const engineer = activeRole === ROLES.AGRICULTURAL_ENGINEER
  const owner = activeRole === ROLES.FARM_OWNER
  const admin = activeRole === ROLES.ADMINISTRATOR
  const [type, setType] = useState(engineer ? 'crop' : 'device')
  const [id, setId] = useState(null)
  const [create, setCreate] = useState(false)
  const path = type === 'crop' ? '/api/crop-condition-reports' : '/api/device-incidents'
  const list = useApiQuery(`support:${path}:${activeRole}`, signal => allPages(path, {}, signal))
  const query = useApiQuery(`support-detail:${path}:${id}`, signal => api.get(`${path}/${id}`, {}, signal), Boolean(id))
  const context = useApiQuery(`crop-context:${id}:${query.data?.revision}`, async signal => ({ readings: await allPages(`${path}/${id}/sensor-readings`, {}, signal), seasons: await allPages(`${path}/${id}/crop-seasons`, {}, signal) }), type === 'crop' && Boolean(query.data))
  const refresh = () => { list.reload(); query.reload(); setCreate(false) }
  const action = useAction(refresh)
  const row = query.data
  return <Workspace title={engineer ? 'Tư vấn tình trạng cây trồng' : 'Báo cáo & hỗ trợ'} description="Tách báo cáo cây bệnh và sự cố thiết bị để chuyển đúng người phụ trách.">
    <div className="wf-toolbar"><Field title="Loại báo cáo"><select value={type} onChange={e => { setType(e.target.value); setId(null); setCreate(false) }}><option value="crop">Cây bệnh / tình trạng cây</option>{!engineer && <option value="device">Không nhận dữ liệu / thiết bị lỗi</option>}</select></Field>{owner && <Button primary onClick={() => setCreate(value => !value)}>{create ? 'Đóng biểu mẫu' : 'Gửi báo cáo mới'}</Button>}</div>
    {create && <Panel title="Thông tin cần hỗ trợ"><NewSupportForm key={type} type={type} onDone={refresh} /></Panel>}
    <div className="wf-two-col"><Panel title="Danh sách báo cáo"><State query={list}><ul className="wf-list">{list.data?.map(row => <li key={row.id}><div><Badge value={row.status} /><p>{row.description}</p><small>Nông trại #{row.farmId} · {time(row.reportedAt)}</small></div><Button onClick={() => setId(row.id)}>Xem #{row.id}</Button></li>)}</ul>{!list.data?.length && <p className="wf-empty">Chưa có báo cáo thuộc phạm vi của bạn.</p>}</State></Panel>
      <Panel title={id ? `Chi tiết báo cáo #${id}` : 'Chọn báo cáo để xem'}><State query={query}>{row && <>
        <Badge value={row.status} /><p>{row.description}</p><p>{row.crop ? `Loại cây: ${row.crop}` : row.deviceCode ? `Thiết bị đã xác định: ${row.deviceCode}` : 'Thiết bị chưa xác định — cần bộ phận kỹ thuật kiểm tra.'}</p><EvidenceGallery images={row.images} />
        {type === 'device' && row.serviceJobId && <JobLink kind="service" id={row.serviceJobId}>Mở công việc xử lý →</JobLink>}
        {type === 'device' && admin && !row.serviceJobId && <IncidentAssignment incident={row} onDone={refresh} />}
        {type === 'crop' && <>
          {engineer && row.status === 'NEW' && <Button disabled={action.busy} onClick={() => action.run(() => api.post(`${path}/${id}/claim`))}>Nhận xử lý báo cáo cây</Button>}
          {((engineer && ['IN_REVIEW', 'AWAITING_INFORMATION'].includes(row.status)) || (owner && row.status === 'AWAITING_INFORMATION')) && <form className="wf-form" onSubmit={e => { e.preventDefault(); const form = new FormData(e.currentTarget); action.run(() => api.post(`${path}/${id}/${form.get('action')}`, { expectedRevision: row.revision, content: form.get('content') })) }}>
            <Field title="Cập nhật xử lý"><select name="action">{owner ? <option value="reply">Bổ sung thông tin</option> : <><option value="recommend">Tư vấn / đề xuất</option><option value="request-information">Yêu cầu khách bổ sung</option><option value="resolve">Đánh dấu đã giải quyết</option></>}</select></Field><Field title="Nội dung"><textarea name="content" required maxLength={4000} rows={3} /></Field><Button type="submit" disabled={action.busy}>Gửi cập nhật</Button>
          </form>}
          {action.errorView}<ol className="wf-timeline">{row.updates?.map(update => <li key={update.id}><strong>{update.action}</strong><p>{update.content}</p><small>{time(update.createdAt)}</small><EvidenceGallery images={update.images} /></li>)}</ol>
          <details><summary>Dữ liệu cảm biến và mùa vụ liên quan</summary><State query={context}>{context.data?.readings.length ? <ul className="wf-list">{context.data.readings.slice(0, 20).map(reading => <li key={reading.id}><span>{reading.channelCode}</span><span>{reading.value} {reading.unit} · {time(reading.measuredAt)}{!reading.isValid && ' · Không hợp lệ'}</span></li>)}</ul> : <p>Không có dữ liệu cảm biến được đính kèm.</p>}<ul>{context.data?.seasons.map(season => <li key={season.id}>{season.name} · {season.crop}</li>)}</ul></State></details>
        </>}
      </>}</State></Panel></div>
  </Workspace>
}
