import NotificationsPanel from './NotificationsPanel'
import { useNotifications } from '../../services/api/useNotifications'
import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { getNavigationForRole } from '../../config/navigation'
import { useAuth } from '../../context/AuthContext'
import { ROLE_LABELS, ROLES } from '../../config/roles'
import AppIcon from './AppIcon'

export default function AppTopbar({ menuButtonRef, drawerOpen, onOpenMenu, farms, currentFarm, onFarmChange }) {
  const { user, logout, activeRole, activeRoleLabel, canAny } = useAuth()
  const isOwner = activeRole === ROLES.FARM_OWNER
  const notifications = useNotifications(user.id, activeRole)
  const navigate = useNavigate()
  const location = useLocation()
  const [query, setQuery] = useState('')
  const [popover, setPopover] = useState(null)
  const [lastLocationKey, setLastLocationKey] = useState(location.key)
  const [online, setOnline] = useState(() => navigator.onLine)
  const searchRef = useRef(null)
  const notificationRef = useRef(null)
  const accountRef = useRef(null)
  const results = getNavigationForRole(activeRole, canAny).filter(({ label, ownerLabel }) => (isOwner ? ownerLabel : label).toLowerCase().includes(query.trim().toLowerCase()))
  const initials = user.fullName.trim().split(/\s+/).slice(-2).map((name) => name[0]).join('').toUpperCase()

  if (lastLocationKey !== location.key) {
    setLastLocationKey(location.key)
    setPopover(null)
  }

  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update)
    window.addEventListener('offline', update)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update) }
  }, [])

  useEffect(() => {
    if (!popover) return
    const onPointerDown = (event) => {
      const trigger = popover === 'search' ? searchRef : popover === 'notifications' ? notificationRef : accountRef
      const container = trigger.current.closest('.app-search, .app-popover-anchor')
      if (!container.contains(event.target)) setPopover(null)
    }
    const onKeyDown = (event) => {
      if (event.key !== 'Escape') return
      setPopover(null)
      const trigger = popover === 'search' ? searchRef : popover === 'notifications' ? notificationRef : accountRef
      trigger.current?.focus()
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => { document.removeEventListener('pointerdown', onPointerDown); document.removeEventListener('keydown', onKeyDown) }
  }, [popover])

  const handleLogout = async () => { await logout(); navigate('/dang-nhap', { replace: true }) }
  const submitSearch = (event) => {
    event.preventDefault()
    if (query.trim() && results.length) { navigate(results[0].path); setPopover(null); searchRef.current.blur() }
  }

  return <header className="app-topbar">
    <div className="app-topbar-context">
      <button ref={menuButtonRef} type="button" className="app-icon-button app-menu-toggle" onClick={onOpenMenu}
        aria-label={isOwner ? 'Mở điều hướng' : 'Open navigation'} aria-controls="app-sidebar" aria-expanded={drawerOpen}><AppIcon name="menu" /></button>
      <div className="app-farm-control">
        <span className="app-farm-icon"><AppIcon name="map" /></span>
        <label className="app-farm-label"><span>{isOwner ? 'Nông trại hiện tại' : 'Current farm'}</span>
          <select aria-label={isOwner ? 'Nông trại hiện tại' : 'Current farm'} value={currentFarm.id} onChange={(event) => onFarmChange(Number(event.target.value))}>
            {farms.map((farm) => <option key={farm.id} value={farm.id}>{farm.name}</option>)}
          </select>
        </label>
      </div>
    </div>
    <form className="app-search" role="search" onSubmit={submitSearch}>
      <AppIcon name="search" size={18} />
      <input ref={searchRef} type="search" value={query} onChange={(event) => { setQuery(event.target.value); setPopover('search') }}
        onFocus={() => setPopover('search')} placeholder={isOwner ? 'Tìm trang…' : 'Search pages…'} aria-label={isOwner ? 'Tìm kiếm trang' : 'Search workspace pages'} autoComplete="off" />
      {popover === 'search' && query.trim() && <div className="app-popover app-search-results">
        <p className="app-popover-label">{isOwner ? 'Các trang' : 'Workspace pages'}</p>
        {results.length ? <ul>{results.map(({ path, label, icon, ownerLabel }) => <li key={path}>
          <Link to={path} onClick={() => setPopover(null)}><AppIcon name={icon} size={17} />{isOwner ? ownerLabel : label}</Link>
        </li>)}</ul> : <p className="app-empty-message" role="status">{isOwner ? 'Không tìm thấy trang phù hợp.' : 'No matching pages.'}</p>}
      </div>}
    </form>
    <div className="app-topbar-actions">
      <div className={`app-connection${online ? '' : ' is-offline'}`} role="status" aria-label={isOwner ? 'Trạng thái kết nối hệ thống' : 'System connection status'}>
        <span className="app-status-dot" />{!online ? (isOwner ? 'Ngoại tuyến' : 'Offline') : (isOwner ? 'Tài khoản backend' : 'Backend account')}
      </div>
      <div className="app-popover-anchor">
        <button ref={notificationRef} type="button" className="app-icon-button app-notification-trigger" aria-label={notifications.hasUnread ? (isOwner ? 'Thông báo, có thông báo chưa đọc' : 'Notifications, unread notifications') : (isOwner ? 'Thông báo' : 'Notifications')} aria-expanded={popover === 'notifications'}
          aria-controls="app-notifications" onClick={() => setPopover(popover === 'notifications' ? null : 'notifications')}><AppIcon name="alert" />{notifications.hasUnread && <span className="app-notification-dot" aria-hidden="true" />}</button>
        {popover === 'notifications' && <section id="app-notifications" className="app-popover app-notification-panel" aria-label={isOwner ? 'Thông báo' : 'Notifications'}>
          <NotificationsPanel notifications={notifications} isOwner={isOwner} />
        </section>}
      </div>
      <div className="app-popover-anchor app-account-anchor">
        <button ref={accountRef} type="button" className="app-account-button" aria-label={isOwner ? `Tài khoản: ${user.fullName}, Chủ trang trại` : `Account: ${user.fullName}, ${activeRoleLabel}`} aria-expanded={popover === 'account'} aria-controls="app-account-panel"
          onClick={() => setPopover(popover === 'account' ? null : 'account')}>
          <span className="app-avatar" aria-hidden="true">{initials}</span>
          <span className="app-user-copy"><strong>{user.fullName}</strong><small>{isOwner && activeRole === ROLES.FARM_OWNER ? 'Chủ trang trại' : activeRoleLabel}</small></span>
          <AppIcon name="down" size={15} />
        </button>
        {popover === 'account' && <section id="app-account-panel" className="app-popover app-account-panel" aria-label={isOwner ? 'Tài khoản của bạn' : 'Your account'}>
          <p className="app-popover-label">{isOwner ? 'Tài khoản của bạn' : 'Your account'}</p><strong>{user.fullName}</strong><p>{user.email}</p><p>{isOwner ? (activeRole === ROLES.FARM_OWNER ? 'Chủ trang trại' : ROLE_LABELS[activeRole] ?? activeRoleLabel) : activeRoleLabel}</p>
          <Link to="/app/profile" onClick={() => setPopover(null)}><AppIcon name="profile" size={17} />{isOwner ? 'Hồ sơ cá nhân' : 'Profile'}</Link>
          <button type="button" onClick={handleLogout}><AppIcon name="logout" size={17} />{isOwner ? 'Đăng xuất' : 'Sign out'}</button>
        </section>}
      </div>
    </div>
  </header>
}
