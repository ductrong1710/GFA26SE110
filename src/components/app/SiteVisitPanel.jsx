import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { SectionCard } from './ui'
import EvidenceImages from './EvidenceImages'

const money = value => value == null ? '—' : new Intl.NumberFormat('vi-VN').format(value) + ' đ'
const date = value => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })

const reviewLabels = { PENDING: 'Chờ duyệt', APPROVED: 'Đã duyệt', RETURNED: 'Đã trả để chỉnh sửa', NOT_REQUIRED: 'Không cần duyệt' }
const paymentLabels = { UNPAID: 'Chưa thanh toán', CONFIRMED: 'Đã xác nhận', NOT_REQUIRED: 'Không cần thanh toán' }
const vnpayLabels = { PENDING: 'Chờ thanh toán', SUCCEEDED: 'Thành công', FAILED: 'Thất bại', RECONCILIATION_REQUIRED: 'Cần đối soát', UNKNOWN: 'Hết hạn, chờ đối soát' }

export default function SiteVisitPanel({ consultationId, visit, editable, admin, preferredChannel, farmLocation, requiresVnpay, onSaved }) {
  const base = '/api/consultations/' + consultationId
  const [form, setForm] = useState({})
  const [bills, setBills] = useState([])
  const [reading, setReading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [checkout, setCheckout] = useState(null)
  const [copied, setCopied] = useState(false)
  const [retryPreview, setRetryPreview] = useState(0)
  const current = visit.current
  const channel = visit.channel
  const previousChoice = channel ?? preferredChannel ?? current?.channel
  const requiresChangeReason = previousChoice === 'ONLINE'
  const selectedOffice = visit.offices?.find(office => office.code === form.officeCode)
  const freshCheckout = checkout && checkout.revision === visit.revision ? checkout.data : null
  const savedAttempt = current?.vnpay
  const vnpay = freshCheckout && (!savedAttempt || (savedAttempt.status === 'FAILED' && savedAttempt.txnRef !== freshCheckout.txnRef && savedAttempt.expiresAt < freshCheckout.expiresAt))
    ? freshCheckout : savedAttempt
  const vnpayBlocked = Boolean(vnpay && vnpay.status !== 'FAILED')
  const awaitingPayment = current?.reviewStatus === 'APPROVED' && current.channel === 'ON_SITE' &&
    current.paymentStatus === 'UNPAID' && !current.scheduledAt
  const locked = current?.scheduledAt || current?.paymentStatus === 'CONFIRMED'
  const field = key => ({ value: form[key] ?? '', onChange: e => setForm({ ...form, [key]: e.target.value }) })
  const siteForm = editable && !locked && (channel === 'ON_SITE' || current?.channel === 'ON_SITE' || form.adjustChannel === 'ON_SITE') && (!current || current.reviewStatus === 'RETURNED' || form.adjustChannel === 'ON_SITE')
  const preview = useApiQuery('visit-fee:' + [consultationId, form.officeCode, form.distanceKm, retryPreview].join(':'),
    signal => api.request(base + '/site-visit/fee-preview', { method: 'POST', body: { officeCode: form.officeCode, distanceKm: Number(form.distanceKm) }, signal }),
    Boolean(siteForm && form.officeCode && Number(form.distanceKm) > 0))
  const quote = preview.data
  const tariff = visit.tariff
  const hasExtraDistance = quote?.status === 'CALCULATED' && tariff && quote.distanceKm > tariff.baseThroughKm
  async function createCheckout() {
    if (busy || vnpayBlocked || !visit.vnpayEnabled) return
    setBusy(true); setError(''); setCopied(false)
    try {
      const data = await api.request(base + '/site-visit/payments/vnpay/checkout', {
        method: 'POST', body: { expectedRevision: visit.revision }, retry: false,
      })
      setCheckout({ revision: visit.revision, data })
    } catch (err) { setError(err.message) }
    finally { setBusy(false) }
  }
  async function refreshVisit() {
    setBusy(true)
    try { onSaved(await api.request(base + '/site-visit')); setCheckout(null); setCopied(false); setError('') }
    catch (err) { setError(err.message) }
    finally { setBusy(false) }
  }
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
      gardenAddress: form.gardenAddress ?? current?.gardenAddress ?? farmLocation,
      mapsUrl: form.mapsUrl, proposedAt: new Date(form.proposedAt + '+07:00').toISOString(), pricingVersion: quote.pricingVersion,
      ...(requiresChangeReason ? { reason: form.channelReason } : {}) })
  }
  return <SectionCard title="Báo cáo tư vấn tại vườn và thanh toán">
    <p>{channel === 'ONLINE' ? 'Tư vấn trực tuyến · 0 đ' : channel === 'ON_SITE' ? 'Khảo sát tại vườn' : current?.channel === 'ON_SITE' ? 'Đã gửi báo cáo tư vấn tại vườn' : 'Chờ nhân viên liên hệ và trao đổi với khách'}</p>
    {current && <dl><dt>Phiên bản</dt><dd>{current.revision} · {reviewLabels[current.reviewStatus] ?? 'Không xác định'}</dd><dt>Hình thức trong báo cáo</dt><dd>{current.channel === 'ONLINE' ? 'Trực tuyến' : 'Tại vườn'}</dd><dt>Phí đề xuất</dt><dd>{money(current.amountVnd)}</dd>
      {current.officeLabel && <><dt>Trụ sở / quãng đường một chiều</dt><dd>{current.officeLabel} · {current.distanceKm} km</dd><dt>Vị trí trên Google Maps</dt><dd>{current.officeMapsUrl && <><a href={current.officeMapsUrl} target="_blank" rel="noreferrer">Trụ sở xuất phát</a> · </>}<a href={current.mapsUrl} target="_blank" rel="noreferrer">Nông trại</a></dd></>}
      {current.gardenAddress && <><dt>Địa chỉ vườn</dt><dd>{current.gardenAddress}</dd></>}
      {current.manualReason && <><dt>Lý do báo giá riêng</dt><dd>{current.manualReason}</dd></>}
      {current.changeReason && <><dt>Lý do thay đổi hình thức tư vấn</dt><dd>{current.changeReason}</dd></>}
      {current.reviewReason && <><dt>Lý do phản hồi</dt><dd>{current.reviewReason}</dd></>}
      {current.proposedAt && <><dt>{current.scheduledAt ? 'Lịch tư vấn đã chốt (giờ Việt Nam)' : 'Lịch tư vấn dự kiến (giờ Việt Nam)'}</dt><dd>{date(current.proposedAt)}</dd></>}
      <dt>Thanh toán</dt><dd>{paymentLabels[current.paymentStatus] ?? 'Không xác định'}</dd><dt>Lịch khảo sát</dt><dd>{current.scheduledAt ? 'Đã chốt' : 'Chưa chốt'}</dd></dl>}
    {admin && current?.payment && <>{current.payment.source === 'VNPAY_SANDBOX' && <p>VNPAY Sandbox · Chỉ dùng thử nghiệm, không thanh toán bằng tiền thật.</p>}
      <p>Thực nhận {money(current.payment.paidAmountVnd)} · {date(current.payment.paidAt)} · {current.payment.reference}</p>
      <EvidenceImages vietnamese images={current.payment.bills ?? []} /></>}
    {error && <p role="alert">{error} <button type="button" disabled={busy} onClick={refreshVisit}>Tải phiên bản hiện hành</button></p>}
    <fieldset disabled={busy} className="management-form">
      {editable && !locked && <>
        {!visit.revision && !channel && previousChoice !== 'ON_SITE' &&
          <button type="button" onClick={() => save('/channel', { channel: 'ONLINE' }, 'PUT')}>Ghi nhận khách chọn tư vấn trực tuyến</button>}
        {previousChoice === 'ON_SITE' && !vnpayBlocked &&
          !(current?.channel === 'ONLINE' && current.reviewStatus === 'PENDING') &&
          <button type="button" onClick={() => save('/site-visit/proposals', { channel: 'ONLINE' })}>Gửi báo cáo chuyển sang trực tuyến</button>}
        {!siteForm && (!current || current.reviewStatus === 'RETURNED') &&
          <button type="button" onClick={() => setForm({ ...form, adjustChannel: 'ON_SITE' })}>
            {requiresChangeReason ? 'Chuyển sang tư vấn tại vườn' : 'Khách đã chốt tại vườn · Lập báo cáo'}
          </button>}
      </>}
      {editable && tariff && <div>
        <strong>Cách tính phí để tư vấn cho khách</strong>
        <ul>
          <li>Đến {tariff.freeThroughKm} km · Miễn phí</li>
          <li>Trên {tariff.freeThroughKm} đến {tariff.firstTierThroughKm} km · {money(tariff.firstTierVnd)}</li>
          <li>Trên {tariff.firstTierThroughKm} đến {tariff.baseThroughKm} km · {money(tariff.baseVnd)}</li>
          <li>Trên {tariff.baseThroughKm} km · {money(tariff.baseVnd)} + {money(tariff.extraVndPerKm)} cho mỗi km vượt mốc</li>
        </ul>
      </div>}
      {siteForm && <form className="management-form" onSubmit={proposal}>
        {requiresChangeReason && <label>Lý do chuyển sang tại vườn<input required {...field('channelReason')} /></label>}
        {!visit.offices?.length && <p>Chưa có trụ sở xuất phát. Quản trị viên cần thêm trụ sở trước khi lập báo cáo.</p>}
        <label>Trụ sở xuất phát<select disabled={!visit.offices?.length} required {...field('officeCode')}><option value="">Chọn trụ sở</option>{visit.offices?.map(o => <option key={o.code} value={o.code}>{o.label}</option>)}</select></label>
        {selectedOffice?.mapsUrl && <p><a href={selectedOffice.mapsUrl} target="_blank" rel="noreferrer">Mở vị trí trụ sở trên Google Maps</a></p>}
        <label>Địa chỉ vườn<input required={requiresVnpay} maxLength={500} {...field('gardenAddress')} value={form.gardenAddress ?? current?.gardenAddress ?? farmLocation ?? ''} /></label>
        <label>Đường dẫn Google Maps của vườn<input required type="url" {...field('mapsUrl')} /></label>
        <label>Quãng đường một chiều (km)<input required type="number" min=".01" step=".01" {...field('distanceKm')} /></label>
        {preview.error && <p role="alert">{preview.error.message} <button type="button" onClick={preview.reload}>Thử lại</button></p>}
        {quote && <p role="status">{quote.status === 'PRICING_NOT_CONFIGURED' ? 'Chưa cấu hình biểu phí' : `Phí dự kiến ${money(quote.amountVnd)}`}</p>}
        {hasExtraDistance && <p>
          {money(tariff.baseVnd)} + ({quote.distanceKm.toLocaleString('vi-VN')} − {tariff.baseThroughKm}) km × {money(tariff.extraVndPerKm)}/km
        </p>}
        <label>Ngày giờ tư vấn tại vườn dự kiến (giờ Việt Nam)<input required type="datetime-local" {...field('proposedAt')} /></label>
        <button disabled={!quote || quote.status === 'PRICING_NOT_CONFIGURED'}>Gửi báo cáo và báo giá cho quản trị viên</button>
      </form>}
      {admin && current?.reviewStatus === 'PENDING' && <>
        <button type="button" onClick={() => save('/site-visit/review',{decision:'APPROVE'})}>{current.channel === 'ON_SITE' && current.amountVnd === 0 ? 'Duyệt và chốt lịch miễn phí' : 'Duyệt báo giá'}</button>
        <form onSubmit={e => { e.preventDefault(); save('/site-visit/review',{decision:'RETURN',reason:form.reason}) }}>
          <label>Lý do trả sửa<input required {...field('reason')} /></label><button>Trả sửa</button></form></>}
      {(admin || editable) && vnpay && <>
            <p>VNPAY: {vnpayLabels[vnpay.status] ?? 'Không xác định'} · {vnpay.txnRef}{vnpay.expiresAt && <> · Hết hạn {date(vnpay.expiresAt)}</>}</p>
            {admin && ['PENDING', 'UNKNOWN', 'RECONCILIATION_REQUIRED'].includes(vnpay.status) && <p>Đang xác minh giao dịch VNPAY. Chưa thể xác nhận thủ công. Nếu kết quả chưa rõ, Admin cần đối soát bằng QueryDr trước khi thu tiếp.</p>}
            {vnpay.checkoutUrl && <>
              <p>{admin ? 'Kỹ sư phụ trách có thể lấy link này trong hồ sơ để gửi cho khách.' : 'Sao chép link này và gửi cho khách để thanh toán phí tư vấn.'}</p>
              <label>Link VNPAY Sandbox<input readOnly value={vnpay.checkoutUrl} onFocus={event => event.currentTarget.select()} /></label>
              <button type="button" onClick={async () => {
                try { await navigator.clipboard.writeText(vnpay.checkoutUrl); setCopied(true) }
                catch { setError('Không thể sao chép tự động. Hãy sao chép link trong ô phía trên.') }
              }}>Sao chép link</button>{copied && <p role="status">Đã sao chép link.</p>}</>}
            <button type="button" onClick={refreshVisit}>Cập nhật trạng thái VNPAY</button>
          </>}
      {!admin && editable && awaitingPayment && !vnpay && <p role="status">Báo giá đã được duyệt. Đang chờ quản trị viên tạo link thanh toán.</p>}
      {admin && current?.reviewStatus === 'APPROVED' && current.channel === 'ON_SITE' && !current.scheduledAt &&
        (current.paymentStatus === 'UNPAID' ? <>
          <p>VNPAY Sandbox · Chỉ dùng thử nghiệm, không thanh toán bằng tiền thật.</p>
          {!visit.vnpayEnabled && <p id="vnpay-unavailable">Backend chưa sẵn sàng tạo link VNPay Sandbox. Nhờ người phụ trách backend kiểm tra cấu hình Sandbox và địa chỉ HTTPS quay về sau thanh toán.</p>}
          <div className="management-row-actions">
            <button type="button" className="app-ui-button app-ui-button--primary" disabled={!visit.vnpayEnabled || vnpayBlocked}
              aria-describedby={!visit.vnpayEnabled ? 'vnpay-unavailable' : undefined} onClick={createCheckout}>Tạo link VNPAY Sandbox</button>
            {!visit.vnpayEnabled && <button type="button" className="app-ui-button" onClick={refreshVisit}>Kiểm tra lại cấu hình</button>}
          </div>
          {!requiresVnpay && <form className="management-form" onSubmit={e => { e.preventDefault(); if (vnpayBlocked) return; save('/site-visit/payments/confirm',{
          paidAmountVnd:Number(form.paid), paidAt:new Date(form.paidAt).toISOString(), reference:form.reference, bills }) }}>
          <fieldset disabled={vnpayBlocked} className="management-form">
          <label>Số tiền thực nhận (VND)<input required type="number" min="1" step="1" {...field('paid')} /></label>
          <label>Thời điểm giao dịch<input required type="datetime-local" {...field('paidAt')} /></label>
          <label>Mã tham chiếu<input required {...field('reference')} /></label><EvidenceImages vietnamese images={bills} onChange={setBills} onBusyChange={setReading} />
          <button disabled={reading || !bills.length}>Xác nhận đã nhận tiền</button></fieldset></form>}</>
          : <button type="button" onClick={() => save('/site-visit/schedule',{})}>Chốt lịch và thông báo kỹ sư</button>)}
    </fieldset>
  </SectionCard>
}
