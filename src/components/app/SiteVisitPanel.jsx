import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { SectionCard } from './ui'
import EvidenceImages from './EvidenceImages'

const money = value => value == null ? '—' : new Intl.NumberFormat('vi-VN').format(value) + ' đ'
const date = value => new Date(value).toLocaleString('vi-VN')

export default function SiteVisitPanel({ consultationId, visit, editable, admin, onSaved }) {
  const base = '/api/consultations/' + consultationId
  const [form, setForm] = useState({})
  const [bills, setBills] = useState([])
  const [reading, setReading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [retryPreview, setRetryPreview] = useState(0)
  const current = visit.current
  const channel = visit.channel
  const locked = current?.scheduledAt || current?.paymentStatus === 'CONFIRMED'
  const field = key => ({ value: form[key] ?? '', onChange: e => setForm({ ...form, [key]: e.target.value }) })
  const siteForm = editable && !locked && (channel === 'ON_SITE' || (visit.revision > 0 && form.adjustChannel === 'ON_SITE'))
  const preview = useApiQuery('visit-fee:' + [consultationId, form.officeCode, form.distanceKm, retryPreview].join(':'),
    signal => api.request(base + '/site-visit/fee-preview', { method: 'POST', body: { officeCode: form.officeCode, distanceKm: Number(form.distanceKm) }, signal }),
    Boolean(siteForm && form.officeCode && Number(form.distanceKm) > 0))
  const quote = preview.data
  const manual = quote?.status === 'MANUAL_QUOTE_REQUIRED'
  async function save(path, values, method = 'POST') {
    if (busy) return
    setBusy(true); setError('')
    try {
      const updated = await api.request(base + path, { method, body: { ...values, expectedRevision: visit.revision }, retry: false })
      onSaved(updated); setForm({}); setBills([])
    } catch (err) { setError(err.message); if (path.endsWith('/proposals')) setRetryPreview(n => n + 1) }
    finally { setBusy(false) }
  }
  const proposal = e => {
    e.preventDefault()
    if (!quote) return
    save('/site-visit/proposals', { channel: 'ON_SITE', officeCode: form.officeCode, distanceKm: Number(form.distanceKm),
      mapsUrl: form.mapsUrl, proposedAt: new Date(form.proposedAt).toISOString(), pricingVersion: quote.pricingVersion,
      ...(channel !== 'ON_SITE' ? { reason: form.channelReason } : {}),
      ...(manual ? { manualAmountVnd: Number(form.amount), manualReason: form.manualReason } : {}) })
  }
  return <SectionCard title="Phí và lịch khảo sát">
    <p>{channel === 'ONLINE' ? 'Tư vấn trực tuyến · 0 đ' : channel === 'ON_SITE' ? 'Khảo sát tại vườn' : 'Chưa xác định hình thức tư vấn'}</p>
    {current && <dl><dt>Phiên bản</dt><dd>{current.revision} · {current.reviewStatus}</dd><dt>Phí đề xuất</dt><dd>{money(current.amountVnd)}</dd>
      {current.officeLabel && <><dt>Trụ sở / quãng đường một chiều</dt><dd>{current.officeLabel} · {current.distanceKm} km · <a href={current.mapsUrl} target="_blank" rel="noreferrer">Google Maps</a></dd></>}
      {current.manualReason && <><dt>Lý do báo giá riêng</dt><dd>{current.manualReason}</dd></>}
      {current.reviewReason && <><dt>Lý do phản hồi</dt><dd>{current.reviewReason}</dd></>}
      {current.proposedAt && <><dt>Lịch đề xuất</dt><dd>{date(current.proposedAt)}</dd></>}
      <dt>Thanh toán</dt><dd>{current.paymentStatus}</dd><dt>Lịch khảo sát</dt><dd>{current.scheduledAt ? 'Đã chốt' : 'Chưa chốt'}</dd></dl>}
    {admin && current?.payment && <><p>Thực nhận {money(current.payment.paidAmountVnd)} · {date(current.payment.paidAt)} · {current.payment.reference}</p>
      <EvidenceImages images={current.payment.bills ?? []} /></>}
    {error && <p role="alert">{error} <button type="button" onClick={async () => {
      try { onSaved(await api.request(base + '/site-visit')); setError('') } catch (err) { setError(err.message) }
    }}>Tải phiên bản hiện hành</button></p>}
    <fieldset disabled={busy} className="management-form">
      {editable && !locked && <>{!visit.revision ? <label>Ghi nhận lựa chọn của khách<select value={channel ?? ''} onChange={e => save('/channel', { channel: e.target.value }, 'PUT')}>
        <option value="" disabled>Chọn hình thức</option><option value="ONLINE">Trực tuyến</option><option value="ON_SITE">Tại vườn</option></select></label>
        : channel === 'ONLINE' && <button type="button" onClick={() => setForm({ ...form, adjustChannel: 'ON_SITE' })}>Đề xuất chuyển sang tại vườn</button>}
        {visit.revision > 0 && channel === 'ON_SITE' && <form onSubmit={e => { e.preventDefault(); save('/site-visit/proposals', { channel:'ONLINE', reason:form.channelReason }) }}>
          <label>Lý do chuyển sang trực tuyến<input required {...field('channelReason')} /></label><button>Gửi điều chỉnh hình thức</button></form>}
      </>}
      {siteForm && <form className="management-form" onSubmit={proposal}>
        {channel === 'ONLINE' && <label>Lý do chuyển sang tại vườn<input required {...field('channelReason')} /></label>}
        <label>Trụ sở<select required {...field('officeCode')}><option value="">Chọn trụ sở</option>{visit.offices?.map(o => <option key={o.code} value={o.code}>{o.label}</option>)}</select></label>
        <label>Quãng đường một chiều (km)<input required type="number" min=".01" step=".01" {...field('distanceKm')} /></label>
        <label>Đường dẫn Google Maps<input required type="url" {...field('mapsUrl')} /></label>
        <label>Lịch khảo sát dự kiến<input required type="datetime-local" {...field('proposedAt')} /></label>
        {preview.error && <p role="alert">{preview.error.message} <button type="button" onClick={preview.reload}>Thử lại</button></p>}
        {quote && <p role="status">{manual ? 'Cần báo giá riêng' : quote.status === 'PRICING_NOT_CONFIGURED' ? 'Chưa cấu hình biểu phí' : money(quote.amountVnd)}</p>}
        {manual && <><label>Báo giá riêng (VND)<input required type="number" min="1" step="1" max={Number.MAX_SAFE_INTEGER} {...field('amount')} /></label>
          <label>Lý do báo giá riêng<input required {...field('manualReason')} /></label></>}
        <button disabled={!quote || quote.status === 'PRICING_NOT_CONFIGURED'}>Gửi duyệt phí / lịch</button>
      </form>}
      {admin && current?.reviewStatus === 'PENDING' && <>
        <button type="button" onClick={() => save('/site-visit/review',{decision:'APPROVE'})}>Duyệt đề xuất</button>
        <form onSubmit={e => { e.preventDefault(); save('/site-visit/review',{decision:'RETURN',reason:form.reason}) }}>
          <label>Lý do trả sửa<input required {...field('reason')} /></label><button>Trả sửa</button></form></>}
      {admin && current?.reviewStatus === 'APPROVED' && current.channel === 'ON_SITE' && !current.scheduledAt &&
        (current.paymentStatus === 'UNPAID' ? <form className="management-form" onSubmit={e => { e.preventDefault(); save('/site-visit/payments/confirm',{
          paidAmountVnd:Number(form.paid), paidAt:new Date(form.paidAt).toISOString(), reference:form.reference, bills }) }}>
          <label>Số tiền thực nhận (VND)<input required type="number" min="1" step="1" {...field('paid')} /></label>
          <label>Thời điểm giao dịch<input required type="datetime-local" {...field('paidAt')} /></label>
          <label>Mã tham chiếu<input required {...field('reference')} /></label><EvidenceImages images={bills} onChange={setBills} onBusyChange={setReading} />
          <button disabled={reading || !bills.length}>Xác nhận đã nhận tiền</button></form>
          : <button type="button" onClick={() => save('/site-visit/schedule',{})}>Chốt lịch khảo sát</button>)}
    </fieldset>
  </SectionCard>
}
