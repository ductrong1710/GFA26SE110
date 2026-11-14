import '../../../styles/app-ui.css'
import { useLocation } from 'react-router-dom'
import { getCurrentNavigationItem, getNavigationForRole } from '../../../config/navigation'
import { useAuth } from '../../../context/AuthContext'

export default function PageHeader({ title, description, eyebrow, breadcrumbs, actions, className = '', ...props }) {
  const location = useLocation()
  const { activeRole, canAny } = useAuth()
  const currentPage = getCurrentNavigationItem(getNavigationForRole(activeRole, canAny), location.pathname)
  const hasTopbarTitle = currentPage?.label.trim().toLocaleLowerCase() === title?.trim().toLocaleLowerCase()
  return <header {...props} className={`app-ui app-ui-page-header ${className}`}>
    <div className="app-ui-page-heading">
      {breadcrumbs && <nav className="app-ui-breadcrumbs" aria-label="Breadcrumb">{breadcrumbs}</nav>}
      {eyebrow && <p className="app-ui-eyebrow">{eyebrow}</p>}
      {!hasTopbarTitle && <h1>{title}</h1>}
      {description && <p className="app-ui-description">{description}</p>}
    </div>
    {actions && <div className="app-ui-actions">{actions}</div>}
  </header>
}
