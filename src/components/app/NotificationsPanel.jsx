import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from '../../services/api/useApiQuery'
import { listAll } from '../../services/api/resources'
import { api } from '../../services/api'
import ApiState from './ApiState'
import { displayTime } from '../../utils/display'

export default function NotificationsPanel() {
  const { user, activeRole } = useAuth()
  const query = useApiQuery(`notifications:${user.id}:${activeRole}`, signal => listAll('/api/notifications', {}, signal))
  const [error, setError] = useState('')
  const [pending, setPending] = useState(null)
  const markRead = async id => {
    setPending(id); setError('')
    try { await api.patch(`/api/notifications/${id}/read`); query.reload() } catch (e) { setError(e.message) } finally { setPending(null) }
  }
  return <><ApiState {...query} />{error && <p role="alert">{error}</p>}{!query.loading && !query.error && !(query.data ?? []).length && <p>No notifications yet.</p>}<ul>{(query.data ?? []).map(n => <li key={n.id}><strong>{n.subject ?? n.channel}</strong><p>{n.message}</p><small>{displayTime(n.createdAt)} · {n.status}</small>{n.status !== 'READ' && <button className="app-ui-button" disabled={pending !== null} onClick={() => markRead(n.id)}>{pending === n.id ? 'Updating…' : 'Mark as read'}</button>}</li>)}</ul></>
}
