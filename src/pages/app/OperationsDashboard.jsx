import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { period, time } from '../../services/api/workflow'
import { Workspace, Panel, State, JobLink } from '../../components/workflow/WorkflowUi'
import PeriodFilter from '../../components/workflow/PeriodFilter'
import OperationsCharts from '../../components/workflow/OperationsCharts'

export default function OperationsDashboard() {
  const [range, setRange] = useState(() => period())
  const query = useApiQuery(`operations-dashboard:${JSON.stringify(range)}`, async signal => {
    const [operations, overview] = await Promise.all([api.get('/api/operations/dashboard', range, signal), api.get('/api/dashboard/overview', {}, signal)])
    return { ...operations, overview }
  })
  const data = query.data
  const cards = data && [['Nông trại', data.overview.farms], ['Thiết bị đang theo dõi', data.overview.sensorNodes.total + data.overview.gateways.total],
    ['Cảnh báo mở', data.overview.alerts.open], ['Công việc đang mở', data.overview.operations.openInstallations + data.overview.operations.openMaintenance + data.overview.operations.openRepairs],
    ['Hợp đồng sắp hết hạn', data.overview.operations.expiringRentals], ['Form tư vấn trong kỳ', data.consultationRequests]]
  return <Workspace title="Điều hành nông trại" description="Theo dõi tiến độ, lịch kỹ thuật viên và hồ sơ cần xử lý." actions={<Link className="app-ui-button app-ui-button--primary" to="/app/dispatch">Mở lịch điều phối</Link>}>
    <PeriodFilter value={range} onChange={setRange} /><State query={query}>{data && <>
      <section className="wf-metrics" aria-label="Tổng quan vận hành">{cards.map(([name, value]) => <div key={name}><span>{name}</span><strong>{value ?? '—'}</strong></div>)}</section>
      <OperationsCharts data={data} /><p className="wf-muted">Biểu đồ tiến độ và UAV nhóm theo ngày tạo, thể hiện trạng thái hiện tại. Không phải lịch sử chuyển trạng thái.</p>
      <div className="wf-two-col"><Panel title="Yêu cầu tư vấn mới" description="Thông tin khách gửi form Homepage; người trực liên hệ bên ngoài." actions={<Link to="/app/consultations" className="wf-link">Xem tất cả →</Link>}>
        {data.recentConsultationRequests.length ? <ul className="wf-list">{data.recentConsultationRequests.map(row => <li key={row.id}><div><strong>{row.customerName}</strong><p>{row.serviceNeed}</p><small>{time(row.createdAt)}</small></div><a className="wf-link" href={`tel:${row.phone}`}>{row.phone}</a></li>)}</ul> : <p className="wf-empty">Chưa có khách gửi form trong kỳ.</p>}
      </Panel><Panel title="Cần phân công lắp đặt" actions={<Link to="/app/work-orders" className="wf-link">Xem công việc →</Link>}>
        {data.installationPipeline.length ? <ul className="wf-list">{data.installationPipeline.map(row => <li key={row.id}><span>Yêu cầu #{row.id} · Nông trại #{row.farmId}</span><Link className="wf-link" to={`/app/installation-requests/${row.id}`}>Phân công →</Link></li>)}</ul> : <p className="wf-empty">Không có yêu cầu chờ phân công.</p>}
      </Panel></div>
      <div className="wf-two-col"><Panel title="Hợp đồng kết thúc trong kỳ" actions={<Link className="wf-link" to="/app/rentals">Hồ sơ thuê →</Link>}>
        <ul className="wf-list">{data.rentalsEndingInPeriod.map(row => <li key={row.id}><Link to={`/app/rentals?id=${row.id}`} className="wf-link">Hồ sơ #{row.id}</Link><span>{time(row.endAt)}</span></li>)}</ul>{!data.rentalsEndingInPeriod.length && <p className="wf-empty">Không có hợp đồng đến hạn trong kỳ.</p>}
      </Panel><Panel title="Lịch bảo trì trong kỳ" actions={<Link className="wf-link" to="/app/maintenance">Cấu hình lịch →</Link>}>
        <ul className="wf-list">{data.maintenance.slice(0, 5).map(row => <li key={row.id}><JobLink kind="service" id={row.id} /><span>{row.overdue ? 'Quá giờ dự kiến · ' : ''}{time(row.scheduledAt)}</span></li>)}</ul>{!data.maintenance.length && <p className="wf-empty">Chưa có công việc bảo trì trong kỳ.</p>}
      </Panel></div>
    </>}</State>
  </Workspace>
}
