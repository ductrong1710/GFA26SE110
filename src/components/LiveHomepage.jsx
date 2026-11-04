import { useState } from 'react'
import { api } from '../services/api'
import { useApiQuery } from '../services/api/useApiQuery'
import { allPages, money } from '../services/api/workflow'
import FloatingContactDock from './FloatingContactDock'
import { policyKinds } from '../config/websiteContent'
import { API_BASE_URL } from '../config/api'
import { useAction } from './workflow/WorkflowUi'
import '../styles/public-service.css'

export function usePublicContent() {
  return useApiQuery('public-content', signal => allPages('/api/public-content', {}, signal, false), true, 5000)
}
export default function LiveHomepage() {
  const query = usePublicContent()
  const [sent, setSent] = useState(false)
  const action = useAction(() => setSent(true))
  const devices = query.data?.filter(row => row.kind === 'DEVICE') ?? []
  const policies = query.data?.filter(row => [...policyKinds, 'INSTALLATION'].includes(row.kind)) ?? []
  return <div className="public-service">
    <section id="thiet-bi-thue" className="public-service-section"><header><p className="eyebrow">THIẾT BỊ & BẢNG GIÁ</p><h2>Chọn thiết bị phù hợp với vườn.</h2><p>Giá công khai để đối chiếu. Đơn giá trong phương án đã được khách xác nhận sẽ được giữ nguyên.</p></header>
      {query.loading && <p role="status">Đang tải danh mục thiết bị…</p>}{query.error && <div role="alert"><p>Chưa tải được bảng giá. Bạn vẫn có thể gửi yêu cầu tư vấn bên dưới.</p><button onClick={query.reload}>Thử lại bảng giá</button></div>}
      {!query.loading && !query.error && !devices.length && <p>Danh mục đang được cập nhật. Hãy để lại nhu cầu để được tư vấn.</p>}
      <div className="public-product-grid">{devices.map(row => <article key={row.id}>{row.hasImage && <img loading="lazy" alt={row.title} src={`${API_BASE_URL}/api/public-content/${row.id}/image`} />}<div><small>{row.kind === 'DEVICE' ? 'Thiết bị cho thuê' : 'Linh kiện thay thế'}</small><h3>{row.title}</h3><p>{row.body}</p>{row.estimatedMonthlyRentVnd != null && <strong>{money(row.estimatedMonthlyRentVnd)}<small> / tháng</small></strong>}{row.replacementPriceVnd != null && <p>Giá thay thế: {money(row.replacementPriceVnd)} / đơn vị</p>}<a href="#gui-tu-van">Tư vấn sản phẩm này →</a></div></article>)}</div>
    </section>
    {policies.length > 0 && <section className="public-service-section" id="chinh-sach"><header><p className="eyebrow">ĐỒNG HÀNH TRONG SUỐT KỲ THUÊ</p><h2>Chính sách dịch vụ</h2></header><div className="public-policy-grid">{policies.map(row => <article key={row.id}><h3>{row.title}</h3><p>{row.body}</p></article>)}</div></section>}
    <section id="gui-tu-van" className="public-consultation"><div><p className="eyebrow">BẮT ĐẦU TỪ NHU CẦU CỦA BẠN</p><h2>Để lại thông tin.<br />Chúng tôi sẽ liên hệ.</h2><p>Không cần tài khoản. Kỹ sư trao đổi về nông trại và thiết bị phù hợp; kỹ thuật viên khảo sát vị trí thực tế trước khi lắp.</p><p className="public-consultation-note">Gửi form không phát sinh phí hay hợp đồng thuê.</p></div>
      {sent ? <div className="public-sent" role="status"><h3>Đã gửi yêu cầu tư vấn</h3><p>Thông tin của bạn đã được ghi nhận. Người trực sẽ liên hệ theo số điện thoại đã cung cấp.</p><button onClick={() => setSent(false)}>Gửi nhu cầu khác</button></div> : <form onSubmit={event => {
        event.preventDefault(); const values = Object.fromEntries(new FormData(event.currentTarget))
        action.run(() => api.request('/api/consultations', { method: 'POST', auth: false, toast: false, body: { ...values, email: values.email.trim(), farmArea: null } }))
      }}>
        {[['customerName', 'Họ và tên', 'text', 150], ['phone', 'Số điện thoại', 'tel', 30], ['email', 'Email (bắt buộc)', 'email', 255], ['farmLocation', 'Địa điểm nông trại', 'text', 500], ['serviceNeed', 'Bạn muốn giám sát điều gì?', 'text', 500]].map(([name, title, type, maxLength]) => <label key={name}>{title}<input name={name} type={type} required maxLength={maxLength} autoComplete={{ customerName: 'name', phone: 'tel', email: 'email' }[name]} /></label>)}
        <label>Nội dung tư vấn<textarea name="content" required maxLength={4000} rows={3} /></label>{action.errorView}<button disabled={action.busy} type="submit">{action.busy ? 'Đang gửi…' : 'Gửi yêu cầu tư vấn →'}</button>
      </form>}
    </section>
  </div>
}
export function QuickContacts() {
  const query = usePublicContent()
  return <FloatingContactDock contacts={query.data ?? []} loading={query.loading} error={query.error} onRetry={query.reload} />
}
