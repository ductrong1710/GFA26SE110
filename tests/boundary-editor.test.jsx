import React, { StrictMode, useState } from 'react'
import { afterEach, beforeAll, expect, test, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import L from 'leaflet'
import BoundaryEditorMap from '../src/components/app/BoundaryEditorMap'
import GeographicFarmMap from '../src/components/app/GeographicFarmMap'

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
  expect(getCurrentPosition).toHaveBeenCalledTimes(1)
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
