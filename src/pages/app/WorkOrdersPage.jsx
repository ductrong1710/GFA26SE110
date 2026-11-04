import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages } from "../../services/api/workflow"
import { Workspace, State, Badge, Field, JobLink, Panel } from '../../components/workflow/WorkflowUi'
import WorkOrderEmpty from '../../components/workflow/WorkOrderEmpty'

const columns = ['ASSIGNED', 'IN_PROGRESS', 'REPORT_SUBMITTED', 'OWNER_CONFIRMED', 'REWORK', 'APPROVED']
export default function WorkOrdersPage() {
  const t = useWorkflowText()
  const { label, time } = useWorkflowFormat()

  const { activeRole } = useAuth()
  const [kind, setKind] = useState('')
  const [status, setStatus] = useState('')
  const admin = activeRole === ROLES.ADMINISTRATOR
  const query = useApiQuery(`work-orders:${kind}:${activeRole}`, async signal => ({ jobs: await allPages(`/api/${kind}-jobs`, {}, signal),
    requests: admin && kind === 'installation' ? await allPages('/api/installation-requests', {}, signal) : [],
    dossiers: admin && kind === 'installation' ? await allPages('/api/installation-dossiers', {}, signal) : [],
  }), Boolean(kind))
  const jobs = (query.data?.jobs ?? []).filter(row => !status || row.status === status)
  const pendingRequests = (query.data?.requests ?? []).filter(row => ['SUBMITTED', 'APPROVED'].includes(row.status) && !query.data.jobs.some(job => job.installationRequestId === row.id))
  return <Workspace title={admin ? t("Lắp đặt & dịch vụ") : t("Công việc được giao")} description={t("Theo dõi tiến độ, mở hồ sơ và xử lý đúng bước.")}>
    <div className="wf-dashboard-toolbar">
    <div className="wf-toolbar"><Field title={t("Loại công việc")}><select value={kind} onChange={e => { setKind(e.target.value); setStatus('') }}><option value="" disabled>Select work order type</option><option value="installation">{t("Lắp đặt")}</option><option value="service">{t("Bảo trì / sửa chữa")}</option></select></Field><Field title={t("Trạng thái")}><select value={status} onChange={e => setStatus(e.target.value)}><option value="">{t("Tất cả")}</option>{[...columns, 'CLOSED'].map(value => <option key={value} value={value}>{label(value)}</option>)}</select></Field></div>
      <Link className="app-ui-button" to="/app/dispatch">{t("Xem lịch công việc")}</Link>
    </div>
    {!kind && <WorkOrderEmpty />}
    {kind && <State query={query}>{admin && kind === 'installation' && query.data?.dossiers.length > 0 && <Panel title={t("Hồ sơ kỹ sư gửi chờ duyệt")} description={t("Kiểm tra thông tin khách hàng, nông trại và bill cọc rồi duyệt để phân công KTV.")}>
      <ul className="wf-list">{query.data?.dossiers.map(row => <li key={row.id}><div><strong>{row.customerName} · {row.farmName}</strong><p>{row.farmLocation} · {row.phone}</p><small>{time(row.submittedAt)}</small></div><Link className="wf-link" to={`/app/consultations/${row.consultationId}`}>{t("Xem hồ sơ và bill →")}</Link></li>)}</ul>
      {!query.data?.dossiers.length && <p className="wf-empty">{t("Không có hồ sơ đang chờ duyệt.")}</p>}
    </Panel>}{admin && kind === 'installation' && pendingRequests.length > 0 && <Panel title={t("Yêu cầu lắp đặt cần xử lý")}>
      <ul className="wf-list">{pendingRequests.map(row => <li key={row.id}><div><Badge value={row.status} /><p>{t("Nông trại #")}{row.farmId} · {time(row.submittedAt)}</p></div><Link className="wf-link" to={`/app/installation-requests/${row.id}`}>{t("Yêu cầu #")}{row.id} →</Link></li>)}</ul>
      {!pendingRequests.length && <p className="wf-empty">{t("Không có yêu cầu lắp đặt đang chờ xử lý.")}</p>}
    </Panel>}
      {!jobs.length ? <WorkOrderEmpty selected filtered={Boolean(status)} /> : <div className="wf-kanban">{(kind === 'installation' ? columns : [...columns.slice(0, 5), 'CLOSED']).map(column => <section key={column} className="wf-lane"><h2>{label(column)} <span>{jobs.filter(row => row.status === column).length}</span></h2>
        {jobs.filter(row => row.status === column).map(row => <article key={row.id}><JobLink kind={kind} id={row.id}>{kind === 'service' ? `${label(row.type)} #${row.id}` : (t("Lắp đặt #") + (row.id))}</JobLink><p>{t("Nông trại #")}{row.farmId}</p><small>{time(row.scheduledAt)}</small><p>{t("KTV #")}{row.technicianId}</p></article>)}
        {!jobs.some(row => row.status === column) && <p className="wf-muted">{t("Không có công việc")}</p>}
      </section>)}</div>}
    </State>}
  </Workspace>
}
