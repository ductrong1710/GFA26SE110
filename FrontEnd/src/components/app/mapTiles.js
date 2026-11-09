import L from 'leaflet'

export const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png'
export const SATELLITE_URL = 'https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}'

// A failed background must not disable locally rendered boundaries or editing.
export function addMapTiles(map) {
  const streets = L.tileLayer(TILE_URL, {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 19,
  })
  const satellite = L.tileLayer(SATELLITE_URL, {
    attribution: 'Source: <a href="https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer">Esri</a>, Vantor, Earthstar Geographics, and the GIS User Community',
    maxZoom: 19,
  })
  let tiles = satellite
  const notice = L.control({ position: 'bottomleft' })
  let paused = false
  notice.onAdd = () => {
    const box = L.DomUtil.create('div', 'map-tile-notice')
    box.setAttribute('role', 'status')
    const message = document.createElement('p')
    message.textContent = 'Map background unavailable. Check your Internet connection or DNS. Coordinates and boundaries remain available.'
    const retry = document.createElement('button')
    retry.type = 'button'
    retry.className = 'app-ui-button'
    retry.textContent = 'Retry map background'
    retry.addEventListener('click', () => {
      notice.remove()
      paused = false
      tiles.addTo(map)
    })
    box.append(message, retry)
    L.DomEvent.disableClickPropagation(box)
    L.DomEvent.disableScrollPropagation(box)
    return box
  }
  for (const layer of [streets, satellite]) {
    layer.on('tileerror', () => {
      if (paused || layer !== tiles || !map.hasLayer(layer)) return
      paused = true
      map.removeLayer(layer)
      notice.addTo(map)
    })
  }
  const switcher = L.control({ position: 'topright' })
  switcher.onAdd = () => {
    const box = L.DomUtil.create('div', 'map-background-control')
    const select = document.createElement('select')
    select.setAttribute('aria-label', 'Map background')
    for (const [value, label] of [['satellite', 'Satellite'], ['street', 'Street map']]) {
      const option = document.createElement('option')
      option.value = value
      option.textContent = label
      select.append(option)
    }
    select.addEventListener('change', () => {
      map.removeLayer(tiles)
      notice.remove()
      paused = false
      tiles = select.value === 'satellite' ? satellite : streets
      tiles.addTo(map)
    })
    box.append(select)
    L.DomEvent.disableClickPropagation(box)
    L.DomEvent.disableScrollPropagation(box)
    return box
  }
  switcher.addTo(map)
  tiles.addTo(map)
  return tiles
}
