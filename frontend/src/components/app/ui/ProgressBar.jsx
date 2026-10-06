import { getProgress, getTone } from './uiState'
import '../../../styles/app-ui.css'

export default function ProgressBar({ value, max = 100, label = 'Progress', showValue = true, tone = 'primary', className = '', ...props }) {
  const progress = getProgress(value, max)
  const text = progress.percent === null ? 'Unavailable' : `${Math.round(progress.percent)}%`
  return <div {...props} className={`app-ui app-ui-progress ${className}`} data-tone={getTone(tone)}>
    <div className="app-ui-progress-label"><span>{label}</span>{showValue && <span>{text}</span>}</div>
    <div className="app-ui-progress-track" role="progressbar" aria-label={label} aria-valuemin={0}
      aria-valuemax={progress.max} aria-valuenow={progress.value ?? undefined} aria-valuetext={text}>
      <span className="app-ui-progress-fill" style={{ width: `${progress.percent ?? 0}%` }} />
    </div>
  </div>
}
