import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import '../../styles/boundary-map.css'
import CurrentLocationControl from './CurrentLocationControl'
import { validateLatitude, validateLongitude } from '../../utils/geo'

export default function GeographicFarmMap({ farm, zones = [], nodes = [], mission, selectedZoneId, onSelect }) {
  const container = useRef(null)
  const mapRef = useRef(null)
  useEffect(() => {
    const map = L.map(container.current).setView([10.7777, 106.8053], 15)
    mapRef.current = map
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors', maxZoom: 19,
    }).addTo(map)
    const group = L.featureGroup().addTo(map)
    const label = text => { const element = document.createElement('span'); element.textContent = text; return element }
    const marker = (record, color) => {
      if (validateLatitude(record.latitude) && validateLongitude(record.longitude))
        return L.circleMarker([Number(record.latitude), Number(record.longitude)], { radius: 6, color, fillOpacity: 0.8 }).addTo(group).bindTooltip(label(record.name || record.deviceCode || 'Location'))
    }
    if (farm?.boundary) L.geoJSON(farm.boundary, { style: { color: '#50645c', fillOpacity: 0.04 } }).addTo(group)
    else if (farm) marker(farm, '#50645c')
    zones.forEach(zone => {
      const layer = zone.boundary ? L.geoJSON(zone.boundary, { style: {
        color: '#176346', weight: zone.id === selectedZoneId ? 4 : 2,
        fillOpacity: zone.id === selectedZoneId ? 0.3 : 0.1,
      } }).addTo(group).bindTooltip(label(zone.name)) : marker(zone, '#176346')
      if (layer && onSelect) layer.on('click', () => onSelect(zone.id))
    })
    nodes.filter(node => zones.some(zone => zone.id === node.zoneId)).forEach(node =>
      marker(node, !node.isActive ? '#718078' : node.status === 'ONLINE' ? '#176346' : '#b42318'))
    const targets = mission?.targets?.map(target => nodes.find(node => node.id === target.sensorNodeId)) ?? []
    // Do not bridge missing locations with an invented route segment.
    if (targets.length > 1 && targets.every(node => node && validateLatitude(node.latitude) && validateLongitude(node.longitude))) {
      const route = targets.map(node => [Number(node.latitude), Number(node.longitude)])
      L.polyline(route, { color: '#739686', dashArray: '6 6', weight: 2 }).addTo(group)
      const completed = route.slice(0, mission?.progress?.collectedTargets ?? 0)
      if (completed.length > 1) L.polyline(completed, { color: '#176346', weight: 3 }).addTo(group)
    }
    if (group.getBounds().isValid()) map.fitBounds(group.getBounds(), { padding: [25, 25], maxZoom: 18 })
    const observer = new ResizeObserver(() => map.invalidateSize())
    observer.observe(container.current)
    return () => { observer.disconnect(); map.remove(); mapRef.current = null }
  }, [farm, zones, nodes, mission, selectedZoneId, onSelect])
  return <div>
    <CurrentLocationControl mapRef={mapRef} />
    <div className="boundary-map" ref={container} aria-label="Farm geographic map" />
    <p>Recorded geographic locations. Records without boundaries appear as center points when available.</p>
    {onSelect && <div className="boundary-actions" aria-label="Select zone">{zones.map(zone =>
      <button type="button" className="app-ui-button" key={zone.id} aria-pressed={zone.id === selectedZoneId} onClick={() => onSelect(zone.id)}>{zone.name}</button>)}</div>}
  </div>
}
