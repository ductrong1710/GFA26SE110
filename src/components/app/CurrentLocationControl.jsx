import { useEffect, useRef, useState } from 'react'
import L from 'leaflet'

export default function CurrentLocationControl({ mapRef }) {
  const [pending, setPending] = useState(false)
  const [location, setLocation] = useState(null)
  const [error, setError] = useState('')
  const request = useRef(0)
  const overlay = useRef(null)
  useEffect(() => () => {
    request.current += 1
    overlay.current?.remove()
    overlay.current = null
  }, [])
  const locate = () => {
    setError('')
    if (window.isSecureContext === false) {
      setError('Location requires HTTPS or localhost. You can still enter coordinates manually.')
      return
    }
    if (!navigator.geolocation) {
      setError('This browser does not support location. You can still enter coordinates manually.')
      return
    }
    const map = mapRef.current
    if (!map) return
    const id = ++request.current
    setPending(true)
    const current = () => request.current === id
    const fail = failure => {
      if (!current()) return
      setPending(false)
      setError(failure.code === 1
        ? 'Location permission was denied. Allow location in browser settings or enter coordinates manually.'
        : failure.code === 3 ? 'Location request timed out. Please try again or enter coordinates manually.'
          : 'Current location is unavailable. Please try again or enter coordinates manually.')
    }
    try {
      navigator.geolocation.getCurrentPosition(({ coords }) => {
        if (!current()) return
        if (mapRef.current !== map) { setPending(false); return }
        const { latitude, longitude, accuracy } = coords
        if (!Number.isFinite(latitude) || !Number.isFinite(longitude) || Math.abs(latitude) > 90 ||
            Math.abs(longitude) > 180 || !Number.isFinite(accuracy) || accuracy < 0) {
          fail({ code: 2 }); return
        }
        overlay.current?.remove()
        const options = { pmIgnore: true, snapIgnore: true, interactive: false }
        overlay.current = L.layerGroup([
          L.circle([latitude, longitude], { ...options, radius: accuracy, color: '#2563eb', weight: 1, fillOpacity: 0.08 }),
          L.circleMarker([latitude, longitude], { ...options, radius: 7, color: '#fff', weight: 2, fillColor: '#2563eb', fillOpacity: 1 }),
        ]).addTo(map)
        map.setView([latitude, longitude], 17)
        setLocation({ latitude, longitude, accuracy })
        setPending(false)
      }, fail, { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 })
    } catch {
      fail({ code: 2 })
    }
  }
  return <div className="current-location-control">
    <button type="button" className="app-ui-button" onClick={locate} disabled={pending}>
      {pending ? 'Locating…' : 'Current location'}
    </button>
    {location && <p role="status" aria-label="Current coordinates">
      Your location: {location.latitude.toFixed(7)}, {location.longitude.toFixed(7)} · Accuracy: approximately {Math.round(location.accuracy)} m.
    </p>}
    {error && <p role="alert" className="farm-form-error">{error}</p>}
  </div>
}
