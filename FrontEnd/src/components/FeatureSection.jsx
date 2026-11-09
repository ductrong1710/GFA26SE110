import { useState } from 'react'
import Icon from './Icon'
import Reveal from './Reveal'

const capabilities = [
  { key: 'sensors', label: 'Cảm biến', icon: 'sensor', eyebrow: 'TỪNG ĐIỂM ĐO, MỘT BỨC TRANH RÕ RÀNG', title: 'Biết điều gì đang diễn ra trên đồng ruộng.', text: 'Xem chỉ số môi trường hiện tại và lịch sử theo từng cảm biến, khu vực hoặc nông trại.', metric: '06', unit: 'nhóm chỉ số môi trường', chart: '12,46 26,38 39,43 52,25 66,31 79,18 94,26 108,16' },
  { key: 'missions', label: 'Nhiệm vụ UAV', icon: 'drone', eyebrow: 'MỘT GATEWAY DI ĐỘNG', title: 'Thu thập dữ liệu theo từng tuyến bay.', text: 'Lập danh sách điểm thu thập, theo dõi tiến độ waypoint và xem kết quả thành công hoặc lỗi.', metric: '01', unit: 'quy trình thu thập có thể truy vết', chart: '8,54 30,54 42,28 70,28 84,12 108,12' },
  { key: 'offline', label: 'Đồng bộ ngoại tuyến', icon: 'cloud', eyebrow: 'KHÔNG BỎ LẠI DỮ LIỆU', title: 'Lưu tại gateway, đồng bộ khi có mạng.', text: 'Bản ghi đã thu thập được giữ cục bộ trong thời gian mất kết nối và gửi lại khi mạng sẵn sàng.', metric: 'OFFLINE', unit: 'lưu tạm và thử đồng bộ lại', chart: '8,20 24,20 24,48 54,48 54,18 78,18 78,36 108,36' },
  { key: 'alerts', label: 'Cảnh báo', icon: 'alert', eyebrow: 'NHẬN BIẾT SỚM BIẾN ĐỘNG', title: 'Đưa dấu hiệu bất thường đến đúng người.', text: 'Thiết lập ngưỡng theo dõi, ghi nhận cảnh báo và lưu lại cách đội ngũ xử lý.', metric: '24/7', unit: 'theo dõi trạng thái thiết bị', chart: '8,46 24,46 36,44 48,12 60,47 76,46 90,24 108,24' },
]

function TelemetryPreview({ item }) {
  return (
    <div className="telemetry-preview" aria-label={`Minh họa ${item.label}`}>
      <div className="preview-toolbar"><span className="window-dots"><i /><i /><i /></span><span>SMART FARM / OVERVIEW</span><span className="preview-live"><i /> LIVE</span></div>
      <div className="preview-main"><div className="preview-title"><span>MEKONG FARM · ZONE A</span><Icon name={item.icon} size={19} /></div><div className="preview-metric"><strong>{item.metric}</strong><span>{item.unit}</span></div><svg className="preview-chart" viewBox="0 0 116 64" role="img" aria-label="Biểu đồ dữ liệu minh họa"><path d={`M${item.chart}`} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/><path d="M0 60H116" stroke="currentColor" opacity=".2" /></svg><div className="preview-legend"><span><i /> Dữ liệu cảm biến</span><span>CẬP NHẬT VỪA XONG</span></div></div>
    </div>
  )
}

export default function FeatureSection() {
  const [activeKey, setActiveKey] = useState(capabilities[0].key)
  const active = capabilities.find((item) => item.key === activeKey)
  return (
    <section className="feature-section page-width" id="platform">
      <Reveal className="section-heading">
        <span className="eyebrow">MỌI DỮ LIỆU Ở ĐÚNG NƠI</span>
        <h2>Một nền tảng đa góc nhìn</h2>
        <p>Từ chỉ số cảm biến đến trạng thái nhiệm vụ, đội ngũ có thể theo dõi toàn bộ quy trình trong một không gian.</p>
      </Reveal>
      <Reveal className="feature-tabs" delay={100} role="tablist" aria-label="Tính năng Smart Farm">
        {capabilities.map((item) => <button key={item.key} className={activeKey === item.key ? 'active' : ''} role="tab" aria-selected={activeKey === item.key} onClick={() => setActiveKey(item.key)}><Icon name={item.icon} size={17} />{item.label}</button>)}
      </Reveal>
      <div className="feature-panel" role="tabpanel" key={active.key}>
        <Reveal className="feature-copy">
          <span className="eyebrow">{active.eyebrow}</span>
          <h3>{active.title}</h3>
          <p>{active.text}</p>
          <a className="inline-link" href="#how-it-works">Tìm hiểu quy trình <Icon name="arrow" size={15} /></a>
        </Reveal>
        <Reveal className="feature-demo" delay={100}><TelemetryPreview item={active} /></Reveal>
      </div>
    </section>
  )
}
