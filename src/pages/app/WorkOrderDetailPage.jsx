import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { jobPath, time, label } from '../../services/api/workflow'
import { Workspace, State, Panel, Badge, Button, useAction } from '../../components/workflow/WorkflowUi'
import AssignmentForm from '../../components/workflow/AssignmentForm'
import AllocationForm from '../../components/workflow/AllocationForm'
import InstallationReportForm from '../../components/workflow/InstallationReportForm'
import ServiceReportForm from '../../components/workflow/ServiceReportForm'
import ServicePlanPanel from '../../components/workflow/ServicePlanPanel'
import JobReports from '../../components/workflow/JobReports'

export default function WorkOrderDetailPage() {
  const { kind, id } = useParams()
  const { activeRole } = useAuth()
  const valid = ['installation', 'service'].includes(kind)
  const path = jobPath(kind, id)
  const query = useApiQuery(`${path}:${activeRole}`, async signal => {
    const detail = await api.get(path, {}, signal)
    if (![ROLES.ADMINISTRATOR, ROLES.TECHNICIAN].includes(activeRole)) return detail
    const start = new Date(detail.scheduledAt)
    const calendar = await api.get('/api/operations/calendar', { from: new Date(start.getTime() - 3600000).toISOString(), to: new Date(start.getTime() + 86400000).toISOString() }, signal)
    const appointment = (kind === 'installation' ? calendar.installations : calendar.services).find(row => row.id === detail.id)
    return { ...detail, customer: appointment?.customer, phone: appointment?.phone, address: appointment?.address }
  }, valid)
  const job = query.data
  const admin = activeRole === ROLES.ADMINISTRATOR
  const technician = activeRole === ROLES.TECHNICIAN
  const canWork = ['IN_PROGRESS', 'REWORK'].includes(job?.status)
  const installed = useApiQuery(`installed:${kind}:${id}:${job?.revision}`, signal => api.get(`/api/operations/${kind}/${id}/installed-devices`, { page: 1, pageSize: 100 }, signal), valid && Boolean(job) && (admin || technician))
  const action = useAction(query.reload)
  if (!valid) return <Workspace title="Không tìm thấy loại công việc"><Link to="/app/work-orders">Về danh sách</Link></Workspace>
  return <Workspace title={`${kind === 'installation' ? 'Lắp đặt' : 'Bảo trì / sửa chữa'} #${id}`} description="Thực hiện và lưu bằng chứng theo từng bước; giữ đầy đủ lịch sử." actions={<Link to="/app/work-orders" className="app-ui-button">Về danh sách</Link>}>
    <State query={query}>{job && <>
      <Panel title={job.farmName ?? `Nông trại #${job.farmId}`}><div className="wf-toolbar"><Badge value={job.status} /><span>KTV #{job.technicianId}</span><span>{time(job.scheduledAt)}</span></div><p>{job.address ?? job.farmLocation ?? job.installationLocation}</p>{job.phone && <p>{job.customer} · <a className="wf-link" href={`tel:${job.phone}`}>{job.phone}</a></p>}<p>{job.description}</p>
        {technician && job.status === 'ASSIGNED' && <Button primary disabled={action.busy} onClick={() => action.run(() => api.post(`${path}/start`, { expectedRevision: job.revision }))}>Bắt đầu khảo sát / thực hiện</Button>}{action.errorView}
      </Panel>
      {admin && ['ASSIGNED', 'IN_PROGRESS', 'REWORK'].includes(job.status) && <Panel title="Đổi kỹ thuật viên / lịch hẹn" description="Dùng khi người phụ trách có việc bận đột xuất. Lịch sử cũ được giữ lại."><details><summary>Chỉnh phân công</summary><AssignmentForm path={`${path}/assignment`} revision={job.revision} kind={kind} jobId={job.id} type={job.type} onDone={query.reload} /></details></Panel>}
      {(admin || technician) && <Panel title="Thiết bị đã lắp tại nông trại"><State query={installed}><ul className="wf-list">{installed.data?.items?.map(device => <li key={device.id}><div><strong>{device.name} · {device.code}</strong><p>{device.installationLocation}</p>{device.history?.map((row, i) => <small key={i}>{time(row.createdAt)} · {row.cause} · {row.components} · KTV #{row.technicianId}</small>)}</div>{admin && <Link className="wf-link" to={`/app/device-history?code=${encodeURIComponent(device.code)}`}>Lịch sử →</Link>}</li>)}</ul>{!installed.data?.items?.length && <p className="wf-empty">Chưa có thiết bị đang thuê tại nông trại.</p>}</State></Panel>}
      {kind === 'installation' && admin && ['ASSIGNED', 'IN_PROGRESS', 'REWORK'].includes(job.status) && <Panel title="Thiết bị vật lý cấp cho lần lắp"><details><summary>Cấp phát / cập nhật thiết bị</summary><AllocationForm job={job} onDone={query.reload} /></details></Panel>}
      {kind === 'installation' && technician && canWork && <Panel title="Báo cáo lắp đặt & hợp đồng giấy"><InstallationReportForm job={job} onDone={query.reload} /></Panel>}
      {kind === 'service' && (admin || technician) && <ServicePlanPanel job={job} technician={technician} admin={admin} onDone={query.reload} />}
      {kind === 'service' && technician && canWork && <Panel title="Báo cáo hoàn thành"><ServiceReportForm job={job} onDone={query.reload} /></Panel>}
      <JobReports kind={kind} job={job} admin={admin} onDone={query.reload} />
      <Panel title="Lịch sử công việc"><ol className="wf-timeline">{job.history.map(row => <li key={row.id}><strong>{label(row.action)}</strong><p>{row.note}</p><small>{time(row.createdAt)} · Người thao tác #{row.userId} · KTV #{row.technicianId}</small></li>)}</ol></Panel>
    </>}</State>
  </Workspace>
}
