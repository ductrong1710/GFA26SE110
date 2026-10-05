import { Link, NavLink } from 'react-router-dom'
import { getNavigationForRole } from '../../config/navigation'
import { useAuth } from '../../context/AuthContext'
import AppIcon from './AppIcon'

export default function AppSidebar({ sidebarRef, mobile, open, onClose }) {
  const { user, activeRole } = useAuth()
  const navigation = getNavigationForRole(activeRole)
  const groups = [...new Set(navigation.map(({ group }) => group))]

  return <aside ref={sidebarRef} id="app-sidebar" className={`app-sidebar${open ? ' is-open' : ''}`}
    inert={mobile && !open} role={mobile && open ? 'dialog' : undefined}
    aria-modal={mobile && open ? true : undefined} aria-label="Workspace navigation">
    <div className="app-sidebar-brand">
      <Link to="/app/dashboard" className="app-brand" onClick={onClose} aria-label="Smart Farm dashboard">
        <span className="app-brand-mark" aria-hidden="true"><i /><i /><i /></span>
        <span>Smart Farm<small>FIELD NETWORK</small></span>
      </Link>
      <button type="button" className="app-icon-button app-sidebar-close" onClick={onClose} aria-label="Close navigation"><AppIcon name="close" /></button>
    </div>
    <nav className="app-sidebar-nav" aria-label="Main navigation">
      {groups.map((group) => <div className="app-nav-group" key={group}>
        <p className="app-nav-label">{group}</p>
        <ul>{navigation.filter((item) => item.group === group).map(({ path, label, icon }) => <li key={path}>
          <NavLink to={path} end={path === '/app/dashboard'} onClick={onClose}
            className={({ isActive }) => `app-nav-link${isActive ? ' is-active' : ''}`}>
            <AppIcon name={icon} /><span>{label}</span>
          </NavLink>
        </li>)}</ul>
      </div>)}
    </nav>
    <div className="app-sidebar-footer">
      <div className="app-workspace-label"><span className="app-status-dot" /><span>{user.isMock ? 'Demo workspace' : 'Farm workspace'}</span></div>
      <Link to="/" className="app-public-link">Back to website <AppIcon name="arrow" size={16} /></Link>
    </div>
  </aside>
}
