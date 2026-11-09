import Icon from './Icon'
import Reveal from './Reveal'

const resources = [
  { title: 'Bản đồ nông trại', text: 'Xem vùng trồng, vị trí cảm biến và trạng thái thiết bị theo không gian.', image: 'https://images.unsplash.com/photo-1500382017468-9049fed747ef?auto=format&fit=crop&w=900&q=80', tag: 'KHÔNG GIAN' },
  { title: 'Nhiệm vụ thu thập', text: 'Theo dõi waypoint hoàn tất, điểm lỗi và ghi chú vận hành.', image: 'https://images.unsplash.com/photo-1473968512647-3e447244af8f?auto=format&fit=crop&w=900&q=80', tag: 'UAV & GATEWAY' },
  { title: 'Lịch sử & cảnh báo', text: 'Tìm lại dữ liệu đã đồng bộ và những thay đổi cần được chú ý.', image: 'https://images.unsplash.com/photo-1464226184884-fa280b87c399?auto=format&fit=crop&w=900&q=80', tag: 'DỮ LIỆU' },
]

export default function ResourceSection() {
  return (
    <section className="resource-section page-width" id="about">
      <Reveal className="resource-heading"><div><span className="eyebrow">TỪ QUAN SÁT ĐẾN HÀNH ĐỘNG</span><h2>Giữ mọi thông tin<br /><span>trong tầm mắt.</span></h2></div><a className="inline-link" href="#contact">Khám phá Smart Farm <Icon name="arrow" size={15} /></a></Reveal>
      <div className="resource-grid">{resources.map((item, index) => <Reveal key={item.title} delay={index * 100}><a className="resource-card" href="#contact"><img src={item.image} alt="" loading="lazy" /><div className="resource-card-copy"><span className="eyebrow">{item.tag}</span><h3>{item.title}</h3><p>{item.text}</p><span className="resource-arrow"><Icon name="arrow" size={16} /></span></div></a></Reveal>)}</div>
    </section>
  )
}
