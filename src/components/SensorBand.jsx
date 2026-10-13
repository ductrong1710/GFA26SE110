import Icon from './Icon'
import Reveal from './Reveal'

const readings = [
  { sensor: 'Độ ẩm đất', zone: 'Khu A · Luống 03', value: '34%', status: 'Ổn định', icon: 'sensor' },
  { sensor: 'Nhiệt độ', zone: 'Nhà lưới · Khu B', value: '29°C', status: 'Ổn định', icon: 'sun' },
  { sensor: 'Độ pH', zone: 'Khu C · Luống 01', value: '6.4', status: 'Trong ngưỡng', icon: 'droplet' },
]

function SensorTable() {
  return (
    <div className="sensor-table-card">
      <div className="sensor-table-head"><div><span className="eyebrow">BẢNG DỮ LIỆU MINH HỌA</span><h3>Trạng thái cảm biến</h3></div><span className="table-sync"><i /> Vừa cập nhật</span></div>
      <div className="sensor-table-wrap">
        <table className="sensor-table">
          <thead><tr><th>Chỉ số</th><th>Khu vực</th><th>Giá trị</th><th>Trạng thái</th></tr></thead>
          <tbody>{readings.map((row) => <tr key={row.sensor}><td><span className="table-sensor-name"><Icon name={row.icon} size={15} />{row.sensor}</span></td><td>{row.zone}</td><td className="table-value">{row.value}</td><td><span className="table-status">{row.status}</span></td></tr>)}</tbody>
        </table>
      </div>
      <div className="sensor-table-foot"><span>3 thiết bị đang hoạt động</span><span>Đồng bộ tự động <Icon name="arrow" size={13} /></span></div>
    </div>
  )
}

export default function SensorBand() {
  return (
    <section className="sensor-band" id="sensor-band" aria-labelledby="sensor-band-title">
      <div className="sensor-band-inner page-width">
        <Reveal className="sensor-band-copy"><span className="eyebrow">MỘT NỀN TẢNG · SÁU CHỈ SỐ</span><h2 id="sensor-band-title">Đọc tín hiệu từ đất.</h2><p>Theo dõi độ ẩm, nhiệt độ, ánh sáng và chất lượng đất theo từng khu vực — mọi thay đổi đều hiện rõ trong một bảng điều khiển.</p><a className="inline-link" href="#platform">Khám phá dữ liệu <Icon name="arrow" size={15} /></a></Reveal>
        <Reveal className="sensor-band-demo" delay={120}><SensorTable /></Reveal>
      </div>
    </section>
  )
}
