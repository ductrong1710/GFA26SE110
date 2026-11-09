import { useState } from 'react'
import { API_BASE_URL } from '../config/api'
import { api } from '../services/api'
import { useApiQuery } from '../services/api/useApiQuery'
import ConsultationButton from './ConsultationButton'

const labels = { SERVICE: 'Dịch vụ', DEVICE: 'Thiết bị', COMPONENT: 'Linh kiện', INSTALLATION: 'Lắp đặt', WARRANTY: 'Bảo hành',
  MAINTENANCE: 'Bảo trì', RETURN: 'Hoàn trả thiết bị', CONTACT: 'Liên hệ' }
const money = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 2 })

export default function PublicInformation() {
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useApiQuery(`public-content:${page}`, signal =>
    api.request('/api/public-content', { auth: false, query: { page }, signal }))
  return <section id="rental" className="equipment-catalog page-width" aria-labelledby="public-information-heading">
    <h2 id="public-information-heading">Thiết bị, dịch vụ và chính sách thuê</h2>
    {loading && <p role="status">Đang tải thông tin…</p>}
    {error && <p role="alert">Chưa tải được thông tin. <button onClick={reload}>Thử lại</button></p>}
    {data && !data.items.length && <p>Chưa có thông tin được công bố. Bạn có thể gửi yêu cầu tư vấn cho nông trại của mình.</p>}
    <div className="equipment-grid">{data?.items.map(entry => <article className="equipment-card" key={entry.id}>
      {entry.hasImage && <img loading="lazy" src={`${API_BASE_URL.replace(/\/$/, '')}/api/public-content/${entry.id}/image`} alt={entry.title} />}
      <div className="equipment-card-copy"><span>{labels[entry.kind]}</span><h3>{entry.title}</h3>
        <p style={{ whiteSpace: 'pre-wrap' }}>{entry.body}</p>
        {entry.estimatedMonthlyRentVnd != null && <p>Giá thuê tham khảo mỗi tháng: {money.format(entry.estimatedMonthlyRentVnd)}</p>}
        {entry.replacementPriceVnd != null && <p>Giá thay thế mỗi đơn vị: {money.format(entry.replacementPriceVnd)}</p>}
      </div></article>)}</div>
    {(page > 1 || data?.pagination.totalPages > 1) && <nav aria-label="Trang thông tin">
      <button disabled={loading || page === 1} onClick={() => setPage(page - 1)}>Trang trước</button> Trang {page} <button
        disabled={loading || page >= (data?.pagination.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Trang sau</button>
    </nav>}
    <ConsultationButton />
  </section>
}
