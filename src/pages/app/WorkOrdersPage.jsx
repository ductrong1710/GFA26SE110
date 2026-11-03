import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, label, time } from '../../services/api/workflow'
import { Workspace, State, Badge, Field, JobLink, Panel } from '../../components/workflow/WorkflowUi'

const columns = ['ASSIGNED', 'IN_PROGRESS', 'REPORT_SUBMITTED', 'OWNER_CONFIRMED', 'REWORK', 'APPROVED']
export default function WorkOrdersPage() {
  const { activeRole } = useAuth()
  const [kind, setKind] = useState('installation')
  const [status, setStatus] = useState('')
  const admin = activeRole === ROLES.ADMINISTRATOR
  const query = useApiQuery(`work-orders:${kind}:${activeRole}`, async signal => ({ jobs: await allPages(`/api/${kind}-jobs`, {}, signal),
    requests: admin && kind === 'installation' ? await allPages('/api/installation-requests', {}, signal) : [],
  }))
  const jobs = (query.data?.jobs ?? []).filter(row => !status || row.status === status)
  const pendingRequests = (query.data?.requests ?? []).filter(row => ['SUBMITTED', 'APPROVED'].includes(row.status) && !query.data.jobs.some(job => job.installationRequestId === row.id))
  return <Workspace title={admin ? 'Lắp đặt & dịch vụ' : 'Công việc được giao'} description="Theo dõi tiến độ, mở hồ sơ và xử lý đúng bước." actions={<Link className="app-ui-button" to="/app/dispatch">Xem lịch công việc</Link>}>
    <div className="wf-toolbar"><Field title="Loại công việc"><select value={kind} onChange={e => { setKind(e.target.value); setStatus('') }}><option value="installation">Lắp đặt</option><option value="service">Bảo trì / sửa chữa</option></select></Field><Field title="Trạng thái"><select value={status} onChange={e => setStatus(e.target.value)}><option value="">Tất cả</option>{[...columns, 'CLOSED'].map(value => <option key={value} value={value}>{label(value)}</option>)}</select></Field></div>
    <State query={query}>{admin && kind === 'installation' && <Panel title="Yêu cầu lắp đặt cần xử lý">
      <ul className="wf-list">{pendingRequests.map(row => <li key={row.id}><div><Badge value={row.status} /><p>Nông trại #{row.farmId} · {time(row.submittedAt)}</p></div><Link className="wf-link" to={`/app/installation-requests/${row.id}`}>Yêu cầu #{row.id} →</Link></li>)}</ul>
      {!pendingRequests.length && <p className="wf-empty">Không có yêu cầu lắp đặt đang chờ xử lý.</p>}
    </Panel>}
      <div className="wf-kanban">{(kind === 'installation' ? columns : [...columns.slice(0, 5), 'CLOSED']).map(column => <section key={column} className="wf-lane"><h2>{label(column)} <span>{jobs.filter(row => row.status === column).length}</span></h2>
        {jobs.filter(row => row.status === column).map(row => <article key={row.id}><JobLink kind={kind} id={row.id}>{kind === 'service' ? `${label(row.type)} #${row.id}` : `Lắp đặt #${row.id}`}</JobLink><p>Nông trại #{row.farmId}</p><small>{time(row.scheduledAt)}</small><p>KTV #{row.technicianId}</p></article>)}
        {!jobs.some(row => row.status === column) && <p className="wf-muted">Không có công việc</p>}
      </section>)}</div>
    </State>
  </Workspace>
}
