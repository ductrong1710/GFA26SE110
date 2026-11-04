import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'

import { Workspace, Panel, Field, Button, State, Pager, JobLink } from '../../components/workflow/WorkflowUi'

export default function DeviceHistoryPage() {
  const t = useWorkflowText()
  const { time } = useWorkflowFormat()

  const [params, setParams] = useSearchParams()
  const [input, setInput] = useState(params.get('code') ?? '')
  const [page, setPage] = useState(1)
  const code = params.get('code') ?? ''
  const query = useApiQuery(`device-history:${code}:${page}`, signal => api.get('/api/operations/device-history', { code, page, pageSize: 20 }, signal), Boolean(code))
  const data = query.data
  return <Workspace title={t("Lịch sử thiết bị đã xử lý")} description={t("Tra mã thiết bị để xem lắp đặt, lỗi phát hiện, linh kiện thay thế và kỹ thuật viên thực hiện.")}>
    <form className="wf-toolbar" onSubmit={e => { e.preventDefault(); setPage(1); setParams({ code: input.trim() }) }}><Field title={t("Mã thiết bị")} required maxLength={200} value={input} onChange={e => setInput(e.target.value)} /><Button type="submit" primary>{t("Tra cứu lịch sử")}</Button></form>
    {!code && <Panel title={t("Bắt đầu tra cứu")}><p className="wf-empty">{t("Nhập mã thiết bị hoặc mở từ chi tiết công việc.")}</p></Panel>}
    <State query={query}>{data && <>
      <Panel title={(t("Thiết bị ") + (code))}><ul className="wf-list">{data.assignments.map(row => <li key={row.id}><div><strong>{row.name}</strong><p>{t("Nông trại #")}{row.farmId}{t(" · Hồ sơ #")}{row.contractId}</p><small>{row.installationLocation}</small></div></li>)}</ul>{!data.total && <p>{t("Chưa có lịch sử thuê cho mã thiết bị này.")}</p>}<Pager page={page} total={data.total} onChange={setPage} /></Panel>
      <div className="wf-two-col"><Panel title={t("Lắp đặt ban đầu")}><ol className="wf-timeline">{data.installations.map((row, i) => <li key={i}><JobLink kind="installation" id={row.jobId} /><p>{row.note}</p><small>{t("KTV #")}{row.technicianId} · {time(row.createdAt)}</small></li>)}</ol></Panel>
        <Panel title={t("Thay toàn bộ thiết bị")}><ol className="wf-timeline">{data.replacements.map(row => <li key={row.id}><strong>{row.oldDeviceCode} → {row.newDeviceCode}</strong><p>{row.note}</p><small>{t("KTV #")}{row.technicianId} · {time(row.createdAt)}</small></li>)}</ol>{!data.replacements.length && <p className="wf-empty">{t("Chưa ghi nhận thay thiết bị.")}</p>}</Panel></div>
      <Panel title={t("Báo cáo bảo trì / sửa chữa")}><div className="wf-report-grid">{data.reports.map(row => <article key={row.id}><JobLink kind="service" id={row.jobId} /><h3>{row.cause}</h3><dl><dt>{t("Linh kiện")}</dt><dd>{row.components}</dd><dt>{t("Kết quả kiểm tra")}</dt><dd>{row.testResults}</dd><dt>{t("Xác nhận khách trên hệ thống")}</dt><dd>{time(row.confirmedAt)}</dd></dl><p>{row.approved ? t("Admin đã duyệt") : t("Chưa duyệt hoàn tất")}{t(" · KTV #")}{row.technicianId}</p><small>{time(row.createdAt)}</small></article>)}</div>{!data.reports.length && <p className="wf-empty">{t("Chưa có báo cáo xử lý.")}</p>}</Panel>
    </>}</State>
  </Workspace>
}
