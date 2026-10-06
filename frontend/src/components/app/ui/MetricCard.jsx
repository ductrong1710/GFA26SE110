import StatCard from './StatCard'
import StatusBadge from './StatusBadge'

export default function MetricCard({ status, lastUpdated, children, className = '', ...props }) {
  return <StatCard {...props} className={`app-ui-metric-card ${className}`} footer={
    (status || lastUpdated || children) && <>
      {status && <StatusBadge status={status} />}
      {lastUpdated && <span className="app-ui-metric-updated">{lastUpdated}</span>}
      {children}
    </>
  } />
}
