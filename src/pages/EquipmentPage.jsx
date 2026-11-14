import { useState } from 'react'
import SiteFooter from '../components/SiteFooter'
import SiteHeader from '../components/SiteHeader'
import PublicDeviceCard from '../components/PublicDeviceCard'
import { api } from '../services/api'
import { useApiQuery } from '../services/api/useApiQuery'

const pageSize = 12

export default function EquipmentPage() {
  const [page, setPage] = useState(1)
  const query = useApiQuery('all-public-devices', signal => api.request('/api/public-devices', { auth: false, signal }))
  const devices = query.data ?? []
  const totalPages = Math.ceil(devices.length / pageSize)
  const visibleDevices = devices.slice((page - 1) * pageSize, page * pageSize)
  return <div className="equipment-page">
    <SiteHeader innerPage />
    <main>
      <section className="equipment-page-intro page-width" aria-labelledby="equipment-page-title">
        <span className="eyebrow">THIẾT BỊ & BẢNG GIÁ</span>
        <h1 id="equipment-page-title">Thiết bị cho nông trại.</h1>
        <p>Xem toàn bộ thiết bị đang được công khai cùng mức giá thuê tham khảo.</p>
      </section>
      <section className="equipment-catalog equipment-page-catalog page-width" aria-label="Danh sách thiết bị">
        {query.loading && <p role="status">Đang tải thiết bị…</p>}
        {query.error && <p role="alert">Chưa tải được thiết bị. <button onClick={query.reload}>Thử lại</button></p>}
        {!query.loading && !query.error && !devices.length && <p>Chưa có thiết bị được công khai.</p>}
        <div className="equipment-grid">{visibleDevices.map(device => <PublicDeviceCard device={device} key={device.id} />)}</div>
        {totalPages > 1 && <nav className="equipment-pagination" aria-label="Trang thiết bị">
          <button disabled={page === 1} onClick={() => setPage(current => current - 1)}>Trang trước</button>
          <span>Trang {page} / {totalPages}</span>
          <button disabled={page === totalPages} onClick={() => setPage(current => current + 1)}>Trang sau</button>
        </nav>}
      </section>
    </main>
    <SiteFooter innerPage />
  </div>
}
