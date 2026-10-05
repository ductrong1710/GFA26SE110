import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

export default function RouteScroll() {
  const { pathname, hash, key } = useLocation()

  useEffect(() => {
    // Router links do not trigger the browser's native cross-page anchor scroll.
    const frame = window.requestAnimationFrame(() => {
      if (hash) {
        let id = hash.slice(1)
        try { id = decodeURIComponent(id) } catch { /* Keep malformed hashes harmless. */ }
        const target = document.getElementById(id)
        if (target) {
          target.scrollIntoView()
          return
        }
      }
      window.scrollTo({ top: 0, left: 0, behavior: 'instant' })
    })
    return () => window.cancelAnimationFrame(frame)
  }, [pathname, hash, key])

  return null
}
