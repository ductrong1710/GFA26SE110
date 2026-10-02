import Icon from './Icon'
import Reveal from './Reveal'

const categories = ['Cảm biến đất & pH', 'Độ ẩm không khí & ánh sáng', 'UAV thu thập dữ liệu']
const sensorProducts = [
  { name: 'Độ ẩm đất', type: 'CẢM BIẾN ĐẤT', image: '/images/sensors/soil-moisture.svg' },
  { name: 'Độ pH đất', type: 'CHẤT LƯỢNG ĐẤT', image: '/images/sensors/soil-ph.svg' },
  { name: 'Ánh sáng', type: 'MÔI TRƯỜNG', image: '/images/sensors/air-light.svg' },
]

export default function RentalSection() {
  return (
    <section className="rental-section" id="rental">
      <div className="rental-inner page-width">
        <Reveal className="rental-copy">
          <span className="eyebrow">THUÊ THIẾT BỊ THEO MÙA VỤ</span>
          <h2>Bắt đầu với đúng<br /><span>bộ thiết bị cần thiết.</span></h2>
          <p>Thuê cảm biến và UAV theo nhu cầu canh tác, không cần đầu tư toàn bộ thiết bị ngay từ đầu. Smart Farm hỗ trợ chọn cấu hình phù hợp với khu vực và thời gian sử dụng.</p>
          <ul>{categories.map((category) => <li key={category}><i />{category}</li>)}</ul>
          <a className="rental-cta" href="/thiet-bi">Xem thiết bị cho thuê <Icon name="arrow" size={15} /></a>
        </Reveal>
        <Reveal className="rental-products" delay={120}>
          <div className="rental-products-heading"><span>THIẾT BỊ CẢM BIẾN</span><span>01 — 03</span></div>
          <div className="rental-products-grid">{sensorProducts.map((product) => <a className="rental-product-card" href="/thiet-bi" key={product.name}><div className="rental-product-image"><img src={product.image} alt={product.name} loading="lazy" /><span>CHO THUÊ</span></div><div className="rental-product-copy"><span>{product.type}</span><strong>{product.name}</strong><i><Icon name="arrow" size={13} /></i></div></a>)}</div>
          <div className="rental-products-foot"><span>Cảm biến phù hợp với từng khu vực canh tác</span><Icon name="sensor" size={16} /></div>
        </Reveal>
      </div>
    </section>
  )
}
