import StatusBadge from './StatusBadge'
import '../../../styles/app-ui.css'

export default function DeviceStatus({ status, name, lastSeen, className = '', ...props }) {
  return <span {...props} className={`app-ui app-ui-device-status ${className}`}>
    {name && <strong>{name}</strong>}<StatusBadge status={status} />
    {lastSeen && <span className="app-ui-device-last-seen">{lastSeen}</span>}
  </span>
}
