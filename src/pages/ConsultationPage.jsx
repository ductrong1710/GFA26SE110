import { useState } from 'react'
import { api } from '../services/api'
import SiteFooter from '../components/SiteFooter'
import SiteHeader from '../components/SiteHeader'
import '../styles/app-ui.css'
import '../styles/management.css'

export default function ConsultationPage() {
  const [values, setValues] = useState({ customerName: '', phone: '', email: '', farmLocation: '', farmArea: '', serviceNeed: '', content: '', channel: '' })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [receipt, setReceipt] = useState(null)
  const field = (name, label) => ({
    name,
    value: values[name],
    onChange(event) {
      event.target.setCustomValidity('')
      setValues({ ...values, [name]: event.target.value })
    },
    onInvalid(event) {
      const input = event.target
      if (input.validity.valueMissing) input.setCustomValidity(`${input.tagName === 'SELECT' ? 'Chọn' : 'Nhập'} ${label}.`)
      else if (input.validity.typeMismatch || input.validity.patternMismatch) input.setCustomValidity(`Kiểm tra lại ${label}.`)
    },
  })
  async function submit(event) {
    event.preventDefault()
    if (pending) return
    setPending(true); setError('')
    try {
      setReceipt(await api.request('/api/consultations', { method: 'POST', auth: false, retry: false, toast: true, toastMessage: 'Đã gửi yêu cầu tư vấn.',
        body: { ...values, email: values.email.trim() || null, farmArea: values.farmArea === '' ? null : Number(values.farmArea) } }))
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  return <>
    <SiteHeader innerPage />
    <main className="app-ui consultation-page">
      <div className="consultation-page-inner">
        <h1>Yêu cầu tư vấn</h1>
        {receipt ? <p className="management-success consultation-success" role="status">Đã nhận yêu cầu #{receipt.id}. Kỹ sư sẽ liên hệ.</p> :
          <form aria-label="Yêu cầu tư vấn" className="management-form consultation-form" onSubmit={submit}>
            <fieldset disabled={pending} className="consultation-fields">
              <label>Hình thức tư vấn *<select required {...field('channel', 'hình thức tư vấn')}><option value="">Chọn</option><option value="ONLINE">Trực tuyến</option><option value="ON_SITE">Tại vườn</option></select></label>
              <label>Họ tên *<input required maxLength={150} autoComplete="name" {...field('customerName', 'họ tên')} /></label>
              <label>Số điện thoại *<input required type="tel" pattern="\+?(?:[ \(\)\-]*[0-9]){7,15}[ \(\)\-]*" maxLength={30} autoComplete="tel" {...field('phone', 'số điện thoại')} /></label>
              <label>Email (tùy chọn)<input type="email" maxLength={255} autoComplete="email" {...field('email', 'email')} /></label>
              <label>Vị trí nông trại *<input required maxLength={500} {...field('farmLocation', 'vị trí nông trại')} /></label>
              <label>Diện tích (ha) *<input type="number" min="0.0001" max="100000000" step="0.0001" {...field('farmArea', 'diện tích')} /></label>
              <label>Dịch vụ cần tư vấn *<input required maxLength={500} {...field('serviceNeed', 'dịch vụ cần tư vấn')} /></label>
              <label className="consultation-field-wide">Mô tả nhu cầu *<textarea required maxLength={4000} rows={5} {...field('content', 'mô tả nhu cầu')} /></label>
              <button type="submit" className="app-ui-button app-ui-button--primary">{pending ? 'Đang gửi…' : 'Gửi yêu cầu'}</button>
            </fieldset>
            {error && <p role="alert" className="management-error">{error}</p>}
          </form>}
      </div>
    </main>
    <SiteFooter innerPage />
  </>
}
