import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import SurveyPanel from '../src/components/app/SurveyPanel'
import { api } from '../src/services/api'
import FarmSurveys from '../src/components/app/FarmSurveys'

afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('opens linked surveys from a farm without exposing survey editing', async () => {
  vi.spyOn(api, 'request').mockImplementation(async path => path.includes('/survey-consultations')
    ? { items: [{ id: 42, serviceNeed: 'Sensors' }], pagination: { totalItems: 1 } }
    : { items: [{ id: 3, revision: 1, farmName: 'Rice farm' }], pagination: { totalItems: 1 } })
  render(<FarmSurveys farmId={8} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Consultation #42: Sensors' }))
  await screen.findByRole('button', { name: 'View revision 1' })
  expect(screen.queryByRole('button', { name: 'Revise latest survey' })).toBeNull()
  expect(screen.queryByRole('button', { name: 'Record survey' })).toBeNull()
})

it('records structured survey information without inventing crop age or farm area', async () => {
  const request = vi.spyOn(api, 'request').mockImplementation(async (_, options = {}) => options.method === 'POST'
    ? { id: 3, revision: 1, ...options.body } : { items: [], pagination: { totalItems: 0 } })
  render(<SurveyPanel consultationId={42} editable />)
  fireEvent.click(await screen.findByRole('button', { name: 'Record survey' }))
  for (const [label, value] of [['Farm name', 'Rice farm'], ['Farm location', 'Can Tho'], ['Monitoring needs', 'Soil moisture'],
    ['Zone name', 'North'], ['Crop', 'Rice'], ['Proposed installation location', 'Canal edge']])
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files: [new File(['survey photo'], 'farm.png', { type: 'image/png' })] } })
  await screen.findByAltText('Evidence image 1')
  fireEvent.click(screen.getByRole('button', { name: 'Save survey revision' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/consultations/42/surveys', expect.objectContaining({
    method: 'POST', body: expect.objectContaining({ expectedRevision: 0, farmArea: null, images: [{ contentType: 'image/png', dataBase64: btoa('survey photo') }],
      zones: [expect.objectContaining({ cropAgeDays: null, installationLocation: 'Canal edge' })] }),
  })))
  expect(await screen.findByText('Survey revision 1 saved.')).toBeTruthy()
  expect(screen.getByAltText('Evidence image 1').getAttribute('src')).toBe(`data:image/png;base64,${btoa('survey photo')}`)
  expect(screen.queryByLabelText('Add evidence images')).toBeNull()
})

it('keeps draft values when a concurrent revision requires reload', async () => {
  vi.spyOn(api, 'request').mockImplementation(async (_, options = {}) => {
    if (options.method === 'POST') throw new Error('Survey changed. Reload the latest revision.')
    return { items: [], pagination: { totalItems: 0 } }
  })
  render(<SurveyPanel consultationId={42} editable />)
  fireEvent.click(await screen.findByRole('button', { name: 'Record survey' }))
  fireEvent.change(screen.getByLabelText('Farm name'), { target: { value: 'Unsaved farm' } })
  fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files: [new File(['unsaved evidence'], 'farm.png', { type: 'image/png' })] } })
  await screen.findByAltText('Evidence image 1')
  fireEvent.submit(screen.getByRole('form', { name: 'Survey revision' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('Reload'))
  expect(screen.getByLabelText('Farm name').value).toBe('Unsaved farm')
  expect(screen.getByAltText('Evidence image 1').getAttribute('src')).toBe(`data:image/png;base64,${btoa('unsaved evidence')}`)
})
