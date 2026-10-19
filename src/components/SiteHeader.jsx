import { Link } from 'react-router-dom'
import { useState } from 'react'
import Icon from './Icon'

const links = [
  { label: 'Nền tảng', href: '#platform' },
  { label: 'Cách hoạt động', href: '#how-it-works' },
  { label: 'Về Smart Farm', href: '#about' },
]

export default function SiteHeader({ innerPage = false, heroPage = false }) {
  const [open, setOpen] = useState(false)
  const homeLink = innerPage ? '/' : '#home'
  const linkHref = (href) => href.startsWith('#') && innerPage ? '/' + href : href

  return (
    <header className={'site-header' + (innerPage && !heroPage ? ' site-header--inner' : '')}>
      <Link className="wordmark" to={homeLink} aria-label="Smart Farm trang chủ"><span className="wordmark-mark"><i /><i /><i /></span>Smart Farm</Link>
      <button className="mobile-toggle" aria-label="Mở menu" aria-expanded={open} onClick={() => setOpen(!open)}><Icon name={open ? 'close' : 'menu'} /></button>
      <nav className={'main-nav ' + (open ? 'nav-open' : '')}>{links.map((link) => <Link key={link.href} to={linkHref(link.href)} onClick={() => setOpen(false)}>{link.label}</Link>)}</nav>
      <div className="header-actions">
        <Link className="header-login" to="/dang-nhap">Đăng nhập</Link>
        <Link className="header-register" to="/dang-ky">Đăng ký</Link>
      </div>
    </header>
  )
}
