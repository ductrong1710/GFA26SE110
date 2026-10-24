import React from 'react'
import { afterEach, beforeAll, expect, test, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import EquipmentDialog from '../src/components/app/EquipmentDialog'
import L from 'leaflet'

afterEach(cleanup)
beforeAll(() => {
  L.Browser.svg = true
  globalThis.ResizeObserver = class { observe() {} disconnect() {} }
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }
})
test('sensor map picks one point, updates inputs, and has no drawing tools', async () => {
  const original = L.map
  let map
  const spy = vi.spyOn(L, 'map').mockImplementation((...args) => { map = original(...args); return map })
  const save = vi.fn().mockResolvedValue(undefined)
  try {
    render(<EquipmentDialog kind="sensor" record={record} workspace={workspace} operations={{ missions: [] }} currentFarm={{ id: 1 }} onSave={save} onClose={() => {}} />)
    const { act } = await import('@testing-library/react')
    act(() => map.fire('click', { latlng: L.latLng(10.1234567, 106.1234567) }))
    expect(screen.getByLabelText('Latitude').value).toBe('10.1234567')
    expect(screen.getByLabelText('Longitude').value).toBe('106.1234567')
    expect(document.querySelector('.leaflet-pm-toolbar')).toBeNull()
    fireEvent.change(screen.getByLabelText('Latitude'), { target: { value: '11' } })
    const markers = Object.values(map._layers).filter(layer => layer instanceof L.CircleMarker)
    expect(markers).toHaveLength(1)
    expect(markers[0].getLatLng().lat).toBe(11)
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(save).toHaveBeenCalledWith(expect.objectContaining({ latitude: '11', longitude: 106.1234567 })))
    fireEvent.click(screen.getByRole('button', { name: 'Clear location' }))
    expect(screen.getByLabelText('Latitude').value).toBe('')
  } finally { spy.mockRestore() }
})
const workspace = { farms: [{ id: 1, name: 'Farm A' }, { id: 2, name: 'Farm B' }, { id: 3, name: 'Empty farm' }],
  zones: [{ id: 11, farmId: 1, name: 'Zone A' }, { id: 22, farmId: 2, name: 'Zone B' }] }
const record = { id: 5, deviceCode: 'NODE-5', name: 'Sensor', zoneId: 11, status: 'OFFLINE' }
test('sensor map displays selected boundaries, removes old outlines and precedes coordinate inputs', () => {
  const boundary = { type: 'Polygon', coordinates: [[[106, 10], [107, 10], [107, 11], [106, 10]]] }
  const scoped = { farms: workspace.farms.map(f => ({ ...f, boundary: f.id === 1 ? boundary : null })),
    zones: workspace.zones.map(z => ({ ...z, boundary: z.id === 11 ? boundary : null })) }
  const original = L.map
  let map
  const spy = vi.spyOn(L, 'map').mockImplementation((...args) => { map = original(...args); return map })
  try {
    render(<EquipmentDialog kind="sensor" record={record} workspace={scoped} operations={{ missions: [] }} currentFarm={{ id: 1 }} onSave={vi.fn()} onClose={() => {}} />)
    const polygons = () => Object.values(map._layers).filter(layer => layer instanceof L.Polygon)
    expect(polygons()).toHaveLength(2)
    expect(polygons().every(layer => layer.options.interactive === false && layer.options.pmIgnore)).toBe(true)
    const locationMap = screen.getByLabelText('Sensor location map')
    expect(locationMap.compareDocumentPosition(screen.getByLabelText('Latitude')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    fireEvent.change(screen.getByRole('combobox', { name: 'Farm' }), { target: { value: '2' } })
    expect(polygons()).toHaveLength(0)
    expect(screen.getByText('The selected zone has no recorded boundary.')).toBeTruthy()
    expect(screen.getByLabelText('Latitude').value).toBe('')
  } finally { spy.mockRestore() }
})
test('editing a sensor can move to a zone in another farm', async () => {
  const save = vi.fn().mockResolvedValue(undefined)
  render(<EquipmentDialog kind="sensor" record={record} workspace={workspace} operations={{ missions: [] }} currentFarm={{ id: 1 }} onSave={save} onClose={() => {}} />)
  const farm = screen.getByRole('combobox', { name: 'Farm' })
  expect(farm.disabled).toBe(false)
  fireEvent.change(farm, { target: { value: '2' } })
  expect(screen.getByRole('combobox', { name: 'Zone' }).value).toBe('22')
  expect(screen.queryByRole('option', { name: 'Zone A' })).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: 'Save' }))
  await waitFor(() => expect(save).toHaveBeenCalledWith(expect.objectContaining({ farmId: '2', zoneId: 22, deviceCode: 'NODE-5' })))
})
test('farm without zones clears the old zone and prevents saving', () => {
  const save = vi.fn()
  render(<EquipmentDialog kind="sensor" record={record} workspace={workspace} operations={{ missions: [] }} currentFarm={{ id: 1 }} onSave={save} onClose={() => {}} />)
  fireEvent.change(screen.getByRole('combobox', { name: 'Farm' }), { target: { value: '3' } })
  expect(screen.getByRole('combobox', { name: 'Zone' }).value).toBe('')
  fireEvent.click(screen.getByRole('button', { name: 'Save' }))
  expect(save).not.toHaveBeenCalled()
})
