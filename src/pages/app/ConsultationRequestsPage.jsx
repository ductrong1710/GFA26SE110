import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { period, time } from '../../services/api/workflow'
import { Workspace, State, Panel, Field, Pager, Button } from '../../components/workflow/WorkflowUi'
import PeriodFilter from '../../components/workflow/PeriodFilter'

export default function ConsultationRequestsPage() {
  const [range, setRange] = useState(() => period())
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const { activeRole } = useAuth()
  const query = useApiQuery(`leads:${JSON.stringify(range)}:${page}:${search}`, signal => api.get('/api/operations/consultation-requests', { ...range, page, pageSize: 20, search }, signal))
  return <Workspace title="Khách gửi yêu cầu tư vấn" description="Danh sách form từ Homepage. Người trực xem và liên hệ bên ngoài; không quản lý ca trực hoặc đánh dấu tiếp nhận.">
    <PeriodFilter value={range} onChange={value => { setRange(value); setPage(1) }} /><Field title="Tìm tên hoặc số điện thoại" type="search" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} />
    <State query={query}><Panel title={`${query.data?.total ?? 0} yêu cầu trong kỳ`}>
      <ul className="wf-list wf-leads">{query.data?.items.map(row => <li key={row.id}><div><h3>{row.customerName}</h3><a href={`tel:${row.phone}`} className="wf-link">{row.phone}</a><p>{row.serviceNeed}</p><p>{row.content}</p><small>{row.farmLocation} · {time(row.createdAt)}</small></div><Link className="app-ui-button" state={row} to={`/app/consultations/${row.id}`}>{activeRole === ROLES.AGRICULTURAL_ENGINEER ? 'Lập hồ sơ phương án' : 'Xem hồ sơ / liên kết khách'}</Link></li>)}</ul>
      {!query.data?.items.length && <p className="wf-empty">Chưa có yêu cầu phù hợp. Thử đổi khoảng ngày hoặc từ khóa.</p>}<Pager page={page} total={query.data?.total} onChange={setPage} />
      <Button onClick={query.reload}>Làm mới danh sách</Button>
    </Panel></State>
  </Workspace>
}
