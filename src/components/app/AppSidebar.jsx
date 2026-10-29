import { Link, NavLink } from 'react-router-dom'
import { getNavigationForRole } from '../../config/navigation'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import AppIcon from './AppIcon'

export default function AppSidebar({ sidebarRef, mobile, open, onClose }) {
  const { activeRole, canAny } = useAuth()
  const isOwner = activeRole === ROLES.FARM_OWNER
  const navigation = getNavigationForRole(activeRole, canAny)
  const groups = [...new Set(navigation.map(item => isOwner ? item.ownerGroup ?? item.group : item.group))]

  return <aside ref={sidebarRef} id="app-sidebar" className={`app-sidebar${open ? ' is-open' : ''}`}
    inert={mobile && !open} role={mobile && open ? 'dialog' : undefined}
    aria-modal={mobile && open ? true : undefined} aria-label={isOwner ? 'Điều hướng không gian làm việc' : 'Workspace navigation'}>
    <div className="app-sidebar-brand">
      <Link to="/app/dashboard" className="app-brand" onClick={onClose} aria-label={isOwner ? 'Trang tổng quan Smart Farm' : 'Smart Farm dashboard'}>
        <span className="app-brand-mark" aria-hidden="true"><i /><i /><i /></span>
        <span>Smart Farm<small>FIELD NETWORK</small></span>
      </Link>
      <button type="button" className="app-icon-button app-sidebar-close" onClick={onClose} aria-label={isOwner ? 'Đóng điều hướng' : 'Close navigation'}><AppIcon name="close" /></button>
    </div>
    <nav className="app-sidebar-nav" aria-label={isOwner ? 'Điều hướng chính' : 'Main navigation'}>
      {groups.map((group) => <div className="app-nav-group" key={group}>
        <p className="app-nav-label">{group}</p>
        <ul>{navigation.filter((item) => (isOwner ? item.ownerGroup ?? item.group : item.group) === group).map(({ path, label, icon, ownerLabel }) => <li key={path}>
          <NavLink to={path} end={path === '/app/dashboard'} onClick={onClose}
            className={({ isActive }) => `app-nav-link${isActive ? ' is-active' : ''}`}>
            <AppIcon name={icon} /><span>{isOwner ? ownerLabel : label}</span>
          </NavLink>
        </li>)}</ul>
      </div>)}
    </nav>
    <div className="app-sidebar-footer">
      <div className="app-workspace-label"><span className="app-status-dot" /><span>{isOwner ? 'Không gian nông trại' : 'Farm workspace'}</span></div>
      <Link to="/" className="app-public-link">{isOwner ? 'Về trang chủ' : 'Back to website'} <AppIcon name="arrow" size={16} /></Link>
    </div>
  </aside>
}
