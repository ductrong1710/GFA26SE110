import { useState } from 'react'
import { api } from '../services/api'
import { PageHeader, SectionCard } from '../components/app/ui'
import '../styles/management.css'

export default function ConsultationPage() {
  const [values, setValues] = useState({ customerName: '', phone: '', email: '', farmLocation: '', farmArea: '', serviceNeed: '', content: '' })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [receipt, setReceipt] = useState(null)
  const field = name => ({ value: values[name], onChange: event => setValues({ ...values, [name]: event.target.value }) })
  async function submit(event) {
    event.preventDefault()
    if (pending) return
    setPending(true); setError('')
    try {
      setReceipt(await api.request('/api/consultations', { method: 'POST', auth: false, retry: false, toast: true, toastMessage: 'Consultation request submitted successfully.',
        body: { ...values, email: values.email.trim() || null, farmArea: values.farmArea === '' ? null : Number(values.farmArea) } }))
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  return <main className="app-ui" style={{ maxWidth: 800, margin: '3rem auto', padding: '1rem' }}>
    <a href="/">← Trang chủ</a>
    <PageHeader title="Yêu cầu tư vấn" description="Gửi nhu cầu của bạn để kỹ sư nông nghiệp liên hệ, khảo sát và đề xuất thiết bị phù hợp." />
    <SectionCard title="Thông tin tư vấn">
      {receipt ? <p role="status">Đã tiếp nhận yêu cầu #{receipt.id}. Kỹ sư sẽ liên hệ theo thông tin bạn cung cấp.</p> :
        <form aria-label="Yêu cầu tư vấn" className="management-form" onSubmit={submit}>
          <fieldset disabled={pending} className="management-form">
            <label>Họ tên<input required maxLength={150} autoComplete="name" {...field('customerName')} /></label>
            <label>Số điện thoại<input required type="tel" maxLength={30} autoComplete="tel" {...field('phone')} /></label>
            <label>Email (không bắt buộc)<input type="email" maxLength={255} autoComplete="email" {...field('email')} /></label>
            <label>Vị trí nông trại<input required maxLength={500} {...field('farmLocation')} /></label>
            <label>Diện tích (ha, không bắt buộc)<input type="number" min="0.0001" max="100000000" step="0.0001" {...field('farmArea')} /></label>
            <label>Nhu cầu dịch vụ<input required maxLength={500} {...field('serviceNeed')} /></label>
            <label>Nội dung tư vấn<textarea required maxLength={4000} rows={5} {...field('content')} /></label>
            <button type="submit" className="app-ui-button app-ui-button--primary">{pending ? 'Đang gửi…' : 'Gửi yêu cầu tư vấn'}</button>
          </fieldset>
          {error && <p role="alert" className="management-error">{error}</p>}
        </form>}
    </SectionCard>
  </main>
}
