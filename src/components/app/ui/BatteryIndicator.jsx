import { getBatteryState } from './uiState'
import { useUiText } from '../../../utils/useUiText'
import '../../../styles/app-ui.css'

export default function BatteryIndicator({ value, charging = false, label = 'Battery', showValue = true, className = '', ...props }) {
  const t = useUiText({
    'Battery': 'Pin',
    'Unknown battery level': 'Mức pin không xác định',
    'Unknown': 'Không xác định',
    'Critical battery': 'Pin ở mức nguy cấp',
    'Low battery': 'Pin yếu',
    'Battery level': 'Mức pin',
    'charging': 'đang sạc',
    'Charging': 'Đang sạc',
  })
  const { level, tone, label: batteryState } = getBatteryState(value)
  const state = t(batteryState)
  const text = level === null ? t('Unknown battery level') : `${Math.round(level)}%, ${state.toLowerCase()}`
  return <span {...props} className={`app-ui app-ui-battery ${className}`} data-tone={tone}
    role={level === null ? 'img' : 'meter'} aria-label={level === null ? `${t(label)}: ${text}` : t(label)}
    aria-valuemin={level === null ? undefined : 0} aria-valuemax={level === null ? undefined : 100}
    aria-valuenow={level ?? undefined} aria-valuetext={level === null ? undefined : `${text}${charging ? `, ${t('charging')}` : ''}`}>
    <span className="app-ui-battery-case" aria-hidden="true"><span style={{ width: `${level ?? 0}%` }} /></span>
    {showValue && <span aria-hidden="true">{level === null ? t('Unknown') : `${Math.round(level)}%`}</span>}
    {charging && <span className="app-ui-battery-charging" aria-hidden="true">{t('Charging')}</span>}
  </span>
}
