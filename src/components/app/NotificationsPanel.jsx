import ApiState from './ApiState'
import { displayTime } from '../../utils/display'

export default function NotificationsPanel({ notifications }) {
  const { latest, hasUnread, markRead, pending, mutationError, loading, error, reload } = notifications
  return <>
    <div className="app-notification-heading">
      <h2>Notifications</h2>
      <button type="button" className="app-ui-button" disabled={loading || !hasUnread || pending !== null}
        onClick={() => markRead()}>{pending === 'all' ? 'Reading…' : 'Read all'}</button>
    </div>
    <ApiState loading={loading} error={error} reload={reload} />
    {mutationError && <p role="alert">{mutationError}</p>}
    {!loading && !error && !latest.length && <p>No notifications yet.</p>}
    <ul>{latest.map(n => <li key={n.id} className={n.status !== 'READ' ? 'is-unread' : undefined}>
      <strong>{n.subject ?? n.channel}</strong><p>{n.message}</p>
      <small>{displayTime(n.createdAt)} · {n.status}</small>
      {n.status !== 'READ' && <button type="button" className="app-ui-button" disabled={pending !== null}
        onClick={() => markRead(n.id)}>{pending === n.id ? 'Updating…' : 'Mark as read'}</button>}
    </li>)}</ul>
  </>
}
