import { useState } from 'react'
import Icon from '../components/Icon'
import ConsultationButton from '../components/ConsultationButton'
import Reveal from '../components/Reveal'
import SiteFooter from '../components/SiteFooter'
import SiteHeader from '../components/SiteHeader'
import products, { exampleBasePrice } from '../data/products'
import { addRentalToCart } from '../utils/cart'

const rentTerms = Array.from({ length: 12 }, (_, index) => {
  const month = index + 1
  const pricing = month <= 3 ? { rate: '8–12%', min: 0.08, max: 0.12 } : month <= 6 ? { rate: '6–8%', min: 0.06, max: 0.08 } : { rate: '4–6%', min: 0.04, max: 0.06 }
  return { duration: `${month} tháng`, month, ...pricing }
})

const formatPrice = (price) => new Intl.NumberFormat('vi-VN').format(price) + 'đ'

const policies = [
  { title: 'Cách thức mua hàng', text: 'Chọn sản phẩm cần mua và gửi yêu cầu tư vấn. Smart Farm sẽ xác nhận cấu hình, khả năng kết nối và báo giá trước khi bạn quyết định đặt hàng.' },
  { title: 'Cách thức giao hàng', text: 'Phương thức và thời gian giao hàng được xác nhận theo khu vực nhận hàng và tình trạng thiết bị tại thời điểm đặt mua.' },
  { title: 'Cách thức đặt cọc', text: 'Nếu đơn hàng cần đặt cọc, mức cọc và điều kiện áp dụng sẽ được trao đổi rõ trước khi xác nhận.' },
  { title: 'Cách thức thanh toán', text: 'Thông tin thanh toán và thời điểm thanh toán sẽ được cung cấp trong báo giá hoặc xác nhận đơn hàng.' },
  { title: 'Bảo hành', text: 'Thời hạn và điều kiện bảo hành tùy theo từng thiết bị. Smart Farm sẽ xác nhận chính sách áp dụng cùng thông tin sản phẩm trước khi mua.' },
  { title: 'Bảo quản sản phẩm', text: 'Vệ sinh và cất thiết bị theo hướng dẫn đi kèm. Bảo vệ đầu đo, cổng kết nối và bộ điều khiển khỏi va đập hoặc điều kiện vượt quá thông số của thiết bị.' },
  { title: 'Cách thức cho thuê', text: 'Gửi yêu cầu về thiết bị, khu vực sử dụng và thời gian thuê dự kiến. Smart Farm sẽ tư vấn cấu hình, thời hạn, chi phí và điều kiện bàn giao trước khi xác nhận.' },
]

function ProductPolicies() {
  const [openIndex, setOpenIndex] = useState(-1)
  return (
    <div className="product-policy-list">{policies.map((policy, index) => <section className={'product-policy' + (openIndex === index ? ' is-open' : '')} key={policy.title}><h6><button type="button" aria-expanded={openIndex === index} onClick={() => setOpenIndex(openIndex === index ? -1 : index)}><span>{policy.title}</span><i aria-hidden="true">{openIndex === index ? '−' : '+'}</i></button></h6>{openIndex === index && <p>{policy.text}</p>}</section>)}</div>
  )
}

function RentalPricingDialog({ product, onClose }) {
  const [selectedDuration, setSelectedDuration] = useState(rentTerms[0].duration)
  const [quantity, setQuantity] = useState(1)
  const [added, setAdded] = useState(false)
  const selectedTerm = rentTerms.find((term) => term.duration === selectedDuration)
  const addSelectedRental = () => {
    addRentalToCart({
      productId: product.id,
      productName: product.name,
      productImage: product.image,
      quantity,
      duration: selectedTerm.duration,
      rate: selectedTerm.rate,
      monthlyMin: exampleBasePrice * selectedTerm.min,
    })
    setAdded(true)
  }
  return (
    <div className="rental-pricing-backdrop" onClick={onClose}>
      <section className="rental-pricing-dialog" role="dialog" aria-modal="true" aria-labelledby="rental-pricing-title" onClick={(event) => event.stopPropagation()}>
        <button className="rental-pricing-close" type="button" aria-label="Đóng bảng giá thuê" onClick={onClose}>×</button>
        <span className="eyebrow">GIÁ THUÊ THAM KHẢO</span>
        <h2 id="rental-pricing-title">Chọn thời hạn thuê</h2>
        <div className="rental-pricing-table-wrap"><table className="rental-pricing-table"><thead><tr><th>Chọn</th><th>Thời hạn thuê</th><th>Tỷ lệ / tháng</th><th>Giá thuê / tháng</th></tr></thead><tbody>{rentTerms.map((term) => <tr key={term.duration} className={selectedDuration === term.duration ? 'selected' : ''}><td><input type="radio" name="rental-duration" aria-label={'Chọn thuê ' + term.duration} checked={selectedDuration === term.duration} onChange={() => { setSelectedDuration(term.duration); setAdded(false) }} /></td><td>{term.duration}</td><td>{term.rate}</td><td>{formatPrice(exampleBasePrice * term.min)}</td></tr>)}</tbody></table></div>
        <div className="rental-quantity-select"><span>Số lượng thiết bị</span><div><button type="button" aria-label="Giảm số lượng" disabled={quantity <= 1} onClick={() => { setQuantity((value) => Math.max(1, value - 1)); setAdded(false) }}>−</button><output>{quantity}</output><button type="button" aria-label="Tăng số lượng" onClick={() => { setQuantity((value) => value + 1); setAdded(false) }}>+</button></div></div>
        <button className="rental-add-to-cart" type="button" onClick={addSelectedRental}>{added ? 'Đã thêm vào giỏ hàng' : 'Thêm vào giỏ hàng'}</button>
        {added && <a className="rental-view-cart" href="/gio-hang">Xem giỏ hàng</a>}
      </section>
    </div>
  )
}

export default function ProductDetailPage({ productId }) {
  const [showRentalPricing, setShowRentalPricing] = useState(false)
  const product = products.find((item) => item.id === productId)
  if (!product) return <><SiteHeader innerPage /><main className="product-not-found"><span>Không tìm thấy sản phẩm</span><a href="/thiet-bi">Quay lại danh mục thiết bị</a></main><SiteFooter innerPage /></>

  return (
    <>
      <SiteHeader innerPage />
      <main className="product-detail-page">
        <div className="product-breadcrumb page-width"><a href="/">Trang chủ</a><Icon name="chevron" size={13} /><a href="/thiet-bi">Thiết bị</a><Icon name="chevron" size={13} /><span>{product.name}</span></div>
        <section className="product-overview page-width"><Reveal className="product-detail-image"><img src={product.image} alt={product.name} /><span>{product.tag}</span></Reveal><Reveal className="product-detail-copy" delay={100}><span className="eyebrow">SMART FARM · THIẾT BỊ</span><h1>{product.name}</h1><p>{product.description}</p><div className="product-detail-specs">{[...product.specs, '100% sản phẩm chính hãng', 'Bao test 1 tuần', 'Freeship nội thành TP.HCM (dưới 15km) hoặc hóa đơn trên 1.000.000 đồng'].map((spec) => <span key={spec}><i />{spec}</span>)}</div><div className="product-prices"><span>GIÁ MUA MẪU</span><strong>{formatPrice(exampleBasePrice)}</strong><small>Đã gồm cấu hình & lắp đặt · Chuyển khoản / theo hợp đồng · Không hỗ trợ COD</small></div><div className="product-detail-actions"><a className="product-buy-now" href="#product-inquiry">Mua ngay</a><button className="product-rent-now" type="button" onClick={() => setShowRentalPricing(true)}>Thuê chỉ từ {formatPrice(exampleBasePrice * .03)}/tháng</button></div></Reveal></section>
        {showRentalPricing && <RentalPricingDialog product={product} onClose={() => setShowRentalPricing(false)} />}
        <section className="product-policy-section page-width"><Reveal className="product-policy-heading"><span className="eyebrow">THÔNG TIN SẢN PHẨM</span><h2>Hướng dẫn mua,<br /><span>thuê và sử dụng.</span></h2></Reveal><Reveal className="product-policy-content"><ProductPolicies /></Reveal></section>
        <section className="equipment-contact product-inquiry" id="product-inquiry"><div className="page-width equipment-contact-inner"><div><span className="eyebrow">CẦN TƯ VẤN THÊM?</span><h2>Hỏi về {product.name.toLowerCase()}.</h2></div><ConsultationButton /></div></section>
      </main>
      <SiteFooter innerPage />
    </>
  )
}
