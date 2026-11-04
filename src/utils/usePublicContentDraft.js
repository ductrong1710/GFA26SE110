import { useEffect, useState } from 'react'
import { websiteContentKinds, websiteContentGroup } from '../config/websiteContent'

export const emptyContent = { kind: '', title: '', body: '', estimatedMonthlyRentVnd: '', replacementPriceVnd: '' }
const emptyDraft = () => ({ record: { ...emptyContent }, image: [], removeImage: false })

function read(key) {
  try {
    const saved = JSON.parse(sessionStorage.getItem(key))
    if (saved?.record && (saved.record.kind === '' || websiteContentKinds.includes(websiteContentGroup(saved.record.kind)))) return {
      record: { ...emptyContent, ...saved.record, kind: websiteContentGroup(saved.record.kind) },
      image: Array.isArray(saved.image) ? saved.image.slice(0, 1) : [],
      removeImage: saved.removeImage === true,
    }
  } catch { /* Invalid or unavailable storage starts with an empty form. */ }
  return emptyDraft()
}

export function usePublicContentDraft(userId) {
  const key = `smartfarm.public-content.draft:${userId ?? 'unknown'}`
  const [state, setState] = useState(() => ({ key, draft: read(key) }))
  const [storageError, setStorageError] = useState(false)
  if (state.key !== key) setState({ key, draft: read(key) })

  useEffect(() => {
    if (state.key !== key) return
    try {
      const { record, image, removeImage } = state.draft
      const hasDraft = record.id || Object.values(record).some(value => value !== '' && value != null) || image.length || removeImage
      if (hasDraft) sessionStorage.setItem(key, JSON.stringify(state.draft))
      else sessionStorage.removeItem(key)
      setStorageError(false)
    } catch { setStorageError(true) }
  }, [key, state])

  const update = change => setState(old => {
    const draft = old.key === key ? old.draft : read(key)
    return { key, draft: { ...draft, ...change(draft) } }
  })
  const reset = () => {
    try { sessionStorage.removeItem(key) } catch { /* The effect reports unavailable storage. */ }
    setState({ key, draft: emptyDraft() })
  }
  return { ...state.draft, update, reset, storageError }
}
