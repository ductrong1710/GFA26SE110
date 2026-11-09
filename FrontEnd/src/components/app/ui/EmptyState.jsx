import AppIcon from '../AppIcon'
import '../../../styles/app-ui.css'

export default function EmptyState({ title = 'No results', description, icon, actions, className = '', ...props }) {
  return <div {...props} className={`app-ui app-ui-empty-state ${className}`}>
    <span className="app-ui-empty-icon" aria-hidden="true">{icon ?? <AppIcon name="database" size={26} />}</span>
    <h3>{title}</h3>{description && <p className="app-ui-description">{description}</p>}
    {actions && <div className="app-ui-actions">{actions}</div>}
  </div>
}
