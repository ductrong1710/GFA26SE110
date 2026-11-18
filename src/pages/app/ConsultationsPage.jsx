import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from '../../components/app/ApiState'
import Icon from '../../components/Icon'
import ManagementDialog from '../../components/app/ManagementDialog'
import SurveyPanel from '../../components/app/SurveyPanel'
import SiteVisitPanel from '../../components/app/SiteVisitPanel'
import ProposalPanel from '../../components/app/ProposalPanel'
import ConsultationBinding from '../../components/app/ConsultationBinding'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import { DataTable, EmptyState, PageHeader, SectionCard, StatusBadge, TablePagination } from '../../components/app/ui'
import { displayVietnameseTime as displayTime, vietnamDateTimeInput } from '../../utils/display'
import '../../styles/management.css'

const channelLabels = { ONLINE: 'Trực tuyến', ON_SITE: 'Tại vườn' }
const assignmentRefreshInterval = 15000

const statusLabels = { NEW: 'Mới', IN_PROGRESS: 'Đang xử lý', COMPLETED: 'Hoàn thành', CANCELLED: 'Đã hủy',
  PENDING_ACCEPTANCE: 'Chờ kỹ sư tiếp nhận', NEEDS_REASSIGNMENT: 'Cần phân công lại' }

const actionLabels = {
  SUBMITTED: 'Đã gửi yêu cầu', CLAIMED: 'Đã nhận yêu cầu', FOLLOW_UP: 'Đã ghi nhận kết quả theo dõi',
  SURVEY_RECORDED: 'Đã ghi nhận khảo sát', CUSTOMER_BOUND: 'Đã liên kết nông trại của khách hàng',
  PROPOSAL_CREATED: 'Đã tạo phương án thiết bị', PLAN_CONFIRMED: 'Đã xác nhận phương án',
  DEPOSIT_CONFIRMED: 'Đã xác nhận tiền đặt cọc', INSTALLATION_SUBMITTED: 'Đã gửi hồ sơ lắp đặt',
  INSTALLATION_APPROVED: 'Đã duyệt hồ sơ lắp đặt', INSTALLATION_RETURNED: 'Đã trả hồ sơ để chỉnh sửa',
  ASSIGNED: 'Đã phân công kỹ sư', REASSIGNED: 'Đã phân công lại', RETURN_REQUESTED: 'Đã xin trả hồ sơ',
  RETURN_APPROVED: 'Đã duyệt trả hồ sơ', RETURN_REJECTED: 'Đã từ chối trả hồ sơ', CLOSED: 'Đã đóng hồ sơ',
  CHANNEL_CHANGED: 'Đã thay đổi hình thức tư vấn',
  ASSIGNMENT_ACCEPTED: 'Kỹ sư đã tiếp nhận', ASSIGNMENT_REJECTED: 'Kỹ sư đã từ chối',
  ASSIGNMENT_EXPIRED: 'Lượt phân công đã tự động hết hạn',
}

const historyIcons = { SUBMITTED: 'send', ASSIGNED: 'userCheck', REASSIGNED: 'swap', CHANNEL_CHANGED: 'refresh' }
const historyChannels = { UNSPECIFIED: 'Chưa xác định', ONLINE: 'Tư vấn trực tuyến', ON_SITE: 'Tư vấn tại vườn' }
const unknownName = 'Không xác định'

function historyTime(value) {
  const local = vietnamDateTimeInput(value)
  return local ? `${local.slice(0, 10).split('-').reverse().join('/')} ${local.slice(11)}` : 'Không có dữ liệu'
}

function historyNotes(item) {
  switch (item.action) {
    case 'ASSIGNED':
      return <>Kỹ sư được phân công: {item.assignedEngineerName || unknownName}</>
    case 'REASSIGNED':
      return <>Từ: {item.previousEngineerName || unknownName}<br />Sang: {item.assignedEngineerName || unknownName}</>
    case 'CHANNEL_CHANGED': {
      const [before, after] = item.notes?.split(/\s*->\s*/) ?? []
      return `${historyChannels[before] ?? unknownName} → ${historyChannels[after] ?? unknownName}`
    }
    case 'CUSTOMER_BOUND':
      return item.notes?.startsWith('Linked owner #') ? <>Chủ nông trại: {item.linkedOwnerName || unknownName}</> : item.notes
    default:
      return item.notes
  }
}

export default function ConsultationsPage() {
  const { can } = useAuth()
  const admin = can(P.USERS_MANAGE)
  const [filter, setFilter] = useState('all')
  const [page, setPage] = useState(1)
  const activeFilter = admin ? filter : 'all'
  const inbox = activeFilter === 'inbox'
  function selectFilter(value) {
    setFilter(value)
    setPage(1)
  }
  const inboxSummary = useApiQuery('consultations:inbox-summary', signal =>
    api.request('/api/consultations', { query: { inbox: true, page: 1, pageSize: 1 }, signal }), admin, assignmentRefreshInterval)
  const query = useApiQuery(`consultations:${activeFilter}:${page}`, signal =>
    api.request('/api/consultations', {
      query: activeFilter === 'pending' ? { pendingAcceptance: true, page } : { inbox, page },
      signal,
    }), true, assignmentRefreshInterval)
  const unassignedCount = (inbox ? query.data?.pagination?.totalItems : undefined) ?? inboxSummary.data?.pagination?.totalItems
  const pagination = query.data?.pagination
  const footer = pagination?.totalItems > 0 && <TablePagination page={page} totalPages={pagination.totalPages}
    pageSize={pagination.pageSize} totalItems={pagination.totalItems} onPageChange={setPage}
    disabled={query.loading} label="Phân trang yêu cầu tư vấn" />
  if (!admin) {
    const jobs = query.data?.items ?? []
    return <div className="app-ui-stack">
      <PageHeader title="Công việc tư vấn được giao" description="Xem yêu cầu Admin giao và phản hồi các công việc đang chờ bạn." />
      <ApiState vietnamese {...query} />
      {!query.loading && !query.error && !jobs.length && <EmptyState title="Chưa có công việc được giao" />}
      {jobs.length > 0 && <ul className="consultation-jobs">
        {jobs.map(job => {
          const waiting = job.status === 'PENDING_ACCEPTANCE'
          return <li className="app-ui consultation-job" key={job.id}>
            <div className="consultation-job__content">
              <h2>{job.serviceNeed || 'Yêu cầu tư vấn'}</h2>
              <p>Hồ sơ #{job.id} · Yêu cầu lúc {displayTime(job.createdAt)}</p>
            </div>
            <StatusBadge status={job.status} label={waiting ? 'Chờ bạn tiếp nhận' : statusLabels[job.status] ?? 'Không xác định'} />
            <Link className="app-ui-button consultation-detail-link" to={`/app/consultations/${job.id}`}>
              {waiting ? 'Xem yêu cầu' : 'Xem chi tiết'} <Icon name="chevron" size={14} />
            </Link>
          </li>
        })}
      </ul>}
      {footer}
    </div>
  }
  return <>
    <PageHeader title="Tư vấn" description="Tiếp nhận yêu cầu tư vấn và theo dõi các khách hàng được phân công." />
    {admin && <div className="consultation-filters" role="group" aria-label="Lọc yêu cầu tư vấn">
      <button type="button" aria-pressed={activeFilter === 'all'} onClick={() => selectFilter('all')}>
        <Icon name="listFilter" size={18} />
        <span>Tất cả yêu cầu</span>
      </button>
      <button type="button" aria-pressed={inbox} onClick={() => selectFilter('inbox')}>
        <Icon name="clock3" size={18} />
        <span>Chờ phân công</span>
        {unassignedCount > 0 && <span className="consultation-filters__count">{unassignedCount}</span>}
      </button>
      <button type="button" aria-pressed={activeFilter === 'pending'} onClick={() => selectFilter('pending')}>
        <Icon name="userCheck" size={18} />
        <span>Chờ tiếp nhận</span>
      </button>
    </div>}
    <ApiState vietnamese {...query} />
    <DataTable vietnamese caption="Yêu cầu tư vấn" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'id', header: 'Mã yêu cầu', render: value => `#${value}` }, { key: 'serviceNeed', header: 'Nhu cầu dịch vụ' },
      { key: 'status', header: 'Trạng thái', render: value => <StatusBadge status={value} label={statusLabels[value] ?? 'Không xác định'} /> },
      { key: 'createdAt', header: 'Ngày tiếp nhận', render: displayTime },
      { key: 'action', header: 'Thao tác', render: (_, row) => (
        <Link className="app-ui-button consultation-detail-link" to={`/app/consultations/${row.id}`}>
          Xem chi tiết <Icon name="chevron" size={14} />
        </Link>
      ) },
    ]} footer={footer} />
  </>
}

export function ConsultationDetailPage() {
  const { id } = useParams()
  return <ConsultationDetail key={id} id={id} />
}

function ConsultationDetail({ id }) {
  const [searchParams, setSearchParams] = useSearchParams()
  const [submitted, setSubmitted] = useState(false)
  const [surveyRecorded, setSurveyRecorded] = useState(false)
  const { can, user } = useAuth()
  const admin = can(P.USERS_MANAGE)
  const manages = can(P.CONSULTATIONS_MANAGE)
  const query = useApiQuery(`consultation:${id}`, signal =>
    api.request(`/api/consultations/${id}`, { signal }), true, admin ? assignmentRefreshInterval : 0)
  const [values, setValues] = useState({ notes: '', contactResult: '', nextAction: '' })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  async function followUp(event) {
    event.preventDefault(); setPending(true); setError(''); setNotice('')
    try {
      const updated = await api.request(`/api/consultations/${id}/follow-ups`, { method: 'POST', body: values })
      query.replaceData(updated); setValues({ notes: '', contactResult: '', nextAction: '' }); setNotice('Đã lưu kết quả theo dõi.')
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const row = query.data
  const lastReturn = row?.history.filter(item => ['RETURN_REQUESTED', 'RETURN_APPROVED', 'RETURN_REJECTED'].includes(item.action)).at(-1)
  const pendingReturn = lastReturn?.action === 'RETURN_REQUESTED' ? lastReturn : null
  const hasAccepted = ['IN_PROGRESS', 'COMPLETED', 'CANCELLED'].includes(row?.status) ||
    row?.history.some(item => item.status === 'IN_PROGRESS')
  const visit = useApiQuery('site-visit:' + id, signal => api.request('/api/consultations/' + id + '/site-visit', { signal }), Boolean(hasAccepted), 10000)
  const channel = visit.data?.channel ?? row?.channel
  const pendingSite = visit.data?.current?.channel === 'ON_SITE' && visit.data.current.reviewStatus === 'PENDING'
  const canSurvey = Boolean(visit.data && ((channel === 'ONLINE' && !pendingSite) || visit.data.current?.scheduledAt))
  const dossierDecision = row?.history.filter(item => ['INSTALLATION_SUBMITTED', 'INSTALLATION_APPROVED', 'INSTALLATION_RETURNED'].includes(item.action)).at(-1)
  const locked = submitted || (dossierDecision && dossierDecision.action !== 'INSTALLATION_RETURNED')
  const editable = manages && row?.engineerId === user?.id && row?.status === 'IN_PROGRESS'
  const awaitingMyResponse = !admin && manages && row?.engineerId === user?.id && row?.status === 'PENDING_ACCEPTANCE'
  const hasSurvey = surveyRecorded || row?.history.some(item => item.action === 'SURVEY_RECORDED' || item.action === 'PROPOSAL_CREATED')
  const hasFarmStep = admin && (row?.farmId || (row?.status === 'IN_PROGRESS' && row.history.some(item =>
    ['PLAN_CONFIRMED', 'DEPOSIT_CONFIRMED', 'INSTALLATION_SUBMITTED', 'INSTALLATION_APPROVED'].includes(item.action))))
  const history = row?.history ?? []
  const latest = history.at(-1)
  const stages = [
    ...(admin ? [{ id: 'assignment', label: 'Lịch sử & Phân công', available: true, count: history.length, unit: 'mục',
      summary: latest && `Mới nhất: ${actionLabels[latest.action] ?? 'Cập nhật yêu cầu'} · ${displayTime(latest.createdAt)}` }] : []),
    { id: 'consultation', label: admin ? 'Tư vấn' : 'Hình thức tư vấn', available: hasAccepted, summary: channelLabels[channel] },
    { id: 'survey', label: 'Khảo sát nông trại', available: canSurvey || hasSurvey,
      count: history.filter(item => item.action === 'SURVEY_RECORDED').length, unit: 'phiên bản' },
    { id: 'equipment', label: 'Phương án thiết bị', available: hasSurvey,
      count: history.filter(item => item.action === 'PROPOSAL_CREATED').length, unit: 'phiên bản' },
    ...(admin ? [{ id: 'farm', label: 'Nông trại khách hàng', available: hasFarmStep }] : []),
  ]
  const requestedStage = searchParams.get('step')
  const currentStage = stages.find(stage => stage.id === requestedStage)
    ?? stages.find(stage => stage.id === 'consultation' && (!admin || row?.status === 'IN_PROGRESS')) ?? stages[0]
  const activeStage = currentStage.available ? currentStage.id : null
  return <><Link to="/app/consultations">{admin ? '← Quay lại danh sách tư vấn' : '← Quay lại công việc được giao'}</Link>
    <PageHeader title={admin ? `Chi tiết tư vấn #${id}` : `Công việc tư vấn #${id}`} /><ApiState vietnamese {...query} />
    {row && <>
      <SectionCard title={row.customerName} actions={<StatusBadge status={row.status} label={statusLabels[row.status] ?? 'Không xác định'} />}>
        <dl className="consultation-details">
          <div>
            <dt>Số điện thoại</dt>
            <dd>{row.phone ? <a href={`tel:${row.phone}`}>{row.phone}</a> : <span>Chưa cung cấp</span>}</dd>
          </div>
          <div>
            <dt>Email</dt>
            <dd>{row.email || <span>Chưa cung cấp</span>}</dd>
          </div>
          <div>
            <dt>Nhu cầu dịch vụ</dt>
            <dd>{row.serviceNeed || <span>Chưa cung cấp</span>}</dd>
          </div>
          <div>
            <dt>Diện tích (ha)</dt>
            <dd>{row.farmArea ?? <span>Chưa cung cấp</span>}</dd>
          </div>
          <div>
            <dt>Hình thức khách mong muốn</dt>
            <dd>{channelLabels[row.preferredChannel] ?? <span>Chưa cung cấp</span>}</dd>
          </div>
          <div>
            <dt>Loại cây đang trồng</dt>
            <dd>{row.cropType || <span>Chưa cung cấp</span>}</dd>
          </div>
          {admin && <div className="consultation-details__wide">
            <dt>Nhân viên tư vấn</dt>
            <dd>{row.engineerId ? row.engineerName || unknownName : <span>Chưa phân công</span>}</dd>
          </div>}
          <div className="consultation-details__wide">
            <dt>Địa điểm nông trại</dt>
            <dd>{row.farmLocation || <span>Chưa cung cấp</span>}</dd>
          </div>
          <div className="consultation-details__wide">
            <dt>Nội dung</dt>
            <dd>{row.content || <span>Chưa cung cấp</span>}</dd>
          </div>
        </dl>
      </SectionCard>
      {awaitingMyResponse && row.assignment && <SectionCard title="Phản hồi công việc">
        <p>Hạn tiếp nhận: <time dateTime={row.assignment.responseDeadline}>{historyTime(row.assignment.responseDeadline)}</time></p>
        <AssignmentResponse id={id} assignment={row.assignment} onAccepted={query.reload} />
      </SectionCard>}
      {(admin || (hasAccepted && row.status !== 'PENDING_ACCEPTANCE')) && <div className="consultation-steps">
      {stages.map(stage => {
        const expanded = requestedStage !== 'closed' && stage.id === currentStage.id
        return <section className="consultation-step" key={stage.id}>
          <button className="consultation-step__trigger" type="button" aria-expanded={expanded}
            aria-controls={`consultation-step-${stage.id}`} onClick={() => setSearchParams(previous => {
              const next = new URLSearchParams(previous)
              next.set('step', expanded ? 'closed' : stage.id)
              return next
            })}>
            <span><strong>{stage.label}</strong>
              {stage.summary && <small>{stage.summary}</small>}</span>
            {stage.count > 0 && <span className="consultation-step__badge">{stage.count} {stage.unit}</span>}
            <Icon className="consultation-step__arrow" name="down" size={18} />
          </button>
          <div id={`consultation-step-${stage.id}`} hidden={!expanded}>
          {expanded && <div className="consultation-step__body">
      {!activeStage && <EmptyState title="Không có dữ liệu" description="Hồ sơ chưa đến bước này." />}
      {activeStage === 'assignment' && <div className="app-ui-stack">
        <ConsultationHistoryCard row={row} id={id} onSaved={query.replaceData}
        canAssign={admin && !pendingReturn && ['NEW', 'IN_PROGRESS', 'NEEDS_REASSIGNMENT'].includes(row.status)} />
        {(editable || (admin && pendingReturn)) &&
          <ConsultationLifecycle id={id} admin={admin} editable={editable} pendingReturn={pendingReturn} onSaved={query.replaceData} />}
      </div>}
      {activeStage === 'survey' && <SurveyPanel consultationId={id} editable={editable && !locked && canSurvey} onSaved={() => setSurveyRecorded(true)} />}
      {activeStage === 'equipment' && <ProposalPanel consultationId={id} linked={Boolean(row.ownerId && row.farmId)}
          customerName={row.customerName} customerEmail={row.email ?? ''} onSubmitted={() => setSubmitted(true)}
          editable={editable && !locked && canSurvey} canConfirmDeposit={editable || (admin && row.status === 'IN_PROGRESS')} />}
      {activeStage === 'farm' && <ConsultationBinding consultation={row} onSaved={query.replaceData} />}
      {activeStage === 'consultation' && <div className="app-ui-stack">
      <ApiState vietnamese {...visit} />
      {visit.data && <SiteVisitPanel consultationId={id} visit={visit.data} preferredChannel={row.preferredChannel} farmLocation={row.farmLocation}
        requiresPayOs={Boolean(row.preferredChannel)} onSaved={visit.replaceData} editable={editable && !locked} admin={admin}
        title={!visit.data.current && channel !== 'ON_SITE'
          ? (channel === 'ONLINE' ? 'Tư vấn trực tuyến' : 'Hình thức tư vấn') : undefined} />}
      {editable && <SectionCard title="Ghi nhận kết quả theo dõi"><form className="management-form" onSubmit={followUp}>
        <fieldset disabled={pending} className="management-form">{[['notes', 'Ghi chú', 4000], ['contactResult', 'Kết quả liên hệ', 1000], ['nextAction', 'Bước tiếp theo', 1000]].map(([name, label, max]) =>
          <label key={name}>{label}<textarea required maxLength={max} value={values[name]} onChange={event => setValues({ ...values, [name]: event.target.value })} /></label>)}
          <button className="app-ui-button app-ui-button--primary" type="submit">{pending ? 'Đang lưu…' : 'Lưu kết quả theo dõi'}</button></fieldset>
        {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
      </form></SectionCard>}
      {!admin && editable && <ConsultationLifecycle id={id} admin={false} editable pendingReturn={pendingReturn} onSaved={query.replaceData} />}
      </div>}
          </div>}
          </div>
        </section>
      })}
      </div>}
    </>}
  </>
}

function AssignmentResponse({ id, assignment, onAccepted }) {
  const navigate = useNavigate()
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  async function respond(accept) {
    if (pending) return
    setPending(true)
    setError('')
    try {
      await api.request(`/api/consultations/${id}/assignment-response`, {
        method: 'POST', body: { assignmentId: assignment.id, accept, reason: accept ? null : reason.trim() }, retry: false,
      })
      if (accept) onAccepted()
      else navigate('/app/consultations')
    } catch (err) {
      setError(err.message)
    } finally { setPending(false) }
  }

  return <div className="consultation-history__candidates">
    <div className="management-row-actions">
      <button type="button" className="app-ui-button app-ui-button--primary" disabled={pending} onClick={() => respond(true)}>Tiếp nhận</button>
      <button type="button" className="app-ui-button" disabled={pending} onClick={() => { setError(''); setRejecting(true) }}>Từ chối</button>
    </div>
    {!rejecting && error && <p role="alert">{error}</p>}
    {rejecting && <ManagementDialog title="Từ chối phân công" onClose={() => { if (!pending) { setRejecting(false); setError('') } }}>
      <form className="management-form" onSubmit={event => { event.preventDefault(); respond(false) }}>
        <label>Lý do từ chối<textarea required maxLength={1000} value={reason} disabled={pending}
          onChange={event => setReason(event.target.value)} /></label>
        {error && <p role="alert">{error}</p>}
        <button type="submit" className="app-ui-button app-ui-button--primary" disabled={pending || !reason.trim()}>Xác nhận từ chối</button>
      </form>
    </ManagementDialog>}
  </div>
}

function AssignmentTiming({ assignment }) {
  const pending = assignment.status === 'PENDING_ACCEPTANCE'
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!pending) return
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [pending])
  const seconds = Math.max(0, Math.ceil((Date.parse(assignment.responseDeadline) - now) / 1000))
  const remaining = [Math.floor(seconds / 3600), Math.floor(seconds / 60) % 60, seconds % 60]
    .map(value => String(value).padStart(2, '0')).join(':')
  return <div className="consultation-history__timing">
    <p>Kỹ sư được phân công: <strong>{assignment.engineerName || unknownName}</strong></p>
    <p>Phân công lúc <time dateTime={assignment.assignedAt}>{historyTime(assignment.assignedAt)}</time>
      {' · Hạn tiếp nhận '}<time dateTime={assignment.responseDeadline}>{historyTime(assignment.responseDeadline)}</time></p>
    {pending && <p>Thời gian còn lại: <strong>{remaining}</strong></p>}
    {assignment.status === 'EXPIRED' && <p className="management-error">Kỹ sư chưa tiếp nhận công việc trong 5 giờ. Lượt phân công đã tự động hết hạn.</p>}
  </div>
}

function ConsultationHistoryCard({ id, row, canAssign, onSaved }) {
  const currentEngineerId = row.engineerId
  const [expanded, setExpanded] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const history = [...row.history].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt) || b.id - a.id)
  const lastAssignment = history.find(item => ['ASSIGNED', 'REASSIGNED', 'CLAIMED', 'RETURN_APPROVED'].includes(item.action))
  const assignedAt = lastAssignment?.action !== 'RETURN_APPROVED' ? lastAssignment?.createdAt : null
  const engineerName = row.engineerName?.trim() || unknownName
  const initials = row.engineerName?.trim().split(/\s+/).slice(0, 2).map(part => Array.from(part)[0]).join('').toLocaleUpperCase('vi') || '?'
  const candidates = useApiQuery(`consultation-candidates:${id}`, signal =>
    api.request(`/api/consultations/${id}/assignment-candidates`, { signal }), canAssign && expanded, 10000)

  async function assign(engineerId) {
    setPending(true)
    setError('')
    setNotice('')
    try {
      onSaved(await api.request(`/api/consultations/${id}/assign`, {
        method: 'POST', body: { engineerId, expectedEngineerId: currentEngineerId ?? null }, retry: false,
      }))
      setExpanded(false)
      setNotice(currentEngineerId ? 'Đã phân công lại kỹ sư phụ trách.' : 'Đã phân công kỹ sư phụ trách.')
    } catch (err) {
      setError(err.message)
      candidates.reload()
    } finally {
      setPending(false)
    }
  }

  return <SectionCard className="consultation-history"
    title={<span className="consultation-history__title"><Icon name="history" size={20} />Lịch sử & Phân công tư vấn</span>}
    description="Theo dõi quá trình xử lý và thay đổi kỹ sư phụ trách"
    actions={canAssign && <button type="button" className="consultation-history__assign" aria-expanded={expanded}
      disabled={pending} onClick={() => setExpanded(value => !value)}>
      <Icon name="userCog" size={18} />
      {expanded ? 'Ẩn danh sách kỹ sư' : currentEngineerId || row.status === 'NEEDS_REASSIGNMENT' ? 'Phân công lại' : 'Mở danh sách phân công'}
    </button>}>
    <div className="consultation-history__engineer">
      <p>Kỹ sư đang phụ trách</p>
      {currentEngineerId ? <div className="consultation-history__person">
        <span className="consultation-history__avatar" aria-hidden="true">{initials}</span>
        <div><strong>{engineerName}</strong>
          <p>Kỹ sư nông nghiệp{assignedAt && <> · Được phân công lúc <time dateTime={assignedAt}>{displayTime(assignedAt)}</time></>}</p>
        </div>
      </div> : <strong>Chưa phân công</strong>}
    </div>
    {row.assignment && <AssignmentTiming assignment={row.assignment} />}
    {canAssign && expanded && <div className="consultation-history__candidates">
      <ApiState vietnamese {...candidates} />
      {candidates.data && <DataTable vietnamese caption="Kỹ sư nông nghiệp" rows={candidates.data} columns={[
        { key: 'fullName', header: 'Kỹ sư nông nghiệp' },
        { key: 'openCount', header: 'Hồ sơ mở', render: (count, row) => `${count}/${row.capacity}` },
        { key: 'blockedReason', header: 'Lý do không thể gán' },
        { key: 'action', header: 'Thao tác', render: (_, row) => <button type="button"
          aria-label={`Gán cho ${row.fullName}`} className="app-ui-button" disabled={pending || Boolean(row.blockedReason)}
          onClick={() => assign(row.id)}>Gán</button> },
      ]} />}
      {error && <p role="alert">{error}</p>}
    </div>}
    {notice && <p className="management-success" role="status">{notice}</p>}
    {history.length ? <ol className="consultation-history__timeline">
      {history.map(item => {
        const actor = item.action === 'SUBMITTED' ? row.customerName : item.actorName
        const details = historyNotes(item)
        return <li key={item.id}>
          <span className="consultation-history__marker"><Icon name={historyIcons[item.action] ?? 'history'} size={16} /></span>
          <div className="consultation-history__event">
            <strong>{actionLabels[item.action] ?? 'Cập nhật yêu cầu'}</strong>
            <p className="consultation-history__meta">
              <time dateTime={item.createdAt}>{historyTime(item.createdAt)}</time>
              <span>{item.action === 'SUBMITTED' ? 'Khách hàng' : 'Thực hiện bởi'}: {item.action === 'ASSIGNMENT_EXPIRED' ? 'Hệ thống' : actor || unknownName}</span>
            </p>
            {details && <p>{details}</p>}
            {item.contactResult && <p>{item.contactResult}</p>}
            {item.nextAction && <p>{item.nextAction}</p>}
          </div>
        </li>
      })}
    </ol> : <p className="consultation-history__empty">Chưa có lịch sử xử lý.</p>}
  </SectionCard>
}

function ConsultationLifecycle({ id, admin, editable, pendingReturn, onSaved }) {
  const [reason, setReason] = useState('')
  const [status, setStatus] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  async function save(path, body) {
    setPending(true)
    setError('')
    try {
      onSaved(await api.request(`/api/consultations/${id}/${path}`, { method: 'POST', body, retry: false }))
      setReason('')
      setStatus('')
    } catch (err) {
      setError(err.message)
    } finally {
      setPending(false)
    }
  }

  return <SectionCard title="Xử lý phân công">
    {pendingReturn ? <>
      <p>Đang chờ duyệt trả hồ sơ. Lý do: {pendingReturn.notes}</p>
      {admin && <div className="management-row-actions">
        <button type="button" disabled={pending} onClick={() => save('return-requests/review', { requestId: pendingReturn.id, approve: true })}>Duyệt trả hồ sơ</button>
        <button type="button" disabled={pending} onClick={() => save('return-requests/review', { requestId: pendingReturn.id, approve: false })}>Từ chối trả hồ sơ</button>
      </div>}
    </> : editable && <>
      <form className="management-form" onSubmit={event => { event.preventDefault(); save('return-requests', { reason }) }}>
        <label>Lý do xin trả hồ sơ<textarea required maxLength={1000} value={reason} onChange={event => setReason(event.target.value)} /></label>
        <button type="submit" disabled={pending || !reason.trim()}>Gửi yêu cầu trả hồ sơ</button>
      </form>
      <form className="management-form" onSubmit={event => { event.preventDefault(); save('close', { status }) }}>
        <label>Kết quả xử lý<select required value={status} onChange={event => setStatus(event.target.value)}>
          <option value="">Chọn kết quả</option><option value="COMPLETED">Hoàn thành</option><option value="CANCELLED">Hủy hồ sơ</option>
        </select></label>
        <button type="submit" disabled={pending || !status}>Cập nhật kết quả</button>
      </form>
    </>}
    {error && <p role="alert">{error}</p>}
  </SectionCard>
}
