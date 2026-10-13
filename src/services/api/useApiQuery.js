import { useCallback, useEffect, useEffectEvent, useState } from 'react'
import { session } from './session'

export function useApiQuery(key, load, enabled = true) {
  const [result, setResult] = useState({ key: null, data: null, loading: enabled, error: null })
  const [revision, setRevision] = useState(0)
  const requestKey = `${session.identity()}:${key}:${revision}`
  const run = useEffectEvent(load)
  const reload = useCallback(() => setRevision(value => value + 1), [])
  const replaceData = useCallback(data => setResult({ key: requestKey, data, loading: false, error: null }), [requestKey])
  useEffect(() => {
    if (!enabled) return
    const controller = new AbortController()
    run(controller.signal).then(data => {
      if (!controller.signal.aborted) setResult({ key: requestKey, data, loading: false, error: null })
    }).catch(error => {
      if (!controller.signal.aborted) setResult({ key: requestKey, data: null, loading: false, error })
    })
    return () => controller.abort()
    // The serialized key describes the request; caller closures are deliberately not dependencies.
  }, [requestKey, enabled])
  return { ...(enabled && result.key === requestKey ? result : { data: null, loading: enabled, error: null }), reload, replaceData }
}
