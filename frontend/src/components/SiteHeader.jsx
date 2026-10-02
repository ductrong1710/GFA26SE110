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
      <a className="wordmark" href={homeLink} aria-label="Smart Farm trang chủ"><span className="wordmark-mark"><i /><i /><i /></span>Smart Farm</a>
      <button className="mobile-toggle" aria-label="Mở menu" aria-expanded={open} onClick={() => setOpen(!open)}><Icon name={open ? 'close' : 'menu'} /></button>
      <nav className={'main-nav ' + (open ? 'nav-open' : '')}>{links.map((link) => <a key={link.href} href={linkHref(link.href)} onClick={() => setOpen(false)}>{link.label}</a>)}</nav>
      <div className="header-actions">
        <a className="header-login" href="/dang-nhap">Đăng nhập</a>
        <a className="header-register" href="/dang-ky">Đăng ký</a>
        <a className="header-cart" href="/gio-hang" aria-label={'Giỏ hàng, ' + cartCount + ' sản phẩm'}><Icon name="cart" size={19} /><span>{cartCount}</span></a>
      </div>
    </header>
  )
}
