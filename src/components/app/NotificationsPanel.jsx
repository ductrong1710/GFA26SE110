import { useState } from 'react'
import { Link } from 'react-router-dom'
import ApiState from './ApiState'
import { displayTime } from '../../utils/display'

function consultationLink(notification) {
  if (['Yêu cầu tư vấn cần phân công lại', 'Kỹ sư đã tiếp nhận yêu cầu tư vấn'].includes(notification.subject))
    return notification.message?.match(/^Yêu cầu tư vấn #([1-9][0-9]*)\b/)?.[1]
  if (notification.subject === 'Yêu cầu tư vấn được phân công')
    return notification.message?.match(/^Bạn được phân công xử lý yêu cầu tư vấn #([1-9][0-9]*)\b/)?.[1]
}

export default function NotificationsPanel({ notifications }) {
  const { latest, hasUnread, markRead, pending, mutationError, loading, error, reload } = notifications
  const [page, setPage] = useState(0)
  const items = notifications.items ?? latest
  const lastPage = Math.max(0, Math.ceil(items.length / 5) - 1)
  const currentPage = Math.min(page, lastPage)
  const visible = items.slice(currentPage * 5, currentPage * 5 + 5)
  return <>
    <div className="app-notification-heading">
      <h2>Notifications</h2>
      <button type="button" className="app-ui-button" disabled={loading || !hasUnread || pending !== null}
        onClick={() => markRead()}>{pending === 'all' ? 'Reading…' : 'Read all'}</button>
    </div>
    <ApiState loading={loading} error={error} reload={reload} />
    {mutationError && <p role="alert">{mutationError}</p>}
    {!loading && !error && !latest.length && <p>No notifications yet.</p>}
    <ul>{visible.map(n => {
      const consultationId = consultationLink(n)
      return <li key={n.id} className={n.status !== 'READ' ? 'is-unread' : undefined}>
        <strong>{n.subject ?? n.channel}</strong><p>{n.message}</p>
        <small>{displayTime(n.createdAt)} · {n.status}</small>
        {consultationId && <p><Link to={`/app/consultations/${consultationId}`}>Xem chi tiết tư vấn</Link></p>}
        {n.status !== 'READ' && <button type="button" className="app-ui-button" disabled={pending !== null}
          onClick={() => markRead(n.id)}>{pending === n.id ? 'Updating…' : 'Mark as read'}</button>}
      </li>
    })}</ul>
    {lastPage > 0 && <div className="app-notification-heading">
      <button type="button" className="app-ui-button" disabled={currentPage === 0}
        onClick={() => setPage(currentPage - 1)}>Newer notifications</button>
      <span>{currentPage + 1} / {lastPage + 1}</span>
      <button type="button" className="app-ui-button" disabled={currentPage === lastPage}
        onClick={() => setPage(currentPage + 1)}>Older notifications</button>
    </div>}
  </>
}
