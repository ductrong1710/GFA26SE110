import { useEffect, useRef, useState } from 'react'
import L from 'leaflet'
import { addMapTiles } from './mapTiles'
import '@geoman-io/leaflet-geoman-free'
import 'leaflet/dist/leaflet.css'
import '@geoman-io/leaflet-geoman-free/dist/leaflet-geoman.css'
import '../../styles/boundary-map.css'
import CurrentLocationControl from './CurrentLocationControl'
import { normalizePolygonGeoJson, manualPointsToGeoJson, geoJsonToManualPoints,
  calculatePolygonArea, isPolygonInsidePolygon } from '../../utils/geo'

const blankPoints = () => Array.from({ length: 4 }, () => ({ latitude: '', longitude: '' }))

export default function BoundaryEditorMap({ boundary, parentBoundary, center, onChange, onValidityChange, isZone }) {
  const container = useRef(null)
  const mapRef = useRef(null)
  const layerRef = useRef(null)
  const handlers = useRef(null)
  const [rows, setRows] = useState(() => boundary ? geoJsonToManualPoints(boundary) : blankPoints())
  const [error, setError] = useState('')
  const [dirty, setDirty] = useState(false)
  const [mode, setMode] = useState('manual')
  const rowsRef = useRef(null)
  const validate = value => {
    const valid = normalizePolygonGeoJson(value)
    if (parentBoundary && !isPolygonInsidePolygon(valid, parentBoundary))
      throw new Error('Zone boundary must be completely inside the farm boundary.')
    return valid
  }
  const accept = value => {
    try {
      const valid = validate(value)
      setRows(geoJsonToManualPoints(valid)); setDirty(false); setError('')
      onValidityChange(true); onChange(valid)
    } catch (err) {
      setError(err.message); onValidityChange(false)
    }
  }
  useEffect(() => {
    handlers.current = { accept, clear: () => {
      if (layerRef.current) mapRef.current.removeLayer(layerRef.current)
      layerRef.current = null
      setRows(blankPoints()); setDirty(false); setError(''); onValidityChange(true); onChange(null)
    } }
  })
  useEffect(() => {
    const map = L.map(container.current).setView([10.7777, 106.8053], 16)
    mapRef.current = map
    addMapTiles(map)
    map.pm.addControls({ position: 'topleft', drawMarker: false, drawCircleMarker: false, drawPolyline: false,
      drawRectangle: false, drawCircle: false, drawText: false, cutPolygon: false, rotateMode: false })
    map.pm.setGlobalOptions({ allowSelfIntersection: false })
    const bind = layer => {
      layer.on('pm:edit pm:dragend pm:vertexadded pm:vertexremoved', () => handlers.current.accept(layer.toGeoJSON(false).geometry))
      layer.on('pm:remove', () => handlers.current.clear())
    }
    map.on('pm:create', ({ layer }) => {
      if (layerRef.current) map.removeLayer(layerRef.current)
      layerRef.current = layer
      bind(layer)
      handlers.current.accept(layer.toGeoJSON(false).geometry)
    })
    mapRef.current.bindBoundary = bind
    const observer = new ResizeObserver(() => map.invalidateSize())
    observer.observe(container.current)
    return () => { observer.disconnect(); map.remove(); mapRef.current = null; layerRef.current = null }
  }, [])
  useEffect(() => {
    const map = mapRef.current
    // Keep Geoman's active handles when this layer itself emitted the geometry.
    if (boundary && layerRef.current &&
        JSON.stringify(layerRef.current.toGeoJSON(false).geometry) === JSON.stringify(boundary)) return
    if (boundary) {
      let layer = layerRef.current
      const latlngs = boundary.coordinates[0].map(([lng, lat]) => [lat, lng])
      if (layer) {
        layer.setLatLngs(latlngs)
        if (layer.pm.enabled()) { layer.pm.disable(); layer.pm.enable({ allowSelfIntersection: false }) }
      } else {
        layer = L.polygon(latlngs, { color: '#176346' }).addTo(map)
        layerRef.current = layer
        map.bindBoundary(layer)
      }
      map.fitBounds(layer.getBounds(), { padding: [25, 25], maxZoom: 18 })
    } else {
      if (layerRef.current) map.removeLayer(layerRef.current)
      layerRef.current = null
    }
  }, [boundary])
  useEffect(() => {
    const map = mapRef.current
    let outer
    if (parentBoundary) {
      outer = L.geoJSON(parentBoundary, { style: { color: '#687880', fillOpacity: 0.06, dashArray: '6 5' },
        pmIgnore: true, snapIgnore: true }).addTo(map)
      map.fitBounds(outer.getBounds(), { padding: [25, 25], maxZoom: 18 })
    }
    return () => { if (outer) map.removeLayer(outer) }
  }, [parentBoundary])
  useEffect(() => {
    if (!parentBoundary && !layerRef.current && center?.latitude !== '' && center?.latitude != null &&
        center?.longitude !== '' && center?.longitude != null)
      mapRef.current.setView([Number(center.latitude), Number(center.longitude)], 16)
  }, [parentBoundary, center?.latitude, center?.longitude])
  let containmentError = ''
  if (boundary && parentBoundary && !isPolygonInsidePolygon(boundary, parentBoundary))
    containmentError = 'Zone boundary must be completely inside the farm boundary.'
  const valid = !dirty && !error && !containmentError
  useEffect(() => { onValidityChange(valid) }, [valid, onValidityChange])
  const editRow = (index, field, value) => {
    setRows(current => current.map((row, i) => i === index ? { ...row, [field]: value } : row))
    setDirty(true); onValidityChange(false)
  }
  const apply = () => { try { accept(manualPointsToGeoJson(rows)) } catch (err) { setError(err.message); onValidityChange(false) } }
  const area = boundary ? calculatePolygonArea(boundary) : null
  return <section className="boundary-editor" aria-label="Boundary">
    <h3>Boundary</h3>
    <div className="boundary-actions">
      <button type="button" className="app-ui-button" aria-pressed={mode === 'draw'} onClick={() => { setMode('draw'); mapRef.current.pm.enableDraw('Polygon') }}>Draw on Map</button>
      <button type="button" className="app-ui-button" aria-pressed={mode === 'manual'} onClick={() => { setMode('manual'); mapRef.current.pm.disableDraw(); rowsRef.current?.querySelector('input')?.focus() }}>Manual Coordinates</button>
    </div>
    <CurrentLocationControl mapRef={mapRef} />
    <div className="boundary-map" ref={container} aria-label="Boundary map" />
    <p>{area === null ? 'No boundary recorded.' : `Area: ${area.toLocaleString(undefined, { maximumFractionDigits: 2 })} m² / ${(area / 10000).toLocaleString(undefined, { maximumFractionDigits: 4 })} ha`}</p>
    {isZone && !parentBoundary && <p>Farm boundary is not recorded; geographic containment validation is unavailable.</p>}
    <div ref={rowsRef}>{rows.map((row, index) => <div className="farm-form-pair" key={index}>
      <label>Point {index + 1} Latitude<input type="number" step="any" min="-90" max="90" value={row.latitude} onChange={e => editRow(index, 'latitude', e.target.value)} /></label>
      <label>Point {index + 1} Longitude<input type="number" step="any" min="-180" max="180" value={row.longitude} onChange={e => editRow(index, 'longitude', e.target.value)} /></label>
    </div>)}</div>
    {dirty && <p>Apply coordinates before saving.</p>}
    {(error || containmentError) && <p role="alert" className="farm-form-error">{error || containmentError}</p>}
    <div className="boundary-actions">
      <button type="button" className="app-ui-button" onClick={apply}>Apply Coordinates</button>
      <button type="button" className="app-ui-button" onClick={() => handlers.current.clear()}>Clear Boundary</button>
    </div>
  </section>
}
