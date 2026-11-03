import { useLocation, useParams } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, money, time } from '../../services/api/workflow'
import { Workspace, Panel, State, NoteAction } from '../../components/workflow/WorkflowUi'
import { CustomerBinding, ProposalForm, SurveyForm } from '../../components/workflow/ConsultationForms'

export default function ConsultationDetailPage() {
  const { id } = useParams()
  const { state } = useLocation()
  const { activeRole } = useAuth()
  const engineer = activeRole === ROLES.AGRICULTURAL_ENGINEER
  const query = useApiQuery(`consultation-detail:${id}:${activeRole}`, async signal => {
    let lead
    try { lead = await api.get(`/api/consultations/${id}`, {}, signal) } catch (e) { if (e.status !== 403 || !state || !engineer) throw e; return { lead: state, surveys: [], proposals: [] } }
    const surveys = await allPages(`/api/consultations/${id}/surveys`, {}, signal)
    const proposals = await allPages(`/api/consultations/${id}/proposals`, {}, signal)
    const survey = surveys[0] ? await api.get(`/api/consultations/${id}/surveys/${surveys[0].id}`, {}, signal) : null
    const proposal = proposals[0] ? await api.get(`/api/consultations/${id}/proposals/${proposals[0].id}`, {}, signal) : null
    return { lead, surveys, proposals, survey, proposal }
  })
  const data = query.data
  const proposal = data?.proposal
  return <Workspace title={`Hồ sơ tư vấn #${id}`} description="Ghi nhận phương án sau khi đã trao đổi với khách bên ngoài."><State query={query}>{data && <>
    <Panel title={data.lead.customerName}><p><a className="wf-link" href={`tel:${data.lead.phone}`}>{data.lead.phone}</a> · {data.lead.farmLocation}</p><p>{data.lead.serviceNeed} · {data.lead.content}</p><p className="wf-muted">Nông trại liên kết: {data.lead.farmId ? `#${data.lead.farmId}` : 'Chưa liên kết — cần Admin thực hiện trước khi gửi lắp đặt.'}</p></Panel>
    {activeRole === ROLES.ADMINISTRATOR && !data.lead.farmId && data.lead.engineerId && <CustomerBinding id={id} onDone={query.reload} />}
    {engineer && <SurveyForm id={id} revision={data.surveys[0]?.revision ?? 0} onDone={query.reload} />}
    {engineer && data.survey && <ProposalForm key={data.survey.id} id={id} survey={data.survey} revision={data.proposals[0]?.revision ?? 0} onDone={query.reload} />}
    {proposal && <Panel title={`Phương án mới nhất · ${proposal.rentalMonths} tháng`} description={`Ngày khách mong muốn lắp: ${time(proposal.requestedInstallationAt)}`}>
      <ul className="wf-list">{proposal.lines.map(line => <li key={line.id}><span>{line.productName ?? line.deviceType} × {line.quantity}</span><strong>{money(line.monthlyRentVnd)}/tháng</strong></li>)}</ul>
      {engineer && proposal.status === 'DRAFT' && <NoteAction path={`/api/consultations/${id}/proposals/${proposal.id}/confirm-plan`} onDone={query.reload}>Ghi nhận khách đồng ý phương án</NoteAction>}
      {engineer && proposal.status === 'PLAN_CONFIRMED' && <NoteAction path={`/api/consultations/${id}/proposals/${proposal.id}/confirm-deposit`} onDone={query.reload}>Ghi nhận cọc bên ngoài</NoteAction>}
      {engineer && proposal.status === 'DEPOSIT_CONFIRMED' && <NoteAction path={`/api/consultations/${id}/proposals/${proposal.id}/submit-installation`} onDone={query.reload}>Gửi yêu cầu lắp đặt đến Admin</NoteAction>}
      {proposal.status === 'SUBMITTED' && <p className="wf-callout">Đã gửi yêu cầu lắp đặt. Admin xử lý trong mục Lắp đặt & dịch vụ.</p>}
    </Panel>}
  </>}</State></Workspace>
}
