import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { time } from '../../services/api/workflow'
import { Workspace, Panel, Field, Button, State, Pager, JobLink } from '../../components/workflow/WorkflowUi'

export default function DeviceHistoryPage() {
  const [params, setParams] = useSearchParams()
  const [input, setInput] = useState(params.get('code') ?? '')
  const [page, setPage] = useState(1)
  const code = params.get('code') ?? ''
  const query = useApiQuery(`device-history:${code}:${page}`, signal => api.get('/api/operations/device-history', { code, page, pageSize: 20 }, signal), Boolean(code))
  const data = query.data
  return <Workspace title="Lịch sử thiết bị đã xử lý" description="Tra mã thiết bị để xem lắp đặt, lỗi phát hiện, linh kiện thay thế và kỹ thuật viên thực hiện.">
    <form className="wf-toolbar" onSubmit={e => { e.preventDefault(); setPage(1); setParams({ code: input.trim() }) }}><Field title="Mã thiết bị" required maxLength={200} value={input} onChange={e => setInput(e.target.value)} /><Button type="submit" primary>Tra cứu lịch sử</Button></form>
    {!code && <Panel title="Bắt đầu tra cứu"><p className="wf-empty">Nhập mã thiết bị hoặc mở từ chi tiết công việc.</p></Panel>}
    <State query={query}>{data && <>
      <Panel title={`Thiết bị ${code}`}><ul className="wf-list">{data.assignments.map(row => <li key={row.id}><div><strong>{row.name}</strong><p>Nông trại #{row.farmId} · Hồ sơ #{row.contractId}</p><small>{row.installationLocation}</small></div></li>)}</ul>{!data.total && <p>Chưa có lịch sử thuê cho mã thiết bị này.</p>}<Pager page={page} total={data.total} onChange={setPage} /></Panel>
      <div className="wf-two-col"><Panel title="Lắp đặt ban đầu"><ol className="wf-timeline">{data.installations.map((row, i) => <li key={i}><JobLink kind="installation" id={row.jobId} /><p>{row.note}</p><small>KTV #{row.technicianId} · {time(row.createdAt)}</small></li>)}</ol></Panel>
        <Panel title="Thay toàn bộ thiết bị"><ol className="wf-timeline">{data.replacements.map(row => <li key={row.id}><strong>{row.oldDeviceCode} → {row.newDeviceCode}</strong><p>{row.note}</p><small>KTV #{row.technicianId} · {time(row.createdAt)}</small></li>)}</ol>{!data.replacements.length && <p className="wf-empty">Chưa ghi nhận thay thiết bị.</p>}</Panel></div>
      <Panel title="Báo cáo bảo trì / sửa chữa"><div className="wf-report-grid">{data.reports.map(row => <article key={row.id}><JobLink kind="service" id={row.jobId} /><h3>{row.cause}</h3><dl><dt>Linh kiện</dt><dd>{row.components}</dd><dt>Kết quả kiểm tra</dt><dd>{row.testResults}</dd><dt>Xác nhận khách trên hệ thống</dt><dd>{time(row.confirmedAt)}</dd></dl><p>{row.approved ? 'Admin đã duyệt' : 'Chưa duyệt hoàn tất'} · KTV #{row.technicianId}</p><small>{time(row.createdAt)}</small></article>)}</div>{!data.reports.length && <p className="wf-empty">Chưa có báo cáo xử lý.</p>}</Panel>
    </>}</State>
  </Workspace>
}
