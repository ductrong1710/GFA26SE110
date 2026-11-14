import { API_BASE_URL } from '../config/api'
import { formatVnd } from '../utils/display'

export default function PublicDeviceCard({ device }) {
  return <article className="equipment-card">
    {device.hasImage && <div className="equipment-image">
      <img loading="lazy" src={`${API_BASE_URL.replace(/\/$/, '')}/api/public-devices/${device.id}/image`} alt={device.title} />
    </div>}
    <div className="equipment-card-copy">
      <span>Thiết bị cho thuê</span>
      <h2>{device.title}</h2>
      <p style={{ whiteSpace: 'pre-wrap' }}>{device.body}</p>
      {device.estimatedMonthlyRentVnd != null && <p className="equipment-price">{formatVnd(device.estimatedMonthlyRentVnd)} / tháng</p>}
    </div>
  </article>
}
