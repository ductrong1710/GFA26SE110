import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useLocation, useParams } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages } from "../../services/api/workflow"
import { Workspace, Panel, State, EvidenceGallery } from '../../components/workflow/WorkflowUi'
import { ProposalForm, SurveyForm } from '../../components/workflow/ConsultationForms'
import InstallationDossierPanel from '../../components/workflow/InstallationDossierPanel'

export default function ConsultationDetailPage() {
  const t = useWorkflowText()
  const { money, time } = useWorkflowFormat()

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
  const editable = engineer && !['WAITING_ADMIN', 'SUBMITTED'].includes(proposal?.status)
  return <Workspace title={(t("Hồ sơ tư vấn #") + (id))} description={t("Ghi nhận phương án sau khi đã trao đổi với khách bên ngoài.")}><State query={query}>{data && <>
    <Panel title={data.lead.customerName}><p><a className="wf-link" href={`tel:${data.lead.phone}`}>{data.lead.phone}</a> · {data.lead.farmLocation}</p><p>{data.lead.serviceNeed} · {data.lead.content}</p><p className="wf-muted">{t("Nông trại trong hồ sơ: ")}{data.lead.farmId ? `#${data.lead.farmId}` : t("Sẽ được lưu khi kỹ sư gửi hồ sơ khách hàng và bill.")}</p></Panel>
    {data.survey && <Panel title={(t("Thông tin nông trại: ") + (data.survey.farmName))}><p>{data.survey.farmLocation} · {data.survey.farmArea == null ? t("Chưa ghi diện tích") : `${data.survey.farmArea} ha`}</p><p>{data.survey.monitoringNeeds}</p><p>{data.survey.notes}</p><ul>{data.survey.zones.map(zone => <li key={zone.id}>{zone.name} · {zone.crop || t("Chưa ghi loại cây")}</li>)}</ul><EvidenceGallery images={data.survey.images} title={t("Ảnh nông trại")} /></Panel>}
    {editable && <SurveyForm id={id} revision={data.surveys[0]?.revision ?? 0} onDone={query.reload} />}
    {editable && data.survey && <ProposalForm key={data.survey.id} id={id} survey={data.survey} revision={data.proposals[0]?.revision ?? 0} onDone={query.reload} />}
    {proposal && <Panel title={(t("Phương án mới nhất · ") + (proposal.rentalMonths) + t(" tháng"))} description={(t("Ngày khách mong muốn lắp: ") + (time(proposal.requestedInstallationAt)))}>
      <ul className="wf-list">{proposal.lines.map(line => <li key={line.id}><span>{line.productName ?? line.deviceType}{" × "}{line.quantity}</span><strong>{money(line.monthlyRentVnd)}{t("/tháng")}</strong></li>)}</ul>
    </Panel>}
    {proposal && <InstallationDossierPanel key={proposal.id} proposal={proposal} lead={data.lead} engineer={engineer} admin={activeRole === ROLES.ADMINISTRATOR} farmLinked={Boolean(data.lead.farmId)} onDone={query.reload} />}
  </>}</State></Workspace>
}
