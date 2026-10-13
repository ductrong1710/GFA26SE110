import { getStatusMeta } from './uiState'
import '../../../styles/app-ui.css'

export default function StatusBadge({ status, label, className = '', ...props }) {
  const meta = getStatusMeta(status)
  return <span {...props} className={`app-ui app-ui-status-badge ${className}`} data-tone={meta.tone} data-status={meta.status}>
    <span className="app-ui-badge-dot" aria-hidden="true" />{label ?? meta.label}
  </span>
}
