import { Link } from 'react-router-dom'
import { useEffect, useState } from 'react'
import SiteFooter from '../components/SiteFooter'
import SiteHeader from '../components/SiteHeader'
import { getCartItems, removeCartItem, subscribeToCart, updateCartQuantity } from '../utils/cart'
import products from '../data/products'

const money = (value) => new Intl.NumberFormat('vi-VN').format(Math.round(value)) + 'đ'

export default function CartPage() {
  const [items, setItems] = useState(() => getCartItems())
  useEffect(() => subscribeToCart(() => setItems(getCartItems())), [])

  return <><SiteHeader innerPage /><main className="cart-page page-width">
    <span className="eyebrow">SMART FARM · ĐƠN THUÊ</span>
    <h1>Giỏ hàng</h1>
    {items.length === 0 ? <section className="cart-empty"><p>Giỏ hàng của bạn đang trống.</p><Link to="/thiet-bi">Khám phá thiết bị</Link></section> : <>
      <div className="cart-layout"><div className="cart-items"><div className="cart-column-head"><span>Sản phẩm thuê</span><span>Đơn giá / tháng</span><span>Số lượng</span><span>Số tiền / tháng</span><span>Thao tác</span></div>{items.map((item) => { const product = products.find((entry) => entry.id === item.productId); return <article className="cart-item" key={item.key}>
        <div className="cart-product"><img src={item.productImage || product?.image} alt={item.productName} /><div><span className="eyebrow">THUÊ THIẾT BỊ</span><h2>{item.productName}</h2><p>Thời hạn thuê: <strong>{item.duration}</strong></p><button className="cart-remove-mobile" type="button" onClick={() => removeCartItem(item.key)}>Xóa</button></div></div>
        <strong className="cart-price">{money(item.monthlyMin)}<small> / tháng</small></strong>
        <div className="cart-quantity"><button type="button" aria-label="Giảm số lượng" disabled={item.quantity <= 1} onClick={() => updateCartQuantity(item.key, item.quantity - 1)}>−</button><output>{item.quantity}</output><button type="button" aria-label="Tăng số lượng" onClick={() => updateCartQuantity(item.key, item.quantity + 1)}>+</button></div>
        <strong className="cart-total">{money(item.monthlyMin * item.quantity)}<small> / tháng</small></strong>
        <button className="cart-remove" type="button" onClick={() => removeCartItem(item.key)}>Xóa</button>
      </article>})}</div></div>
      <aside className="cart-summary"><div className="cart-summary-count"><span>Số lượng thiết bị</span><strong>{items.reduce((total, item) => total + item.quantity, 0)}</strong></div><div className="cart-summary-price"><span>Tổng cộng ({items.reduce((total, item) => total + item.quantity, 0)} sản phẩm)</span><strong>{money(items.reduce((total, item) => total + item.monthlyMin * item.quantity, 0))}<small> / tháng</small></strong></div><Link className="cart-contact" to="/thiet-bi#contact">Tiếp tục tư vấn thuê</Link></aside>
    </>}
  </main><SiteFooter innerPage /></>
}
