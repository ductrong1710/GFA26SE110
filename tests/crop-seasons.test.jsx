import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import CropSeasonsPage from '../src/pages/app/CropSeasonsPage'
import CropSeasonForm from '../src/components/app/CropSeasonForm'
import { api } from '../src/services/api'
import { PERMISSIONS as P } from '../src/config/permissions'

const auth = vi.hoisted(() => ({ user: { id: 4 }, can: () => true }))
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => auth }))
afterEach(() => { cleanup(); vi.restoreAllMocks(); auth.can = () => true })
const season = { id: 1, ownerId: 4, farmId: 2, zoneId: null, revision: 3, status: 'ACTIVE', name: 'Spring', crop: 'Rice', cropAgeDays: 0 }
test('creating a season uses creation feedback and preserves unknown optional values', async () => {
  const saved = vi.fn()
  vi.spyOn(api, 'get').mockImplementation(async path => ({ items: path === '/api/farms' ? [{ id: 2, name: 'Rice farm', isActive: true }] : [], pagination: { totalPages: 1 } }))
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') return season
    return { items: path === '/api/farms' ? [{ id: 2, name: 'Rice farm', isActive: true }] : [], pagination: { totalPages: 1 } }
  })
  render(<CropSeasonForm onSaved={saved} onCancel={vi.fn()} />)
  await screen.findByRole('option', { name: 'Rice farm' })
  fireEvent.change(screen.getByLabelText('Farm'), { target: { value: '2' } })
  fireEvent.change(screen.getByLabelText('Season name'), { target: { value: 'Spring' } })
  fireEvent.change(screen.getByLabelText('Crop'), { target: { value: 'Rice' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save season' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalledWith(season))
  expect(request).toHaveBeenCalledWith('/api/crop-seasons', expect.objectContaining({ method: 'POST',
    toastMessage: 'Crop season created successfully.', body: expect.objectContaining({ farmId: 2, expectedRevision: 0, areaHectares: null }) }))
})
function mockSeasons() {
  return vi.spyOn(api, 'request').mockImplementation(async path => path.endsWith('/1') ? season : { items: [season], pagination: { totalPages: 1 } })
}
test('editing preserves zero and unknown values and submits the loaded revision', async () => {
  const request = mockSeasons()
  render(<CropSeasonsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open season #1' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Edit season' }))
  fireEvent.change(screen.getByLabelText('Season name'), { target: { value: 'Early spring' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save season' }))
  await screen.findByText('Season saved.')
  expect(request).toHaveBeenCalledWith('/api/crop-seasons/1', { method: 'PUT', body: {
    expectedRevision: 3, farmId: 2, zoneId: null, name: 'Early spring', crop: 'Rice', cropAgeDays: 0,
    areaHectares: null, startDate: null, expectedEndDate: null, notes: null,
  } })
})
test('read-only viewer can open details without receiving mutation controls', async () => {
  auth.can = permission => permission === P.CROP_SEASONS_VIEW
  mockSeasons()
  render(<CropSeasonsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open season #1' }))
  await screen.findByText('Whole farm')
  expect(screen.queryByRole('button', { name: 'Create season' })).toBeNull()
  expect(screen.queryByRole('button', { name: 'Edit season' })).toBeNull()
})
test('owner completes a season with unknown yield and zero revenue', async () => {
  const request = mockSeasons()
  render(<CropSeasonsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open season #1' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Complete season' }))
  fireEvent.change(screen.getByLabelText('Revenue (VND)'), { target: { value: '0' } })
  request.mockResolvedValueOnce({ ...season, status: 'COMPLETED', revision: 4, revenueVnd: 0 })
  fireEvent.click(screen.getByRole('button', { name: 'Confirm completion' }))
  await screen.findByText('COMPLETED')
  expect(screen.queryByRole('button', { name: 'Complete season' })).toBeNull()
  expect(request).toHaveBeenCalledWith('/api/crop-seasons/1/complete', { method: 'POST', body: {
    expectedRevision: 3, endDate: null, yieldKg: null, totalCostVnd: null, revenueVnd: 0, notes: null,
  } })
})
test('comparison requests both selected seasons and displays the activity breakdown', async () => {
  const rows = [season, { ...season, id: 8, name: 'Autumn' }]
  const request = vi.spyOn(api, 'request').mockImplementation(async path => path.includes('compare?')
    ? rows.map(row => ({ ...row, yieldKg: 0, profitVnd: null, activities: [{ type: 'WATERING', count: 2 }] })) : { items: rows, pagination: { totalPages: 1 } })
  render(<CropSeasonsPage />)
  fireEvent.click(await screen.findByLabelText('Compare season #8'))
  expect(screen.queryByRole('heading', { name: 'Season comparison' })).toBeNull()
  fireEvent.click(screen.getByLabelText('Compare season #1'))
  expect(await screen.findAllByText('WATERING (2)')).toHaveLength(2)
  expect(screen.getAllByRole('cell', { name: '0', exact: true })).toHaveLength(2)
  expect(screen.getAllByRole('cell', { name: 'No data recorded for this criterion.' }).length).toBeGreaterThan(0)
  expect(request.mock.calls.some(([path]) => path === '/api/crop-seasons/compare?ids=8&ids=1')).toBe(true)
  fireEvent.click(screen.getByRole('button', { name: 'Clear selection' }))
  expect(screen.queryByRole('heading', { name: 'Season comparison' })).toBeNull()
})
test('owner records an activity and opens its saved evidence in the refreshed log', async () => {
  const activity = { id: 9, date: '2026-03-20', type: 'INSPECTION', description: 'Leaves healthy', imageCount: 1 }
  let recorded = false
  const image = { contentType: 'image/png', dataBase64: 'AQ==' }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options) => {
    if (path.endsWith('/images')) return [image]
    if (path.endsWith('/activities') && options.method === 'POST') { recorded = true; return { ...season, revision: 4 } }
    if (path.endsWith('/activities')) return { items: recorded ? [activity] : [], pagination: { totalPages: 1 } }
    return path.endsWith('/1') ? season : { items: [season], pagination: { totalPages: 1 } }
  })
  render(<CropSeasonsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open season #1' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Record activity' }))
  fireEvent.change(screen.getByLabelText('Activity date'), { target: { value: activity.date } })
  fireEvent.change(screen.getByLabelText('Activity type'), { target: { value: activity.type } })
  fireEvent.change(screen.getByLabelText('Description'), { target: { value: activity.description } })
  fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files: [new File([new Uint8Array([1])], 'plant.png', { type: 'image/png' })] } })
  await screen.findByRole('button', { name: 'Remove image 1' })
  fireEvent.click(screen.getByRole('button', { name: 'Save activity' }))
  fireEvent.click(await screen.findByRole('button', { name: 'View images for activity #9' }))
  expect(await screen.findByRole('img', { name: 'Evidence image 1' })).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/crop-seasons/1/activities', { method: 'POST', toastMessage: 'Crop activity recorded successfully.', body: {
    expectedRevision: 3, date: activity.date, type: activity.type, description: activity.description, costVnd: null, notes: null, images: [image],
  } })
})
