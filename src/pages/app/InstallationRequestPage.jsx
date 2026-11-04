import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'

import { Workspace, Panel, State, Badge, NoteAction, EvidenceGallery } from '../../components/workflow/WorkflowUi'
import AssignmentForm from '../../components/workflow/AssignmentForm'

export default function InstallationRequestPage() {
  const t = useWorkflowText()
  const { money, time } = useWorkflowFormat()

  const { id } = useParams()
  const path = `/api/installation-requests/${id}`
  const query = useApiQuery(path, signal => api.get(path, {}, signal))
  const row = query.data
  return <Workspace title={(t("Yêu cầu lắp đặt #") + (id))} description={t("Kiểm tra phương án, xác nhận cọc và phân công người thực hiện.")}><State query={query}>{row && <>
    <Panel title={t("Phương án khách đã xác nhận")}><Badge value={row.status} /><dl className="wf-facts"><div><dt>{t("Nông trại / Chủ vườn")}</dt><dd>#{row.farmId} / #{row.ownerId}</dd></div><div><dt>{t("Ngày mong muốn lắp")}</dt><dd>{time(row.requestedInstallationAt)}</dd></div><div><dt>{t("Thời hạn thuê")}</dt><dd>{row.rentalMonths}{t(" tháng")}</dd></div><div><dt>{t("Thỏa thuận")}</dt><dd>{row.planConfirmationNote}</dd></div><div><dt>{t("Xác nhận cọc")}</dt><dd>{row.depositConfirmationNote}</dd></div></dl>
      <ul className="wf-list">{row.lines.map(line => <li key={line.id}><span>{line.productName ?? line.deviceType}{" × "}{line.quantity}</span><span>{money(line.monthlyRentVnd)}{t("/tháng")}</span></li>)}</ul>
      <EvidenceGallery images={row.depositBills} title={t("Bill cọc bên ngoài")} />
    </Panel>
    {row.status === 'SUBMITTED' && <Panel title={t("Kiểm tra yêu cầu")}><div className="wf-two-col"><NoteAction path={`${path}/approve`} onDone={query.reload}>{t("Duyệt yêu cầu")}</NoteAction><NoteAction path={`${path}/return`} onDone={query.reload} danger>{t("Yêu cầu bổ sung")}</NoteAction></div></Panel>}
    {row.status === 'APPROVED' && <Panel title={t("Phân công lắp đặt")} description={t("Nếu công việc đã được tạo, hãy mở công việc để đổi KTV; không tạo lại.")}><AssignmentForm path={`${path}/jobs`} onDone={query.reload} /></Panel>}
    {row.reviewNote && <Panel title={t("Ghi chú kiểm tra")}><p>{row.reviewNote}</p></Panel>}
  </>}</State></Workspace>
}
