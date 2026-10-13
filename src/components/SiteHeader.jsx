import { Link } from 'react-router-dom'
import { useEffect, useState } from 'react'
import Icon from './Icon'
import { getCartCount, getCartItems, subscribeToCart } from '../utils/cart'

const links = [
  { label: 'Nền tảng', href: '#platform' },
  { label: 'Thiết bị', href: '#rental' },
  { label: 'Cách hoạt động', href: '#how-it-works' },
  { label: 'Về Smart Farm', href: '#about' },
]

export default function SiteHeader({ innerPage = false, heroPage = false }) {
  const [open, setOpen] = useState(false)
  const [cartCount, setCartCount] = useState(() => getCartCount())
  const homeLink = innerPage ? '/' : '#home'
  const linkHref = (href) => href.startsWith('#') && innerPage ? '/' + href : href

  useEffect(() => subscribeToCart(() => setCartCount(getCartCount(getCartItems()))), [])

  return (
    <header className={'site-header' + (innerPage && !heroPage ? ' site-header--inner' : '')}>
      <Link className="wordmark" to={homeLink} aria-label="Smart Farm trang chủ"><span className="wordmark-mark"><i /><i /><i /></span>Smart Farm</Link>
      <button className="mobile-toggle" aria-label="Mở menu" aria-expanded={open} onClick={() => setOpen(!open)}><Icon name={open ? 'close' : 'menu'} /></button>
      <nav className={'main-nav ' + (open ? 'nav-open' : '')}>{links.map((link) => <Link key={link.href} to={linkHref(link.href)} onClick={() => setOpen(false)}>{link.label}</Link>)}</nav>
      <div className="header-actions">
        <Link className="header-login" to="/dang-nhap">Đăng nhập</Link>
        <Link className="header-register" to="/dang-ky">Đăng ký</Link>
        <Link className="header-cart" to="/gio-hang" aria-label={'Giỏ hàng, ' + cartCount + ' sản phẩm'}><Icon name="cart" size={19} /><span>{cartCount}</span></Link>
      </div>
    </header>
  )
}
