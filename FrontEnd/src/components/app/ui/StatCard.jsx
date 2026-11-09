import { getTone } from './uiState'
import '../../../styles/app-ui.css'

export default function StatCard({ label, value, unit, icon, description, trend, footer, className = '', ...props }) {
  const displayValue = typeof value === 'number' && !Number.isFinite(value) ? '—' : value ?? '—'
  return <article {...props} className={`app-ui app-ui-stat-card ${className}`}>
    <div className="app-ui-stat-heading"><p>{label}</p>{icon && <span className="app-ui-stat-icon" aria-hidden="true">{icon}</span>}</div>
    <p className="app-ui-stat-value">{displayValue}{unit && <span className="app-ui-stat-unit">{unit}</span>}</p>
    {description && <p className="app-ui-description">{description}</p>}
    {trend && <p className="app-ui-stat-trend" data-tone={getTone(trend.tone)}><strong>{trend.value}</strong>{trend.label && <span>{trend.label}</span>}</p>}
    {footer && <footer className="app-ui-stat-footer">{footer}</footer>}
  </article>
}
