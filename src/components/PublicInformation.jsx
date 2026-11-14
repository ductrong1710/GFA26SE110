import { Link } from 'react-router-dom'
import { api } from '../services/api'
import { useApiQuery } from '../services/api/useApiQuery'
import ConsultationButton from './ConsultationButton'
import Icon from './Icon'
import PublicDeviceCard from './PublicDeviceCard'

export default function PublicInformation() {
  const { data, loading, error, reload } = useApiQuery('homepage-public-devices', signal =>
    api.request('/api/public-devices', { auth: false, signal }))
  return <>
  <section className="equipment-band">
    <div id="rental" className="equipment-catalog page-width" aria-labelledby="public-information-heading">
      <header className="catalog-heading">
        <h2 id="public-information-heading">Thiết bị cho nông trại</h2>
        <p>Khám phá một số thiết bị đang có và giá thuê tham khảo.</p>
      </header>
      {loading && <p role="status">Đang tải thiết bị…</p>}
      {error && <p role="alert">Chưa tải được thiết bị. <button onClick={reload}>Thử lại</button></p>}
      {data && !data.length && <p>Chưa có thiết bị được công khai.</p>}
      <div className="equipment-grid">{data?.slice(0, 4).map(device => <PublicDeviceCard device={device} key={device.id} />)}</div>
      <div className="all-devices-action"><Link className="all-devices-link" to="/thiet-bi">Xem toàn bộ thiết bị <Icon name="arrow" size={15} /></Link></div>
    </div>
  </section>
  <section className="equipment-consultation" aria-labelledby="equipment-consultation-heading">
    <div className="equipment-consultation-inner page-width">
      <div className="equipment-consultation-copy">
        <h2 id="equipment-consultation-heading">Chưa biết chọn thiết bị nào?</h2>
      </div>
      <ConsultationButton />
    </div>
  </section>
  </>
}
