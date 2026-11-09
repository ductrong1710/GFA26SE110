import React, { StrictMode, useState } from 'react'
import { afterEach, beforeAll, expect, test, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import L from 'leaflet'
import BoundaryEditorMap from '../src/components/app/BoundaryEditorMap'
import GeographicFarmMap from '../src/components/app/GeographicFarmMap'
import { addMapTiles, TILE_URL } from '../src/components/app/mapTiles'

test('satellite is the default and changing background preserves boundaries and view', () => {
  const container = document.createElement('div')
  document.body.append(container)
  const map = L.map(container).setView([10, 106], 17)
  try {
    const boundary = L.polygon([[10, 106], [10, 107], [11, 107]]).addTo(map)
    const satellite = addMapTiles(map)
    expect(satellite._url).toContain('World_Imagery/MapServer/tile/{z}/{y}/{x}')
    const selector = screen.getByRole('combobox', { name: 'Map background' })
    expect(selector.value).toBe('satellite')
    satellite.fire('tileerror')
    fireEvent.change(selector, { target: { value: 'street' } })
    expect(screen.queryByRole('button', { name: 'Retry map background' })).toBeNull()
    expect(map.hasLayer(satellite)).toBe(false)
    expect(map.hasLayer(boundary)).toBe(true)
    expect(map.getZoom()).toBe(17)
    expect(map.getCenter().lat).toBeCloseTo(10)
    const streets = Object.values(map._layers).find(layer => layer instanceof L.TileLayer)
    expect(streets._url).toBe(TILE_URL)
    satellite.fire('tileerror')
    expect(map.hasLayer(streets)).toBe(true)
    fireEvent.change(selector, { target: { value: 'satellite' } })
    expect(map.hasLayer(satellite)).toBe(true)
    expect(map.hasLayer(streets)).toBe(false)
  } finally { map.remove(); container.remove() }
})

test('tile failure pauses background requests without removing boundaries and permits explicit retry', () => {
  const container = document.createElement('div')
  document.body.append(container)
  const map = L.map(container).setView([10, 106], 12)
  try {
    const boundary = L.polygon([[10, 106], [10, 107], [11, 107]]).addTo(map)
    const tiles = addMapTiles(map)
    expect(TILE_URL).toBe('https://tile.openstreetmap.org/{z}/{x}/{y}.png')
    act(() => tiles.fire('tileerror'))
    expect(map.hasLayer(tiles)).toBe(false)
    expect(map.hasLayer(boundary)).toBe(true)
    expect(screen.getByRole('status').textContent).toContain('Check your Internet connection or DNS')
    map.setZoom(13)
    expect(map.hasLayer(tiles)).toBe(false)
    fireEvent.click(screen.getByRole('button', { name: 'Retry map background' }))
    expect(map.hasLayer(tiles)).toBe(true)
    expect(screen.queryByRole('status')).toBeNull()
    act(() => tiles.fire('tileerror'))
    expect(screen.getAllByRole('button', { name: 'Retry map background' })).toHaveLength(1)
  } finally { map.remove(); container.remove() }
})

beforeAll(() => {
  L.Browser.svg = true
  globalThis.ResizeObserver = class { observe() {} disconnect() {} }
})
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals() })
test('current location is opt-in and does not replace the boundary', () => {
  let success
  const getCurrentPosition = vi.fn(callback => { success = callback })
  vi.stubGlobal('navigator', { geolocation: { getCurrentPosition } })
  render(<Editor />)
  expect(getCurrentPosition).not.toHaveBeenCalled()
  enter([[106, 10], [107, 10], [107, 11], [106, 11]])
  fireEvent.click(screen.getByText('Apply Coordinates'))
  const previous = screen.getByTestId('boundary').textContent
  fireEvent.click(screen.getByRole('button', { name: 'Current location' }))
  expect(getCurrentPosition).toHaveBeenCalledTimes(1)
  act(() => success({ coords: { latitude: 10.7778899, longitude: 106.8051063, accuracy: 25 } }))
  expect(screen.getByRole('status', { name: 'Current coordinates' }).textContent).toContain('10.7778899')
  expect(screen.getByRole('status', { name: 'Current coordinates' }).textContent).toContain('25 m')
  expect(screen.getByTestId('boundary').textContent).toBe(previous)
})
test('denied location keeps manual editing available and permits retry', () => {
  const getCurrentPosition = vi.fn((_, failure) => failure({ code: 1 }))
  vi.stubGlobal('navigator', { geolocation: { getCurrentPosition } })
  render(<Editor />)
  fireEvent.click(screen.getByRole('button', { name: 'Current location' }))
  expect(screen.getByRole('alert').textContent).toContain('denied')
  expect(getCurrentPosition).toHaveBeenCalledTimes(1)
  expect(screen.getByText('Save record').disabled).toBe(false)
  expect(screen.getByRole('button', { name: 'Current location' }).disabled).toBe(false)
})
test('location timeout and insecure HTTP explain manual fallback', () => {
  const getCurrentPosition = vi.fn((_, failure) => failure({ code: 3 }))
  vi.stubGlobal('navigator', { geolocation: { getCurrentPosition } })
  render(<Editor />)
  fireEvent.click(screen.getByRole('button', { name: 'Current location' }))
  expect(screen.getByRole('alert').textContent).toContain('timed out')
  vi.stubGlobal('isSecureContext', false)
  fireEvent.click(screen.getByRole('button', { name: 'Current location' }))
  expect(screen.getByRole('alert').textContent).toContain('HTTPS or localhost')
  expect(getCurrentPosition).toHaveBeenCalledTimes(2)
})
test('location retries a timeout once and ignores the first attempt arriving late', () => {
  const calls = []
  const getCurrentPosition = vi.fn((success, fail, options) => calls.push({ success, fail, options }))
  vi.stubGlobal('navigator', { geolocation: { getCurrentPosition } })
  render(<Editor />)
  fireEvent.click(screen.getByRole('button', { name: 'Current location' }))
    expect(calls[0].options).toEqual({ enableHighAccuracy: true, timeout: 30000, maximumAge: 0 })
  act(() => calls[0].fail({ code: 3 }))
  expect(calls).toHaveLength(2)
  expect(calls[1].options).toEqual({ enableHighAccuracy: true, timeout: 30000, maximumAge: 0 })
  expect(screen.queryByRole('alert')).toBeNull()
  act(() => calls[0].success({ coords: { latitude: 1, longitude: 2, accuracy: 500 } }))
  expect(screen.queryByRole('status', { name: 'Current coordinates' })).toBeNull()
  act(() => calls[1].success({ coords: { latitude: 10, longitude: 106, accuracy: 25 } }))
  expect(screen.getByRole('status', { name: 'Current coordinates' }).textContent).toContain('10.0000000, 106.0000000')
    expect(screen.getByRole('status', { name: 'Current coordinates' }).textContent).toContain('approximately 25 m')
  expect(screen.getByTestId('boundary').textContent).toBe('null')
})
test('late location callbacks are ignored after the dialog closes', () => {
  let success
  vi.stubGlobal('navigator', { geolocation: { getCurrentPosition: callback => { success = callback } } })
  const { unmount } = render(<Editor />)
  fireEvent.click(screen.getByRole('button', { name: 'Current location' }))
  unmount()
  expect(() => act(() => success({ coords: { latitude: 10, longitude: 106, accuracy: 20 } }))).not.toThrow()
})
test('geographic overview renders real sensors, route and accessible zone selection', () => {
  const onSelect = vi.fn()
  const zones = [{ id: 1, name: 'North', boundary: { type: 'Polygon', coordinates: [[[106, 10], [107, 10], [107, 11], [106, 10]]] } }]
  const nodes = [{ id: 1, zoneId: 1, latitude: 10.2, longitude: 106.3, status: 'ONLINE', isActive: true },
    { id: 2, zoneId: 1, latitude: 10.3, longitude: 106.4, status: 'OFFLINE', isActive: true }]
  const { container } = render(<GeographicFarmMap zones={zones} nodes={nodes} onSelect={onSelect}
    mission={{ targets: [{ sensorNodeId: 1 }, { sensorNodeId: 2 }], progress: { collectedTargets: 2 } }} />)
  expect(container.querySelectorAll('.leaflet-overlay-pane path').length).toBe(5)
  fireEvent.click(screen.getByRole('button', { name: 'North' }))
  expect(onSelect).toHaveBeenCalledWith(1)
})
function Editor({ parentBoundary }) {
  const [boundary, setBoundary] = useState(null)
  const [valid, setValid] = useState(true)
  return <><BoundaryEditorMap boundary={boundary} parentBoundary={parentBoundary} onChange={setBoundary}
    onValidityChange={setValid} isZone={Boolean(parentBoundary)} />
    <button disabled={!valid}>Save record</button><output data-testid="boundary">{JSON.stringify(boundary)}</output></>
}
const enter = (coordinates) => coordinates.forEach(([longitude, latitude], index) => {
  fireEvent.change(screen.getByLabelText(`Point ${index + 1} Latitude`), { target: { value: latitude } })
  fireEvent.change(screen.getByLabelText(`Point ${index + 1} Longitude`), { target: { value: longitude } })
})
test('manual workflow applies to Leaflet, blocks unapplied edits and clears under StrictMode', () => {
  const { container } = render(<StrictMode><Editor /></StrictMode>)
  enter([[106, 10], [107, 10], [107, 11], [106, 11]])
  expect(screen.getByText('Save record').disabled).toBe(true)
  fireEvent.click(screen.getByText('Apply Coordinates'))
  expect(screen.getByText('Save record').disabled).toBe(false)
  expect(JSON.parse(screen.getByTestId('boundary').textContent).coordinates[0][4]).toEqual([106, 10])
  expect(container.querySelector('.leaflet-overlay-pane path')).not.toBeNull()
  fireEvent.change(screen.getByLabelText('Point 1 Latitude'), { target: { value: '' } })
  expect(screen.getByText('Save record').disabled).toBe(true)
  fireEvent.click(screen.getByText('Clear Boundary'))
  expect(screen.getByTestId('boundary').textContent).toBe('null')
  expect(container.querySelector('.leaflet-overlay-pane path')).toBeNull()
})
test('outside zone shows an accessible error and cannot save', () => {
  render(<Editor parentBoundary={{ type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 1], [0, 0]]] }} />)
  enter([[106, 10], [107, 10], [107, 11], [106, 11]])
  fireEvent.click(screen.getByText('Apply Coordinates'))
  expect(screen.getByRole('alert').textContent).toContain('completely inside')
  expect(screen.getByText('Save record').disabled).toBe(true)
})
test('drawing and vertex editing update manual coordinates and canonical boundary', () => {
  const originalMap = L.map
  let map
  const spy = vi.spyOn(L, 'map').mockImplementation((...args) => { map = originalMap(...args); return map })
  render(<Editor />)
  const layer = L.polygon([[10, 106], [10, 107], [11, 107], [11, 106]]).addTo(map)
  // Geoman's public events exercise the same path as mouse drawing/editing.
  fireEvent.click(screen.getByText('Manual Coordinates'))
  act(() => map.fire('pm:create', { layer, shape: 'Polygon' }))
  expect(map.hasLayer(layer)).toBe(true)
  expect(screen.getByLabelText('Point 1 Latitude').value).toBe('10')
  const current = Object.values(map._layers).find(item => item instanceof L.Polygon)
  act(() => { current.setLatLngs([[10.1, 106], [10, 107], [11, 107], [11, 106]]); current.fire('pm:edit') })
  expect(screen.getByLabelText('Point 1 Latitude').value).toBe('10.1')
  expect(map.hasLayer(current)).toBe(true)
  spy.mockRestore()
})
