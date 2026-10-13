import { useId } from 'react'
import '../../../styles/app-ui.css'

export default function SectionCard({ title, description, actions, footer, children, className = '', ...props }) {
  const titleId = useId()
  return <section {...props} aria-labelledby={title ? titleId : props['aria-labelledby']} className={`app-ui app-ui-section-card ${className}`}>
    {(title || description || actions) && <header className="app-ui-card-header">
      <div>{title && <h2 id={titleId}>{title}</h2>}{description && <p className="app-ui-description">{description}</p>}</div>
      {actions && <div className="app-ui-actions">{actions}</div>}
    </header>}
    <div className="app-ui-card-body">{children}</div>
    {footer && <footer className="app-ui-card-footer">{footer}</footer>}
  </section>
}
