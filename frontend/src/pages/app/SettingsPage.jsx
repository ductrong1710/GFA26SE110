import { useState } from 'react'
import { useManagement } from '../../context/ManagementContext'
import { sensorTypes } from '../../data/mock/sensorTypes'
import { MOCK_NOW } from '../../data/mock/scenario'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, SectionCard, StatusBadge } from '../../components/app/ui'
import '../../styles/management.css'

export default function SettingsPage() {
  const { management, changeManagement } = useManagement()
  const [draft, setDraft] = useState(() => structuredClone(management.settings))
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const change = (values) => { setDraft({ ...draft, ...values }); setNotice(''); setError('') }
  const threshold = (id, key, value) => change({ thresholds: draft.thresholds.map((item) => item.sensorTypeId === id ? { ...item, [key]: value } : item) })
  const save = (event) => {
    event.preventDefault()
    try { const next = changeManagement({ kind: 'settings', values: draft }); setDraft(structuredClone(next.settings)); setError(''); setNotice('Settings saved for this demo session.') }
    catch (err) { setError(err.message); setNotice('') }
  }
  return <div className="app-ui management-page">
    <PageHeader eyebrow="ADMINISTRATION" title="Settings" description="Monitoring defaults and notification preferences." actions={<StatusBadge status="INFO" label="Local demo" />} />
    <p className="management-note">Defaults are saved locally until refresh. They do not rewrite existing sensor configurations or historical alert thresholds. No notifications are sent.</p>
    <form onSubmit={save} className="settings-form">
      <SectionCard title="Sensor Threshold Defaults" description="Illustrative ranges for future sensor configuration">
        <div className="settings-thresholds">{draft.thresholds.map((item) => {
          const type = sensorTypes.find(({ id }) => id === item.sensorTypeId)
          return <div key={type.id}><strong>{type.name}<small>{type.unit}</small></strong><label>Minimum<input aria-label={`${type.name} minimum`} type="number" required step="any" min={type.minValue} max={type.maxValue} value={item.min} onChange={(event) => threshold(type.id, 'min', event.target.value)} /></label><label>Maximum<input aria-label={`${type.name} maximum`} type="number" required step="any" min={type.minValue} max={type.maxValue} value={item.max} onChange={(event) => threshold(type.id, 'max', event.target.value)} /></label></div>
        })}</div>
      </SectionCard>
      <div className="settings-grid">
        <SectionCard title="Data Timeout" description="Default period before a sensor is considered overdue"><label className="settings-field">Minutes without data<input type="number" min={1} max={1440} step={1} required value={draft.dataTimeoutMinutes} onChange={(event) => change({ dataTimeoutMinutes: event.target.value })} /></label></SectionCard>
        <SectionCard title="Low Battery Threshold" description="Highlight devices with battery at or below this level"><label className="settings-field">Battery percentage<input type="number" min={1} max={100} step={1} required value={draft.lowBatteryPercent} onChange={(event) => change({ lowBatteryPercent: event.target.value })} /></label></SectionCard>
        <SectionCard title="Web Notification" description="Preference for future in-app notifications"><label className="management-check"><input type="checkbox" checked={draft.webNotifications} onChange={(event) => change({ webNotifications: event.target.checked })} />Enable web notifications</label></SectionCard>
        <SectionCard title="Email Notification" description="Preference only; email delivery is not connected"><label className="management-check"><input type="checkbox" checked={draft.emailNotifications} onChange={(event) => change({ emailNotifications: event.target.checked })} />Enable email notifications</label></SectionCard>
      </div>
      <SectionCard title="System Information"><dl className="management-detail-grid"><div><dt>Environment</dt><dd>Frontend demo</dd></div><div><dt>Connection</dt><dd>No backend integration</dd></div><div><dt>Time zone</dt><dd>Asia/Ho_Chi_Minh (UTC+7)</dd></div><div><dt>Data snapshot</dt><dd>{displayTime(MOCK_NOW)}</dd></div><div><dt>Storage</dt><dd>In-memory; resets on refresh or sign-out</dd></div></dl></SectionCard>
      {error && <p className="management-error" role="alert">{error}</p>}{notice && <p className="management-success" role="status">{notice}</p>}
      <div className="settings-actions"><button type="button" className="app-ui-button" onClick={() => { setDraft(structuredClone(management.settings)); setError(''); setNotice('Unsaved changes discarded.') }}>Discard changes</button><button type="submit" className="app-ui-button app-ui-button--primary">Save Settings</button></div>
    </form>
  </div>
}
