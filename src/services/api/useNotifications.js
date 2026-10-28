import { useLayoutEffect, useRef, useState } from 'react'
import { useApiQuery } from './useApiQuery'
import { listAll } from './resources'
import { api } from './index'
import { session } from './session'
import { showToast } from '../../utils/toast'

export function useNotifications(userId, role) {
  const query = useApiQuery(`notifications:${userId}:${role}`,
    signal => listAll('/api/notifications', {}, signal), Boolean(userId), 10000)
  const [mutation, setMutation] = useState({ identity: null, pending: null, error: '' })
  const busy = useRef(false)
  const identity = `${session.identity()}:${userId}:${role}`
  const current = useRef(identity)
  useLayoutEffect(() => {
    current.current = identity
    return () => { current.current = null }
  }, [identity])
  const all = query.data ?? []
  const unread = all.filter(item => item.status !== 'READ')
  const latest = [...all].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt) || b.id - a.id).slice(0, 5)
  const markRead = async (id = 'all') => {
    if (busy.current) return
    busy.current = true
    const startedIdentity = identity
    const sessionIdentity = session.identity()
    setMutation({ identity, pending: id, error: '' })
    try {
      const targets = id === 'all' ? unread : unread.filter(item => item.id === id)
      for (const item of targets) {
        if (current.current !== startedIdentity || session.identity() !== sessionIdentity) return
        await api.request(`/api/notifications/${item.id}/read`, { method: 'PATCH', toast: false })
      }
      if (current.current === startedIdentity && session.identity() === sessionIdentity) {
        setMutation({ identity, pending: null, error: '' })
        if (targets.length) showToast('success', id === 'all' ? 'Đã đánh dấu tất cả thông báo là đã đọc.' : 'Đã đánh dấu thông báo là đã đọc.')
      }
    } catch (error) {
      if (current.current === startedIdentity) setMutation({ identity, pending: null, error: error.message })
    } finally {
      busy.current = false
      if (current.current === startedIdentity) query.reload()
    }
  }
  return { ...query, latest, hasUnread: unread.length > 0, markRead,
    pending: mutation.identity === identity ? mutation.pending : null,
    mutationError: mutation.identity === identity ? mutation.error : '' }
}
