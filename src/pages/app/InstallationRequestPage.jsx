import { useParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { money, time } from '../../services/api/workflow'
import { Workspace, Panel, State, Badge, NoteAction } from '../../components/workflow/WorkflowUi'
import AssignmentForm from '../../components/workflow/AssignmentForm'

export default function InstallationRequestPage() {
  const { id } = useParams()
  const path = `/api/installation-requests/${id}`
  const query = useApiQuery(path, signal => api.get(path, {}, signal))
  const row = query.data
  return <Workspace title={`Yêu cầu lắp đặt #${id}`} description="Kiểm tra phương án, xác nhận cọc và phân công người thực hiện."><State query={query}>{row && <>
    <Panel title="Phương án khách đã xác nhận"><Badge value={row.status} /><dl className="wf-facts"><div><dt>Nông trại / Chủ vườn</dt><dd>#{row.farmId} / #{row.ownerId}</dd></div><div><dt>Ngày mong muốn lắp</dt><dd>{time(row.requestedInstallationAt)}</dd></div><div><dt>Thời hạn thuê</dt><dd>{row.rentalMonths} tháng</dd></div><div><dt>Thỏa thuận</dt><dd>{row.planConfirmationNote}</dd></div><div><dt>Xác nhận cọc</dt><dd>{row.depositConfirmationNote}</dd></div></dl>
      <ul className="wf-list">{row.lines.map(line => <li key={line.id}><span>{line.productName ?? line.deviceType} × {line.quantity}</span><span>{money(line.monthlyRentVnd)}/tháng</span></li>)}</ul>
    </Panel>
    {row.status === 'SUBMITTED' && <Panel title="Kiểm tra yêu cầu"><div className="wf-two-col"><NoteAction path={`${path}/approve`} onDone={query.reload}>Duyệt yêu cầu</NoteAction><NoteAction path={`${path}/return`} onDone={query.reload} danger>Yêu cầu bổ sung</NoteAction></div></Panel>}
    {row.status === 'APPROVED' && <Panel title="Phân công lắp đặt" description="Nếu công việc đã được tạo, hãy mở công việc để đổi KTV; không tạo lại."><AssignmentForm path={`${path}/jobs`} onDone={query.reload} /></Panel>}
    {row.reviewNote && <Panel title="Ghi chú kiểm tra"><p>{row.reviewNote}</p></Panel>}
  </>}</State></Workspace>
}
