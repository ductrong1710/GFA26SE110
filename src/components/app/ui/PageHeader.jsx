import '../../../styles/app-ui.css'
import { useUiText } from '../../../utils/useUiText'
import { useContext, useLayoutEffect } from 'react'
import { PageHeaderContext } from '../../../context/PageHeaderContext'

export default function PageHeader({ title, description, eyebrow, breadcrumbs, actions, className = '', ...props }) {
  const t = useUiText()
  const setPageHeader = useContext(PageHeaderContext)
  useLayoutEffect(() => {
    if (!setPageHeader) return
    setPageHeader({ title })
    return () => setPageHeader(null)
  }, [setPageHeader, title])
  if (setPageHeader) return breadcrumbs || actions ? <div {...props} className={`app-ui app-page-tools ${className}`}>
    {breadcrumbs && <nav className="app-ui-breadcrumbs" aria-label={t('Breadcrumb')}>{breadcrumbs}</nav>}
    {actions && <div className="app-ui-actions">{actions}</div>}
  </div> : null
  return <header {...props} className={`app-ui app-ui-page-header ${className}`}>
    <div className="app-ui-page-heading">
      {breadcrumbs && <nav className="app-ui-breadcrumbs" aria-label={t('Breadcrumb')}>{breadcrumbs}</nav>}
      {eyebrow && <p className="app-ui-eyebrow">{eyebrow}</p>}
      <h1>{title}</h1>
      {description && <p className="app-ui-description">{description}</p>}
    </div>
    {actions && <div className="app-ui-actions">{actions}</div>}
  </header>
}
