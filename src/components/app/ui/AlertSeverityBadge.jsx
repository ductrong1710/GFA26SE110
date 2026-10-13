import StatusBadge from './StatusBadge'

export default function AlertSeverityBadge({ severity, className = '', ...props }) {
  return <StatusBadge {...props} status={severity} className={`app-ui-severity-badge ${className}`} />
}
