import { Link } from 'react-router-dom'
import { useState } from 'react'
import Icon from '../components/Icon'
import ConsultationButton from '../components/ConsultationButton'
import products, { exampleBasePrice } from '../data/products'
import Reveal from '../components/Reveal'
import SiteFooter from '../components/SiteFooter'
import SiteHeader from '../components/SiteHeader'

const categories = [
  { id: 'all', label: 'Tất cả thiết bị' },
  { id: 'sensor', label: 'Cảm biến' },
  { id: 'uav', label: 'UAV & gateway' },
]

const formatPrice = (price) => new Intl.NumberFormat('vi-VN').format(price) + 'đ'

function ProductCard({ product }) {
  return (
    <Reveal><article className="equipment-card">
      <Link className="equipment-product-link" to={`/thiet-bi/${product.id}`} aria-label={`Xem chi tiết ${product.name}`}><div className="equipment-image"><img src={product.image} alt="" loading="lazy" /><span className="equipment-tag">{product.tag}</span><span className="equipment-icon"><Icon name={product.icon} size={20} /></span></div></Link>
      <div className="equipment-card-copy"><h2><Link className="equipment-product-title" to={'/thiet-bi/' + product.id}>{product.name}</Link></h2><p>{product.description}</p><ul>{product.specs.map((spec) => <li key={spec}><span />{spec}</li>)}</ul><div className="equipment-card-prices"><span>Giá mua mẫu<strong>{formatPrice(exampleBasePrice)}</strong></span><span>Thuê từ<strong>{formatPrice(exampleBasePrice * .03)}/tháng</strong></span></div><div className="equipment-actions"><Link className="equipment-buy" to={'/thiet-bi/' + product.id + '#product-inquiry'}>Mua ngay <Icon name="arrow" size={14} /></Link><Link className="equipment-rent" to={'/thiet-bi/' + product.id + '#product-inquiry'}>Thuê</Link></div></div>
    </article></Reveal>
  )
}

export default function EquipmentPage() {
  const [active, setActive] = useState('all')
  const visibleProducts = products.filter((product) => active === 'all' || product.category === active)
  return (
    <>
      <SiteHeader innerPage />
      <main className="equipment-page">
        <section className="equipment-catalog page-width" id="catalog"><Reveal className="catalog-heading"><h1>Cảm biến &amp; thiết bị bay.</h1><p>Thiết bị có thể mua hoặc thuê linh hoạt. Liên hệ để nhận tư vấn cấu hình và báo giá theo nhu cầu thực tế.</p></Reveal><Reveal className="catalog-filters" role="tablist" aria-label="Lọc danh mục thiết bị">{categories.map((category) => <button type="button" role="tab" aria-selected={active === category.id} className={active === category.id ? 'active' : ''} key={category.id} onClick={() => setActive(category.id)}>{category.label}</button>)}</Reveal><div className="equipment-grid">{visibleProducts.map((product) => <ProductCard key={product.id} product={product} />)}</div></section>
        <section className="equipment-contact"><div className="page-width equipment-contact-inner"><div><span className="eyebrow">CHƯA BIẾT BẮT ĐẦU TỪ ĐÂU?</span><h2>Cùng chọn bộ thiết bị<br />cho nông trại của bạn.</h2></div><ConsultationButton /></div></section>
      </main>
      <SiteFooter innerPage />
    </>
  )
}
