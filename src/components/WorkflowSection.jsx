import Icon from './Icon'
import Reveal from './Reveal'

const missions = [
  { id: 'SF-024', area: 'Khu A · tuyến 01', progress: '12 / 12', state: 'Hoàn tất' },
  { id: 'SF-031', area: 'Nhà lưới · tuyến 02', progress: '08 / 10', state: 'Đang chạy' },
  { id: 'SF-008', area: 'Khu C · tuyến 03', progress: 'Chờ lịch', state: 'Sẵn sàng' },
]

function MissionTable() {
  return (
    <div className="mission-board">
      <div className="mission-board-top"><div><span className="eyebrow">ĐIỀU PHỐI THU THẬP</span><h3>Nhiệm vụ hôm nay</h3></div><button type="button" aria-label="Thêm nhiệm vụ"><Icon name="plus" size={17} /></button></div>
      <div className="mission-table-wrap"><table className="mission-table"><thead><tr><th>Mã nhiệm vụ</th><th>Khu vực</th><th>Waypoint</th><th>Trạng thái</th></tr></thead><tbody>{missions.map((mission) => <tr key={mission.id}><td className="mission-id">{mission.id}</td><td>{mission.area}</td><td>{mission.progress}</td><td><span className={`mission-state ${mission.state === 'Đang chạy' ? 'is-running' : ''}`}><i />{mission.state}</span></td></tr>)}</tbody></table></div>
      <div className="mission-board-foot"><span><i /> Gateway UAV kết nối</span><span>03 nhiệm vụ</span></div>
    </div>
  )
}

const steps = [
  { n: '01', title: 'Chọn khu vực', text: 'Lập nhiệm vụ cho vùng trồng và các cảm biến cần ghé thăm.' },
  { n: '02', title: 'Thu thập dữ liệu', text: 'Gateway UAV tìm thiết bị đã đăng ký tại từng waypoint.' },
  { n: '03', title: 'Theo dõi & đồng bộ', text: 'Xem kết quả, cảnh báo và trạng thái đồng bộ trên bảng điều khiển.' },
]

export default function WorkflowSection() {
  return (
    <section className="workflow-section" id="how-it-works">
      <div className="workflow-split page-width">
        <Reveal className="workflow-demo"><MissionTable /></Reveal>
        <div className="workflow-content"><Reveal className="workflow-heading"><span className="eyebrow">TỪ CÁNH ĐỒNG ĐẾN BẢNG ĐIỀU KHIỂN</span><h2>Một quy trình<br /><span>liền mạch.</span></h2><p>Từng nhiệm vụ được ghi nhận xuyên suốt, giúp đội ngũ nắm tiến độ và xử lý kịp thời.</p></Reveal><div className="workflow-steps">{steps.map((step, index) => <Reveal key={step.n} delay={index * 100}><article className="workflow-step"><span>{step.n}</span><div><h3>{step.title}</h3><p>{step.text}</p></div><Icon name="arrow" size={15} /></article></Reveal>)}</div></div>
      </div>
    </section>
  )
}
