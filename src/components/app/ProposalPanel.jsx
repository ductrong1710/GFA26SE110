import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import EvidenceImages from './EvidenceImages'
import ApiState from './ApiState'
import { DataTable, SectionCard, StatusBadge, TablePagination } from './ui'
import { displayVietnameseTime as displayTime, parseVietnamDateTime, formatVnd } from '../../utils/display'

const emptyLine = () => ({ surveyZoneId: '', publicContentId: '', deviceType: '', quantity: 1, installationLocation: '' })

const proposalStatusLabels = {
  DRAFT: 'Bản nháp', PLAN_CONFIRMED: 'Đã xác nhận phương án', DEPOSIT_CONFIRMED: 'Đã xác nhận đặt cọc',
  SUBMITTED: 'Đã gửi hồ sơ', WAITING_ADMIN: 'Chờ quản trị viên duyệt', RETURNED: 'Đã trả để chỉnh sửa',
}

export default function ProposalPanel({ consultationId, editable = false, canConfirmDeposit = editable, onSubmitted, linked = true, customerName = '', customerEmail = '' }) {
  const [page, setPage] = useState(1)
  const [draft, setDraft] = useState(null)
  const [selected, setSelected] = useState(null)
  const [pending, setPending] = useState(false)
  const [note, setNote] = useState('')
  const [name, setName] = useState(customerName)
  const [email, setEmail] = useState(customerEmail)
  const [bills, setBills] = useState([])
  const [reading, setReading] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const path = `/api/consultations/${consultationId}/proposals`
  const query = useApiQuery(`proposals:${consultationId}:${page}`, signal => api.request(path, { query: { page }, signal }))
  const surveys = useApiQuery(`proposal-surveys:${consultationId}:${!!draft}`, signal => api.request(`/api/consultations/${consultationId}/surveys`, { signal }), !!draft)
  const catalog = useApiQuery(`proposal-products:${!!draft}`, signal => api.request('/api/public-devices', { signal }), !!draft)
  const products = catalog.data?.filter(entry => entry.estimatedMonthlyRentVnd != null) ?? []
  const survey = useApiQuery(`proposal-survey:${consultationId}:${draft?.surveyId}`, signal => api.request(`/api/consultations/${consultationId}/surveys/${draft.surveyId}`, { signal }), !!draft?.surveyId)
  async function open(id) {
    setPending(true); setError(''); setNotice(''); setNote('')
    try { setSelected(await api.request(`${path}/${id}`)); setDraft(null) }
    catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function save(event) {
    event.preventDefault(); if (pending) return
    setPending(true); setError(''); setNotice('')
    try {
      const saved = await api.request(path, { method: 'POST', body: { ...draft, surveyId: Number(draft.surveyId), rentalMonths: Number(draft.rentalMonths),
        requestedInstallationAt: parseVietnamDateTime(draft.requestedInstallationAt).toISOString(), notes: draft.notes || null,
        lines: draft.lines.map(line => ({ ...line, publicContentId: Number(line.publicContentId), surveyZoneId: Number(line.surveyZoneId), quantity: Number(line.quantity) })) } })
      setSelected(saved); setDraft(null); setPage(1); query.reload(); setNotice(`Đã lưu phương án số ${saved.revision}.`)
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function transition(action) {
    if (pending) return
    setPending(true); setError(''); setNotice('')
    try {
      if (action === 'submit-installation') {
        const dossier = await api.request('/api/installation-dossiers/' + selected.id + '/submit', {
          method: 'POST', toastMessage: 'Đã gửi yêu cầu lắp đặt.', body: { expectedRevision: selected.revision, note, bills, ...(!linked && { customer: { fullName: name.trim(), email: email.trim() } }) },
        })
        setSelected({ ...selected, status: dossier.status, depositBills: bills }); setBills([])
        setNotice(`Đã gửi hồ sơ #${dossier.proposalId} để quản trị viên duyệt.`)
        onSubmitted?.()
      } else {
        setSelected(await api.request(`${path}/${selected.id}/${action}`, { method: 'POST', body: { note } }))
        setNotice(action === 'confirm-plan' ? 'Đã ghi nhận khách hàng xác nhận phương án.' : 'Đã ghi nhận xác nhận tiền đặt cọc bên ngoài hệ thống.')
      }
      setNote(''); query.reload()
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const field = name => ({ value: draft[name], onChange: event => setDraft({ ...draft, [name]: event.target.value }) })
  const lineField = (index, name) => ({ value: draft.lines[index][name], onChange: event => setDraft({ ...draft,
    lines: draft.lines.map((line, i) => i === index ? { ...line, [name]: event.target.value } : line) }) })
  const confirmation = selected?.status === 'DRAFT' && editable ? 'confirm-plan' : selected?.status === 'PLAN_CONFIRMED' && canConfirmDeposit ? 'confirm-deposit' : null
  return <SectionCard title="Phương án thiết bị" description="Mỗi phiên bản lưu bản khảo sát, thiết bị đề xuất, thời hạn thuê và lịch lắp đặt mong muốn.">
    <ApiState vietnamese {...query} />{error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {editable && !draft && page === 1 && !query.loading && !query.error && !['SUBMITTED', 'WAITING_ADMIN'].includes(query.data?.items[0]?.status) && <button className="app-ui-button" disabled={pending} onClick={() => {
      setDraft({ expectedRevision: query.data?.items[0]?.revision ?? 0, surveyId: '', rentalMonths: '', requestedInstallationAt: '', notes: '', lines: [emptyLine()] })
      setSelected(null); setError(''); setNotice('')
    }}>Lập phương án</button>}
    {draft && <form aria-label="Phương án thiết bị" className="management-form" onSubmit={save}><fieldset disabled={pending} className="management-form"><legend>Phiên bản phương án {draft.expectedRevision + 1}</legend>
      <ApiState vietnamese {...surveys} /><ApiState vietnamese {...survey} />
      <ApiState vietnamese {...catalog} />
      {catalog.data && !products.length && <p>Chưa có thiết bị công khai nào có giá thuê theo tháng. Quản trị viên có thể đăng thiết bị trong mục Nội dung trang chủ.</p>}
      <label>Phiên bản khảo sát<select required value={draft.surveyId} onChange={event => setDraft({ ...draft, surveyId: event.target.value, lines: [emptyLine()] })}>
        <option value="">Chọn bản khảo sát mới nhất</option>{surveys.data?.items.slice(0, 1).map(row => <option key={row.id} value={row.id}>Phiên bản {row.revision}: {row.farmName}</option>)}</select></label>
      <label>Thời hạn thuê (tháng)<input required type="number" min="1" max="120" step="1" {...field('rentalMonths')} /></label>
      <label>Thời gian lắp đặt mong muốn<input required type="datetime-local" {...field('requestedInstallationAt')} /></label>
      <label>Ghi chú phương án<textarea maxLength={4000} {...field('notes')} /></label>
      {draft.lines.map((line, index) => <fieldset key={index} className="management-form"><legend>Thiết bị {index + 1}</legend>
        <label>Khu vực khảo sát<select required {...lineField(index, 'surveyZoneId')}><option value="">Chọn khu vực</option>{survey.data?.zones.map(zone => <option key={zone.id} value={zone.id}>{zone.name}</option>)}</select></label>
        <label>Loại thiết bị<input required maxLength={150} {...lineField(index, 'deviceType')} /></label>
        <label>Thiết bị cho thuê đã công khai<select required {...lineField(index, 'publicContentId')}><option value="">Chọn thiết bị có giá thuê</option>
          {products.map(entry => <option key={entry.id} value={entry.id}>{entry.title} · {formatVnd(entry.estimatedMonthlyRentVnd)}/tháng</option>)}
        </select></label>
        <label>Số lượng<input required type="number" min="1" max="1000" step="1" {...lineField(index, 'quantity')} /></label>
        <label>Vị trí lắp đặt<textarea required maxLength={1000} {...lineField(index, 'installationLocation')} /></label>
        {draft.lines.length > 1 && <button type="button" onClick={() => setDraft({ ...draft, lines: draft.lines.filter((_, i) => i !== index) })}>Xóa thiết bị {index + 1}</button>}
      </fieldset>)}
      <button type="button" disabled={draft.lines.length >= 100} onClick={() => setDraft({ ...draft, lines: [...draft.lines, emptyLine()] })}>Thêm thiết bị đề xuất</button>
      <button type="submit" className="app-ui-button app-ui-button--primary" disabled={!survey.data || !!survey.error}>Lưu phiên bản phương án</button>
      <button type="button" onClick={() => { setDraft(null); setError('') }}>Hủy bản nháp phương án</button>
    </fieldset></form>}
    {selected && <section aria-label="Chi tiết phương án"><h3>Phiên bản phương án {selected.revision}</h3><StatusBadge status={selected.status} label={proposalStatusLabels[selected.status] ?? 'Không xác định'} />
      <p>Khảo sát #{selected.surveyId} · Thời hạn thuê: {selected.rentalMonths} tháng · Lịch lắp đặt mong muốn: {displayTime(selected.requestedInstallationAt)}</p><p>{selected.notes}</p>
      <ul>{selected.lines.map((line, index) => <li key={line.id ?? index}>{line.quantity} × {line.deviceType} · Khu vực khảo sát #{line.surveyZoneId} · {line.installationLocation}
        {line.productName && <p>{line.productName} · {formatVnd(line.monthlyRentVnd)}/tháng · Phiên bản giá {line.priceRevision}</p>}</li>)}</ul>
      {selected.lines.length > 0 && selected.lines.every(line => line.monthlyRentVnd != null) &&
        <p>Tiền thuê đã thỏa thuận ban đầu: {formatVnd(selected.lines.reduce((sum, line) => sum + line.monthlyRentVnd * line.quantity, 0) * selected.rentalMonths)}</p>}
      {selected.planConfirmedAt && <p>Phương án được xác nhận lúc {displayTime(selected.planConfirmedAt)} bởi người dùng #{selected.planConfirmedByUserId}: {selected.planConfirmationNote}</p>}
      {selected.depositConfirmedAt && <p>Tiền đặt cọc được xác nhận lúc {displayTime(selected.depositConfirmedAt)} bởi người dùng #{selected.depositConfirmedByUserId}: {selected.depositConfirmationNote}</p>}
      {confirmation && <form className="management-form" onSubmit={event => { event.preventDefault(); transition(confirmation) }}><fieldset disabled={pending}>
        <label>Minh chứng hoặc mã tham chiếu xác nhận<textarea required maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></label>
        <button type="submit">{confirmation === 'confirm-plan' ? 'Ghi nhận khách hàng xác nhận phương án' : 'Ghi nhận xác nhận tiền đặt cọc bên ngoài hệ thống'}</button>
      </fieldset></form>}
      {selected.dossierReviewNote && <p>Ý kiến của quản trị viên: {selected.dossierReviewNote}</p>}
      <EvidenceImages vietnamese images={selected.depositBills} />
      {editable && selected.status === 'DEPOSIT_CONFIRMED' && (<form onSubmit={event => { event.preventDefault(); transition('submit-installation') }}>
        <fieldset disabled={pending}><legend>Hồ sơ lắp đặt</legend>
          {!linked && <><p>Quản trị viên sẽ xác minh thông tin trước khi tạo hoặc liên kết tài khoản chủ nông trại và nông trại.</p>
            <label>Tên tài khoản khách hàng<input required maxLength={150} value={name} onChange={e => setName(e.target.value)} /></label>
            <label>Email tài khoản khách hàng<input required maxLength={255} type="email" value={email} onChange={e => setEmail(e.target.value)} /></label></>}
          <label>Ghi chú hồ sơ<textarea required maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></label>
          <EvidenceImages vietnamese images={bills} onChange={setBills} onBusyChange={setReading} />
          <button disabled={reading || !bills.length}>Gửi yêu cầu lắp đặt</button>
        </fieldset></form>)}
    </section>}
    <DataTable vietnamese caption="Các phiên bản phương án" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'revision', header: 'Phiên bản' }, { key: 'status', header: 'Trạng thái', render: value => <StatusBadge status={value} label={proposalStatusLabels[value] ?? 'Không xác định'} /> },
      { key: 'createdAt', header: 'Ngày ghi nhận', render: displayTime },
      { key: 'action', header: 'Thao tác', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>Xem phiên bản phương án {row.revision}</button> },
    ]} footer={(query.data?.pagination?.totalItems ?? 0) > 0 && <TablePagination page={page} totalPages={query.data?.pagination?.totalPages} pageSize={query.data?.pagination?.pageSize} totalItems={query.data?.pagination?.totalItems} onPageChange={setPage} disabled={query.loading} label="Phân trang các phiên bản phương án" />} />
  </SectionCard>
}
