import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import '../../styles/boundary-map.css'
import { addMapTiles } from './mapTiles'
import CurrentLocationControl from './CurrentLocationControl'

const point = (lat, lng) => lat !== '' && lat != null && lng !== '' && lng != null &&
  Number.isFinite(Number(lat)) && Number.isFinite(Number(lng)) &&
  Math.abs(Number(lat)) <= 90 && Math.abs(Number(lng)) <= 180
  ? [Number(lat), Number(lng)] : null

export default function SensorLocationMap({ latitude, longitude, farm, zone, onChange, disabled }) {
  const container = useRef(null)
  const mapRef = useRef(null)
  const marker = useRef(null)
  const handler = useRef(null)
  useEffect(() => { handler.current = { onChange, disabled } })
  useEffect(() => {
    const map = L.map(container.current, { pmIgnore: true }).setView([10.7777, 106.8053], 16)
    mapRef.current = map
    addMapTiles(map)
    map.on('click', ({ latlng }) => {
      if (!handler.current.disabled) handler.current.onChange({
        latitude: Number(latlng.lat.toFixed(7)), longitude: Number(latlng.wrap().lng.toFixed(7)),
      })
    })
    const observer = new ResizeObserver(() => map.invalidateSize())
    observer.observe(container.current)
    return () => { observer.disconnect(); map.remove(); mapRef.current = null; marker.current = null }
  }, [])
  useEffect(() => {
    const map = mapRef.current
    const selected = point(latitude, longitude)
    if (selected) {
      if (marker.current) marker.current.setLatLng(selected)
      else marker.current = L.circleMarker(selected, { radius: 8, color: '#fff', weight: 2,
        fillColor: '#dc2626', fillOpacity: 1, pmIgnore: true, interactive: false }).addTo(map)
      map.setView(selected, Math.max(map.getZoom(), 17))
    } else {
      marker.current?.remove(); marker.current = null
      const center = point(farm?.latitude, farm?.longitude)
      if (center) map.setView(center, 16)
    }
  }, [latitude, longitude, farm?.id, farm?.latitude, farm?.longitude])
  useEffect(() => {
    const map = mapRef.current
    const layers = []
    for (const [boundary, style] of [
      [farm?.boundary, { color: '#22c55e', weight: 3, fillOpacity: 0.03 }],
      [zone?.boundary, { color: '#f59e0b', weight: 3, dashArray: '7 5', fillOpacity: 0.08 }],
    ]) {
      if (boundary) layers.push(L.geoJSON(boundary, {
        style, interactive: false, pmIgnore: true, snapIgnore: true,
      }).addTo(map))
    }
    if (layers.length) {
      const bounds = L.latLngBounds([])
      layers.forEach(layer => bounds.extend(layer.getBounds()))
      if (bounds.isValid()) map.fitBounds(bounds, { padding: [24, 24], maxZoom: 18 })
    }
    return () => { layers.forEach(layer => layer.remove()) }
  }, [farm?.id, farm?.boundary, zone?.id, zone?.boundary])
  return <section className="sensor-location-picker" aria-label="Sensor location">
    <h3>Sensor location</h3>
    <p>Click the map to select the sensor position, or enter Latitude and Longitude in the form. Current location centers the map; click to confirm the sensor position.</p>
    <CurrentLocationControl mapRef={mapRef} />
    <div className="boundary-map" ref={container} aria-label="Sensor location map" />
    <p>Farm: solid green outline · Selected zone: dashed amber outline. Boundaries are read-only.</p>
    {!farm?.boundary && <p>The selected farm has no recorded boundary.</p>}
    {zone && !zone.boundary && <p>The selected zone has no recorded boundary.</p>}
    <button type="button" className="app-ui-button" disabled={disabled} onClick={() => onChange({ latitude: '', longitude: '' })}>Clear location</button>
  </section>
}
