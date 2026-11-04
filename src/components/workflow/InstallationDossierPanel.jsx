import { useWorkflowText } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../services/api'
import { Panel, Field, Button, Badge, EvidenceGallery, NoteAction, useAction } from './WorkflowUi'
import EvidenceUpload from './EvidenceUpload'

export default function InstallationDossierPanel({ proposal, lead, engineer, admin, farmLinked, onDone }) {
  const t = useWorkflowText()

  const [bills, setBills] = useState([])
  const [note, setNote] = useState('')
  const navigate = useNavigate()
  const action = useAction(onDone)
  const path = `/api/installation-dossiers/${proposal.id}`
  const review = { expectedRevision: proposal.revision }
  const draft = ['DRAFT', 'PLAN_CONFIRMED', 'DEPOSIT_CONFIRMED'].includes(proposal.status)
  return <Panel title={t("Hồ sơ đã chốt bên ngoài")} description={t("Tư vấn, thống nhất thiết bị và nhận cọc diễn ra bên ngoài. Hệ thống chỉ ghi nhận kết quả và bill để Admin kiểm tra.")}>
    <Badge value={proposal.status === 'DRAFT' ? t("Hồ sơ chưa gửi") : proposal.status === 'SUBMITTED' ? t("Đã chuyển sang lắp đặt") : proposal.status} />
    <EvidenceGallery images={proposal.depositBills} title={t("Bill cọc do kỹ sư gửi")} />
    {proposal.depositConfirmationNote && <p>{t("Ghi nhận bên ngoài: ")}{proposal.depositConfirmationNote}</p>}
    {proposal.dossierReviewNote && <p className="wf-callout">{t("Admin phản hồi: ")}{proposal.dossierReviewNote}</p>}
    {engineer && proposal.status === 'RETURNED' && <p>{t("Chỉnh thông tin nông trại nếu cần và lưu phiên bản thiết bị mới phía trên, sau đó gửi lại hồ sơ kèm bill. Phiên bản cũ được giữ để đối chiếu.")}</p>}
    {engineer && draft && <form className="wf-form" onSubmit={e => {
      e.preventDefault()
      const values = new FormData(e.currentTarget)
      action.run(() => api.post(`${path}/submit`, { expectedRevision: proposal.revision, note, bills,
        customer: farmLinked ? null : { fullName: values.get('customerName'), email: values.get('customerEmail') } }))
    }}>
      {!farmLinked && <><Field title={t("Họ tên khách hàng đã chốt")} name="customerName" required maxLength={150} defaultValue={lead?.customerName ?? ''} /><Field title={t("Email khách hàng")} name="customerEmail" type="email" required maxLength={255} defaultValue={lead?.email ?? ''} /></>}
      <Field title={t("Thông tin đã chốt / ghi chú cọc bên ngoài")} required maxLength={2000} value={note} onChange={e => setNote(e.target.value)} />
      <EvidenceUpload title={t("Bill cọc bên ngoài")} required value={bills} onChange={setBills} />
      <p className="wf-muted">{t("Khi gửi, hệ thống lưu khách hàng và tạo nông trại từ thông tin đã nhập. Admin nhận hồ sơ đầy đủ để duyệt và phân công KTV.")}</p>
      {action.errorView}<Button primary type="submit" disabled={action.busy || !bills.length}>{t("Gửi hồ sơ và bill cho Admin")}</Button>
    </form>}
    {proposal.status === 'WAITING_ADMIN' && <p className="wf-callout">{t("Đã gửi hồ sơ, đang chờ Admin kiểm tra thông tin nông trại, thiết bị và bill cọc.")}</p>}
    {admin && proposal.status === 'WAITING_ADMIN' && <>
      {!farmLinked && <p className="wf-callout">{t("Hồ sơ thiếu thông tin khách/vườn. Trả kỹ sư bổ sung trước khi duyệt.")}</p>}
      <div className="wf-two-col">
        {farmLinked && <form className="wf-inline-form" onSubmit={e => {
          e.preventDefault(); const values = new FormData(e.currentTarget)
          action.run(async () => { const result = await api.post(`${path}/review`, { ...review, approve: true, note: values.get('reviewNote') }); navigate(`/app/installation-requests/${result.installationRequestId}`) })
        }}><Field title={t("Kết quả đối chiếu hồ sơ và bill")} name="reviewNote" required maxLength={2000} />{action.errorView}<Button primary type="submit" disabled={action.busy}>{t("Duyệt hồ sơ để phân công KTV")}</Button></form>}
        <NoteAction path={`${path}/review`} body={{ ...review, approve: false }} onDone={onDone} danger>{t("Trả hồ sơ bổ sung")}</NoteAction>
      </div>
    </>}
    {proposal.status === 'SUBMITTED' && <p>{t("Hồ sơ đã chuyển sang yêu cầu lắp đặt. Xem trong mục Lắp đặt & dịch vụ.")}</p>}
  </Panel>
}
