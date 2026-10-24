import { useCallback, useEffect, useEffectEvent, useState } from 'react'
import { session } from './session'

export function useApiQuery(key, load, enabled = true, pollInterval = 0) {
  const [result, setResult] = useState({ key: null, data: null, loading: enabled, error: null })
  const [revision, setRevision] = useState(0)
  const requestKey = `${session.identity()}:${key}:${revision}`
  const run = useEffectEvent(load)
  const reload = useCallback(() => setRevision(value => value + 1), [])
  const replaceData = useCallback(data => setResult({ key: requestKey, data, loading: false, error: null }), [requestKey])
  useEffect(() => {
    if (!enabled) return
    const controller = new AbortController()
    let timer
    const fetchData = async () => {
      try {
        const data = await run(controller.signal)
        if (!controller.signal.aborted) setResult({ key: requestKey, data, loading: false, error: null })
      } catch (error) {
        if (!controller.signal.aborted) setResult(previous => ({ key: requestKey,
          data: previous.key === requestKey ? previous.data : null, loading: false, error }))
      } finally {
        if (!controller.signal.aborted && pollInterval > 0) timer = setTimeout(() => {
          // Poll only while visible; preserve the last snapshot during refresh.
          if (document.visibilityState === 'hidden') scheduleVisibleCheck()
          else fetchData()
        }, pollInterval)
      }
    }
    const scheduleVisibleCheck = () => {
      if (controller.signal.aborted) return
      timer = setTimeout(() => {
        if (document.visibilityState === 'hidden') scheduleVisibleCheck()
        else fetchData()
      }, pollInterval)
    }
    fetchData()
    return () => { controller.abort(); clearTimeout(timer) }
    // The serialized key describes the request; caller closures are deliberately not dependencies.
  }, [requestKey, enabled, pollInterval])
  return { ...(enabled && result.key === requestKey ? result : { data: null, loading: enabled, error: null }), reload, replaceData }
}
