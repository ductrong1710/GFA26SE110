import '../../../styles/app-ui.css'

export default function PageHeader({ title, description, eyebrow, breadcrumbs, actions, className = '', ...props }) {
  return <header {...props} className={`app-ui app-ui-page-header ${className}`}>
    <div className="app-ui-page-heading">
      {breadcrumbs && <nav className="app-ui-breadcrumbs" aria-label="Breadcrumb">{breadcrumbs}</nav>}
      {eyebrow && <p className="app-ui-eyebrow">{eyebrow}</p>}
      <h1>{title}</h1>
      {description && <p className="app-ui-description">{description}</p>}
    </div>
    {actions && <div className="app-ui-actions">{actions}</div>}
  </header>
}
