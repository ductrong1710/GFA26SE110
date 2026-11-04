import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages } from "../../services/api/workflow"
import { Workspace, Panel, State, Field, Button, Badge, EvidenceGallery, JobLink, useAction } from '../../components/workflow/WorkflowUi'
import { NewSupportForm, IncidentAssignment } from '../../components/workflow/SupportForms'

export default function SupportPage() {
  const t = useWorkflowText()
  const { time } = useWorkflowFormat()

  const { activeRole } = useAuth()
  const engineer = activeRole === ROLES.AGRICULTURAL_ENGINEER
  const owner = activeRole === ROLES.FARM_OWNER
  const admin = activeRole === ROLES.ADMINISTRATOR
  const [type, setType] = useState('')
  const [id, setId] = useState(null)
  const [create, setCreate] = useState(false)
  const path = type === 'crop' ? '/api/crop-condition-reports' : '/api/device-incidents'
  const list = useApiQuery(`support:${path}:${activeRole}`, signal => allPages(path, {}, signal), Boolean(type))
  const query = useApiQuery(`support-detail:${path}:${id}`, signal => api.get(`${path}/${id}`, {}, signal), Boolean(id))
  const context = useApiQuery(`crop-context:${id}:${query.data?.revision}`, async signal => ({ readings: await allPages(`${path}/${id}/sensor-readings`, {}, signal), seasons: await allPages(`${path}/${id}/crop-seasons`, {}, signal) }), type === 'crop' && Boolean(query.data))
  const refresh = () => { list.reload(); query.reload(); setCreate(false) }
  const action = useAction(refresh)
  const row = query.data
  return <Workspace title={engineer ? t("Tư vấn tình trạng cây trồng") : t("Báo cáo & hỗ trợ")} description={t("Tách báo cáo cây bệnh và sự cố thiết bị để chuyển đúng người phụ trách.")}>
    <div className="wf-toolbar"><Field title={t("Loại báo cáo")}><select value={type} onChange={e => { setType(e.target.value); setId(null); setCreate(false) }}><option value="" disabled>{owner ? 'Chọn loại báo cáo' : 'Select report type'}</option><option value="crop">{t("Cây bệnh / tình trạng cây")}</option>{!engineer && <option value="device">{t("Không nhận dữ liệu / thiết bị lỗi")}</option>}</select></Field>{owner && type && <Button primary onClick={() => setCreate(value => !value)}>{create ? t("Đóng biểu mẫu") : t("Gửi báo cáo mới")}</Button>}</div>
    {create && <Panel title={t("Thông tin cần hỗ trợ")}><NewSupportForm key={type} type={type} onDone={refresh} /></Panel>}
    {type && <div className="wf-two-col"><Panel title={t("Danh sách báo cáo")}><State query={list}><ul className="wf-list">{list.data?.map(row => <li key={row.id}><div><Badge value={row.status} /><p>{row.description}</p><small>{t("Nông trại #")}{row.farmId} · {time(row.reportedAt)}</small></div><Button onClick={() => setId(row.id)}>{t("Xem #")}{row.id}</Button></li>)}</ul>{!list.data?.length && <p className="wf-empty">{t("Chưa có báo cáo thuộc phạm vi của bạn.")}</p>}</State></Panel>
      <Panel title={id ? (t("Chi tiết báo cáo #") + (id)) : t("Chọn báo cáo để xem")}><State query={query}>{row && <>
        <Badge value={row.status} /><p>{row.description}</p><p>{row.crop ? (t("Loại cây: ") + (row.crop)) : row.deviceCode ? (t("Thiết bị đã xác định: ") + (row.deviceCode)) : t("Thiết bị chưa xác định — cần bộ phận kỹ thuật kiểm tra.")}</p><EvidenceGallery images={row.images} />
        {type === 'device' && row.serviceJobId && <JobLink kind="service" id={row.serviceJobId}>{t("Mở công việc xử lý →")}</JobLink>}
        {type === 'device' && admin && !row.serviceJobId && <IncidentAssignment incident={row} onDone={refresh} />}
        {type === 'crop' && <>
          {engineer && row.status === 'NEW' && <Button disabled={action.busy} onClick={() => action.run(() => api.post(`${path}/${id}/claim`))}>{t("Nhận xử lý báo cáo cây")}</Button>}
          {((engineer && ['IN_REVIEW', 'AWAITING_INFORMATION'].includes(row.status)) || (owner && row.status === 'AWAITING_INFORMATION')) && <form className="wf-form" onSubmit={e => { e.preventDefault(); const form = new FormData(e.currentTarget); action.run(() => api.post(`${path}/${id}/${form.get('action')}`, { expectedRevision: row.revision, content: form.get('content') })) }}>
            <Field title={t("Cập nhật xử lý")}><select name="action" required defaultValue=""><option value="" disabled>{owner ? 'Chọn thao tác' : 'Select action'}</option>{owner ? <option value="reply">{t("Bổ sung thông tin")}</option> : <><option value="recommend">{t("Tư vấn / đề xuất")}</option><option value="request-information">{t("Yêu cầu khách bổ sung")}</option><option value="resolve">{t("Đánh dấu đã giải quyết")}</option></>}</select></Field><Field title={t("Nội dung")}><textarea name="content" required maxLength={4000} rows={3} /></Field><Button type="submit" disabled={action.busy}>{t("Gửi cập nhật")}</Button>
          </form>}
          {action.errorView}<ol className="wf-timeline">{row.updates?.map(update => <li key={update.id}><strong>{update.action}</strong><p>{update.content}</p><small>{time(update.createdAt)}</small><EvidenceGallery images={update.images} /></li>)}</ol>
          <details><summary>{t("Dữ liệu cảm biến và mùa vụ liên quan")}</summary><State query={context}>{context.data?.readings.length ? <ul className="wf-list">{context.data.readings.slice(0, 20).map(reading => <li key={reading.id}><span>{reading.channelCode}</span><span>{reading.value} {reading.unit} · {time(reading.measuredAt)}{!reading.isValid && t(" · Không hợp lệ")}</span></li>)}</ul> : <p>{t("Không có dữ liệu cảm biến được đính kèm.")}</p>}<ul>{context.data?.seasons.map(season => <li key={season.id}>{season.name} · {season.crop}</li>)}</ul></State></details>
        </>}
      </>}</State></Panel></div>}
  </Workspace>
}
