import { getBatteryState } from './uiState'
import '../../../styles/app-ui.css'

export default function BatteryIndicator({ value, charging = false, label = 'Battery', showValue = true, className = '', ...props }) {
  const { level, tone, label: state } = getBatteryState(value)
  const text = level === null ? 'Unknown battery level' : `${Math.round(level)}%, ${state.toLowerCase()}`
  return <span {...props} className={`app-ui app-ui-battery ${className}`} data-tone={tone}
    role={level === null ? 'img' : 'meter'} aria-label={level === null ? `${label}: ${text}` : label}
    aria-valuemin={level === null ? undefined : 0} aria-valuemax={level === null ? undefined : 100}
    aria-valuenow={level ?? undefined} aria-valuetext={level === null ? undefined : `${text}${charging ? ', charging' : ''}`}>
    <span className="app-ui-battery-case" aria-hidden="true"><span style={{ width: `${level ?? 0}%` }} /></span>
    {showValue && <span aria-hidden="true">{level === null ? 'Unknown' : `${Math.round(level)}%`}</span>}
    {charging && <span className="app-ui-battery-charging" aria-hidden="true">Charging</span>}
  </span>
}
