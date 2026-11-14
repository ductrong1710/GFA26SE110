import { cleanup, fireEvent, render as renderComponent, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, test, vi } from 'vitest'
import CropSeasonsPage from '../src/pages/app/CropSeasonsPage'
import CropSeasonForm from '../src/components/app/CropSeasonForm'
import { api } from '../src/services/api'
import { PERMISSIONS as P } from '../src/config/permissions'

const auth = vi.hoisted(() => ({ user: { id: 4 }, can: () => true }))
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => auth }))
afterEach(() => { cleanup(); vi.restoreAllMocks(); auth.can = () => true })
function render(component) { return renderComponent(<MemoryRouter>{component}</MemoryRouter>) }
const season = { id: 1, ownerId: 4, farmId: 2, zoneId: null, revision: 3, status: 'ACTIVE', name: 'Spring', crop: 'Rice', cropAgeDays: 0 }

test('reply refreshes the embedded report when report and season have the same id', async () => {
  let report = { id: 1, farmId: 2, cropSeasonId: 1, crop: 'Rice', status: 'NEW', reporterId: 4, revision: 1,
    allowedActions: [], canReadEvidence: true, images: [], updates: [], sensorChannelIds: [] }
  vi.spyOn(api, 'get').mockImplementation(async path => ({ items: path === '/api/farms' ? [{ id: 2, name: 'Rice farm', isActive: true }] : [], pagination: { totalPages: 1 } }))
  vi.spyOn(api, 'request').mockImplementation(async path => {
    if (path === '/api/crop-condition-reports' || path === '/api/crop-condition-reports/1') return report
    if (path.endsWith('/reply')) {
      report = { ...report, status: 'IN_REVIEW', revision: 4, allowedActions: [], updates: [
        { id: 4, action: 'OWNER_REPLIED', content: 'Watered yesterday', userId: 4, createdAt: '2026-11-01T12:00:00Z' },
      ] }
      return report
    }
    if (path === '/api/crop-seasons/1') return season
    return { items: path === '/api/crop-seasons' ? [season] : [], pagination: { totalPages: 1 } }
  })
  render(<CropSeasonsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open season #1' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Report abnormal crop condition' }))
  await screen.findByRole('option', { name: 'Rice farm' })
  fireEvent.change(screen.getByLabelText('Symptoms'), { target: { value: 'Yellow leaves' } })
  fireEvent.change(screen.getByLabelText('Observed at (local time)'), { target: { value: '2026-11-01T10:00' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Report crop condition' }))
  await screen.findByText('Crop report submitted. The season remains unchanged.')
  report = { ...report, status: 'AWAITING_INFORMATION', revision: 3, allowedActions: ['reply'], engineerId: 7 }
  fireEvent.click(screen.getByRole('button', { name: 'Reload report' }))
  fireEvent.change(await screen.findByLabelText('Consultation note'), { target: { value: 'Watered yesterday' } })
  fireEvent.click(screen.getByRole('button', { name: 'Send additional information' }))
  await vi.waitFor(() => expect(screen.queryAllByLabelText('Consultation note')).toHaveLength(0))
  expect(screen.getAllByRole('heading', { name: 'Crop report #1' })).toHaveLength(1)
  expect(await screen.findByText('Watered yesterday')).toBeTruthy()
})

test('owner opens a crop report from a completed season with existing context', async () => {
  const completed = { ...season, status: 'COMPLETED' }
  vi.spyOn(api, 'get').mockImplementation(async path => ({ items: path === '/api/farms' ? [{ id: 2, name: 'Rice farm', isActive: true }] : [], pagination: { totalPages: 1 } }))
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options) => {
    if (options?.method === 'POST') return { id: 44, farmId: 2, cropSeasonId: 1, crop: 'Rice', status: 'NEW', reporterId: 4, revision: 1, allowedActions: [], canReadEvidence: true, images: [], updates: [] }
    return path === '/api/crop-seasons/1' ? completed : { items: path.endsWith('/activities') ? [] : [completed], pagination: { totalPages: 1 } }
  })
  render(<CropSeasonsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open season #1' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Report abnormal crop condition' }))
  await screen.findByRole('option', { name: 'Rice farm' })
  expect(screen.getByLabelText('Farm').value).toBe('2')
  expect(screen.getByLabelText('Crop season (optional)').value).toBe('1')
  expect(screen.getByLabelText('Crop').value).toBe('Rice')
  fireEvent.change(screen.getByLabelText('Symptoms'), { target: { value: 'Stored crop issue' } })
  fireEvent.change(screen.getByLabelText('Observed at (local time)'), { target: { value: '2026-11-01T10:00' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Report crop condition' }))
  await screen.findByText('Crop report submitted. The season remains unchanged.')
  expect(request).toHaveBeenCalledWith('/api/crop-condition-reports', expect.objectContaining({ body: expect.objectContaining({ cropSeasonId: 1, farmId: 2, zoneId: null, crop: 'Rice' }) }))
  expect(request.mock.calls.some(([path]) => path.endsWith('/complete'))).toBe(false)
})
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
