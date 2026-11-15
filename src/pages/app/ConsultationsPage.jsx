import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from '../../components/app/ApiState'
import SurveyPanel from '../../components/app/SurveyPanel'
import SiteVisitPanel from '../../components/app/SiteVisitPanel'
import ProposalPanel from '../../components/app/ProposalPanel'
import ConsultationBinding from '../../components/app/ConsultationBinding'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import { DataTable, PageHeader, SectionCard, StatusBadge, TablePagination } from '../../components/app/ui'
import { displayVietnameseTime as displayTime } from '../../utils/display'
import '../../styles/management.css'

const channelLabels = { ONLINE: 'Trực tuyến', ON_SITE: 'Tại vườn' }

const statusLabels = { NEW: 'Mới', IN_PROGRESS: 'Đang xử lý', COMPLETED: 'Hoàn thành', CANCELLED: 'Đã hủy' }

const actionLabels = {
  SUBMITTED: 'Đã gửi yêu cầu', CLAIMED: 'Đã nhận yêu cầu', FOLLOW_UP: 'Đã ghi nhận kết quả theo dõi',
  SURVEY_RECORDED: 'Đã ghi nhận khảo sát', CUSTOMER_BOUND: 'Đã liên kết nông trại của khách hàng',
  PROPOSAL_CREATED: 'Đã tạo phương án thiết bị', PLAN_CONFIRMED: 'Đã xác nhận phương án',
  DEPOSIT_CONFIRMED: 'Đã xác nhận tiền đặt cọc', INSTALLATION_SUBMITTED: 'Đã gửi hồ sơ lắp đặt',
  INSTALLATION_APPROVED: 'Đã duyệt hồ sơ lắp đặt', INSTALLATION_RETURNED: 'Đã trả hồ sơ để chỉnh sửa',
  ASSIGNED: 'Đã phân công nhân viên', REASSIGNED: 'Đã phân công lại', RETURN_REQUESTED: 'Đã xin trả hồ sơ',
  RETURN_APPROVED: 'Đã duyệt trả hồ sơ', RETURN_REJECTED: 'Đã từ chối trả hồ sơ', CLOSED: 'Đã đóng hồ sơ',
  CHANNEL_CHANGED: 'Đã đổi hình thức tư vấn',
}

export default function ConsultationsPage() {
  const { can } = useAuth()
  const admin = can(P.USERS_MANAGE)
  const [inbox, setInbox] = useState(admin)
  const [page, setPage] = useState(1)
  const query = useApiQuery(`consultations:${inbox}:${page}`, signal => api.request('/api/consultations', { query: { inbox, page }, signal }))
  return <>
    <PageHeader title="Tư vấn" description="Tiếp nhận yêu cầu tư vấn và theo dõi các khách hàng được phân công." />
    <div className="management-row-actions">{admin && [true, false].map(value => <button key={String(value)} className="app-ui-button" aria-pressed={inbox === value}
      onClick={() => { setInbox(value); setPage(1) }}>{value ? 'Chờ phân công' : 'Tất cả yêu cầu tư vấn'}</button>)}</div>
    <ApiState vietnamese {...query} />
    <DataTable vietnamese caption="Yêu cầu tư vấn" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'id', header: 'Mã yêu cầu', render: value => `#${value}` }, { key: 'serviceNeed', header: 'Nhu cầu dịch vụ' },
      { key: 'status', header: 'Trạng thái', render: value => <StatusBadge status={value} label={statusLabels[value] ?? 'Không xác định'} /> },
      { key: 'createdAt', header: 'Ngày tiếp nhận', render: displayTime },
      { key: 'action', header: 'Thao tác', render: (_, row) => <Link to={`/app/consultations/${row.id}`}>Mở #{row.id}</Link> },
    ]} footer={query.data?.pagination.totalItems > 0 && <TablePagination page={page} totalPages={query.data.pagination.totalPages} pageSize={query.data.pagination.pageSize}
      totalItems={query.data.pagination.totalItems} onPageChange={setPage} disabled={query.loading} label="Phân trang yêu cầu tư vấn" />} />
  </>
}

export function ConsultationDetailPage() {
  const { id } = useParams()
  return <ConsultationDetail key={id} id={id} />
}

function ConsultationDetail({ id }) {
  const [submitted, setSubmitted] = useState(false)
  const { can, user } = useAuth()
  const admin = can(P.USERS_MANAGE)
  const manages = can(P.CONSULTATIONS_MANAGE)
  const query = useApiQuery(`consultation:${id}`, signal => api.request(`/api/consultations/${id}`, { signal }))
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
  const visit = useApiQuery('site-visit:' + id, signal => api.request('/api/consultations/' + id + '/site-visit', { signal }), Boolean(row), 10000)
  const channel = visit.data?.channel ?? row?.channel
  const pendingSite = visit.data?.current?.channel === 'ON_SITE' && visit.data.current.reviewStatus === 'PENDING'
  const canSurvey = Boolean(visit.data && ((channel === 'ONLINE' && !pendingSite) || visit.data.current?.scheduledAt))
  const dossierDecision = row?.history.filter(item => ['INSTALLATION_SUBMITTED', 'INSTALLATION_APPROVED', 'INSTALLATION_RETURNED'].includes(item.action)).at(-1)
  const locked = submitted || (dossierDecision && dossierDecision.action !== 'INSTALLATION_RETURNED')
  const editable = manages && row?.engineerId === user?.id && row?.status === 'IN_PROGRESS'
  return <><Link to="/app/consultations">← Quay lại danh sách tư vấn</Link><PageHeader title={`Chi tiết tư vấn #${id}`} /><ApiState vietnamese {...query} />
    {row && <>
      <SectionCard title={row.customerName} actions={<StatusBadge status={row.status} label={statusLabels[row.status] ?? 'Không xác định'} />}>
        <dl><dt>Số điện thoại</dt><dd>{row.phone}</dd><dt>Email</dt><dd>{row.email || 'Chưa cung cấp'}</dd><dt>Địa điểm nông trại</dt><dd>{row.farmLocation}</dd>
          <dt>Diện tích (ha)</dt><dd>{row.farmArea ?? 'Chưa cung cấp'}</dd><dt>Hình thức khách mong muốn</dt><dd>{channelLabels[row.preferredChannel] ?? 'Chưa cung cấp'}</dd><dt>Loại cây đang trồng</dt><dd>{row.cropType || 'Chưa cung cấp'}</dd><dt>Nhân viên tư vấn</dt><dd>{row.engineerId ? `#${row.engineerId}` : 'Chưa phân công'}</dd><dt>Nhu cầu dịch vụ</dt><dd>{row.serviceNeed}</dd><dt>Nội dung</dt><dd>{row.content}</dd></dl>
      </SectionCard>
      <SectionCard title="Lịch sử xử lý"><ol>{row.history.map(item => <li key={item.id}><strong>{actionLabels[item.action] ?? 'Cập nhật yêu cầu'}</strong> · {displayTime(item.createdAt)}
        {item.userId && <span> · Người dùng #{item.userId}</span>}<p>{item.notes}</p><p>{item.contactResult}</p><p>{item.nextAction}</p></li>)}</ol></SectionCard>
      {(editable || (admin && pendingReturn)) &&
        <ConsultationLifecycle id={id} admin={admin} editable={editable} pendingReturn={pendingReturn} onSaved={query.replaceData} />}
      {admin && !pendingReturn && ['NEW', 'IN_PROGRESS'].includes(row.status) &&
        <ConsultationAssignment key={row.engineerId ?? 'unassigned'} id={id} currentEngineerId={row.engineerId} onSaved={query.replaceData} />}
      {admin && row.status === 'IN_PROGRESS' && <ConsultationBinding consultation={row} onSaved={query.replaceData} />}
      <ApiState vietnamese {...visit} />
      {visit.data && <SiteVisitPanel consultationId={id} visit={visit.data} preferredChannel={row.preferredChannel} farmLocation={row.farmLocation} requiresVnpay={Boolean(row.preferredChannel)} onSaved={visit.replaceData} editable={editable && !locked} admin={can(P.USERS_MANAGE)} />}
      <SurveyPanel key={id} consultationId={id} editable={editable && !locked && canSurvey} />
      <ProposalPanel key={`proposal:${id}`} consultationId={id} linked={Boolean(row.ownerId && row.farmId)} customerName={row.customerName} customerEmail={row.email ?? ''} onSubmitted={() => setSubmitted(true)} editable={editable && !locked} canConfirmDeposit={editable || can(P.USERS_MANAGE)} />
      {editable && <SectionCard title="Ghi nhận kết quả theo dõi"><form className="management-form" onSubmit={followUp}>
        <fieldset disabled={pending} className="management-form">{[['notes', 'Ghi chú', 4000], ['contactResult', 'Kết quả liên hệ', 1000], ['nextAction', 'Bước tiếp theo', 1000]].map(([name, label, max]) =>
          <label key={name}>{label}<textarea required maxLength={max} value={values[name]} onChange={event => setValues({ ...values, [name]: event.target.value })} /></label>)}
          <button className="app-ui-button app-ui-button--primary" type="submit">{pending ? 'Đang lưu…' : 'Lưu kết quả theo dõi'}</button></fieldset>
        {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
      </form></SectionCard>}
    </>}
  </>
}

function ConsultationAssignment({ id, currentEngineerId, onSaved }) {
  const [expanded, setExpanded] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const candidates = useApiQuery(`consultation-candidates:${id}`, signal =>
    api.request(`/api/consultations/${id}/assignment-candidates`, { signal }), expanded, 10000)

  async function assign(engineerId) {
    setPending(true)
    setError('')
    try {
      onSaved(await api.request(`/api/consultations/${id}/assign`, {
        method: 'POST', body: { engineerId, expectedEngineerId: currentEngineerId ?? null }, retry: false,
      }))
    } catch (err) {
      setError(err.message)
      candidates.reload()
    } finally {
      setPending(false)
    }
  }

  return <SectionCard title={currentEngineerId ? 'Phân công lại kỹ sư nông nghiệp' : 'Phân công kỹ sư nông nghiệp'}>
    <button type="button" className="app-ui-button" aria-expanded={expanded} onClick={() => setExpanded(value => !value)}>
      {expanded ? 'Ẩn danh sách kỹ sư' : currentEngineerId ? 'Phân công lại' : 'Mở danh sách phân công'}
    </button>
    {expanded && <>
      <ApiState vietnamese {...candidates} />
      {candidates.data && <DataTable vietnamese caption="Kỹ sư nông nghiệp" rows={candidates.data} columns={[
        { key: 'fullName', header: 'Kỹ sư nông nghiệp' },
        { key: 'openCount', header: 'Hồ sơ mở', render: (count, row) => `${count}/${row.capacity}` },
        { key: 'blockedReason', header: 'Lý do không thể gán' },
        { key: 'action', header: 'Thao tác', render: (_, row) => <button type="button"
          aria-label={`Gán cho ${row.fullName}`} className="app-ui-button" disabled={pending || Boolean(row.blockedReason)}
          onClick={() => assign(row.id)}>Gán</button> },
      ]} />}
    </>}
    {error && <p role="alert">{error}</p>}
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
