import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import ManagementDialog from '../../components/app/ManagementDialog'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { Workspace, State, Panel, Field, Pager, Button } from '../../components/workflow/WorkflowUi'
import PeriodFilter from '../../components/workflow/PeriodFilter'
import '../../styles/maintenance.css'

export default function ConsultationRequestsPage() {
  const t = useWorkflowText()
  const { time } = useWorkflowFormat()

  const [range, setRange] = useState({ from: '', to: '' })
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState(null)
  const partialRange = Boolean(range.from) !== Boolean(range.to)
  const query = useApiQuery(`leads:${JSON.stringify(range)}:${page}:${search}`, signal => api.get('/api/operations/consultation-requests', { ...(range.from && range.to ? range : {}), page, pageSize: 10, search }, signal), !partialRange, 15000)
  return <Workspace title={t("Khách gửi yêu cầu tư vấn")} description={t("Danh sách form từ Homepage. Người trực xem và liên hệ bên ngoài; không quản lý ca trực hoặc đánh dấu tiếp nhận.")}>
    <PeriodFilter value={range} onChange={value => { setRange(value); setPage(1) }} /><Field title={t("Tìm tên hoặc số điện thoại")} type="search" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} />
    {!range.from && !range.to && <p className="wf-muted">All consultation requests</p>}
    {partialRange ? <p className="wf-empty">Select both From date and To date to filter requests.</p> : <State query={query}><Panel title={((query.data?.total ?? 0) + t(" yêu cầu trong kỳ"))}>
      <div className="mt-table-scroll"><table className="mt-table"><thead><tr><th>Customer</th><th>Phone</th><th>Service need</th><th>Submitted</th><th>Action</th></tr></thead><tbody>{query.data?.items.map(row => <tr key={row.id}><td><strong>{row.customerName}</strong><small>{row.farmLocation}</small></td><td><a href={`tel:${row.phone}`}>{row.phone}</a></td><td>{row.serviceNeed}</td><td>{time(row.createdAt)}</td><td><button type="button" className="app-ui-button" onClick={() => setSelected(row)}>View details</button></td></tr>)}</tbody></table></div>
      {!query.data?.items.length && <p className="wf-empty">{t("Chưa có yêu cầu phù hợp. Thử đổi khoảng ngày hoặc từ khóa.")}</p>}<Pager page={page} total={query.data?.total} pageSize={10} onChange={setPage} />
      <Button onClick={query.reload}>{t("Làm mới danh sách")}</Button>
    </Panel></State>}
    {selected && <ManagementDialog drawer title={`Consultation request #${selected.id}`} onClose={() => setSelected(null)}>
      <div className="management-drawer-body">
        <dl className="management-detail-grid">
          <div><dt>Customer</dt><dd>{selected.customerName || '—'}</dd></div>
          <div><dt>Phone</dt><dd>{selected.phone ? <a className="wf-link" href={`tel:${selected.phone}`}>{selected.phone}</a> : '—'}</dd></div>
          <div><dt>Email</dt><dd>{selected.email ? <a className="wf-link" href={`mailto:${selected.email}`}>{selected.email}</a> : '—'}</dd></div>
          <div><dt>Farm location</dt><dd>{selected.farmLocation || '—'}</dd></div>
          <div><dt>Service need</dt><dd>{selected.serviceNeed || '—'}</dd></div>
          <div><dt>Submitted</dt><dd>{time(selected.createdAt)}</dd></div>
        </dl>
        <section><h3>Request content</h3><p className="consultation-request-content">{selected.content || '—'}</p></section>
      </div>
    </ManagementDialog>}
  </Workspace>
}
