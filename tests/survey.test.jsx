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
  await screen.findByRole('button', { name: 'Xem phiên bản 1' })
  expect(screen.queryByRole('button', { name: 'Chỉnh sửa bản khảo sát mới nhất' })).toBeNull()
  expect(screen.queryByRole('button', { name: 'Ghi nhận khảo sát' })).toBeNull()
})

it('records structured survey information without inventing crop age or farm area', async () => {
  const request = vi.spyOn(api, 'request').mockImplementation(async (_, options = {}) => options.method === 'POST'
    ? { id: 3, revision: 1, ...options.body } : { items: [], pagination: { totalItems: 0 } })
  render(<SurveyPanel consultationId={42} editable />)
  fireEvent.click(await screen.findByRole('button', { name: 'Ghi nhận khảo sát' }))
  for (const [label, value] of [['Tên nông trại', 'Rice farm'], ['Địa điểm nông trại', 'Can Tho'], ['Nhu cầu giám sát', 'Soil moisture'],
    ['Tên khu vực', 'North'], ['Cây trồng', 'Rice'], ['Vị trí lắp đặt đề xuất', 'Canal edge']])
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  fireEvent.change(screen.getByLabelText('Thêm ảnh minh chứng'), { target: { files: [new File(['survey photo'], 'farm.png', { type: 'image/png' })] } })
  await screen.findByAltText('Ảnh minh chứng 1')
  fireEvent.click(screen.getByRole('button', { name: 'Lưu phiên bản khảo sát' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/consultations/42/surveys', expect.objectContaining({
    method: 'POST', toastMessage: 'Đã lưu khảo sát nông trại.', body: expect.objectContaining({ expectedRevision: 0, farmArea: null, images: [{ contentType: 'image/png', dataBase64: btoa('survey photo') }],
      zones: [expect.objectContaining({ cropAgeDays: null, installationLocation: 'Canal edge' })] }),
  })))
  expect(await screen.findByText('Đã lưu bản khảo sát số 1.')).toBeTruthy()
  expect(screen.getByAltText('Ảnh minh chứng 1').getAttribute('src')).toBe(`data:image/png;base64,${btoa('survey photo')}`)
  expect(screen.queryByLabelText('Thêm ảnh minh chứng')).toBeNull()
})

it('keeps draft values when a concurrent revision requires reload', async () => {
  vi.spyOn(api, 'request').mockImplementation(async (_, options = {}) => {
    if (options.method === 'POST') throw new Error('Survey changed. Reload the latest revision.')
    return { items: [], pagination: { totalItems: 0 } }
  })
  render(<SurveyPanel consultationId={42} editable />)
  fireEvent.click(await screen.findByRole('button', { name: 'Ghi nhận khảo sát' }))
  fireEvent.change(screen.getByLabelText('Tên nông trại'), { target: { value: 'Unsaved farm' } })
  fireEvent.change(screen.getByLabelText('Thêm ảnh minh chứng'), { target: { files: [new File(['unsaved evidence'], 'farm.png', { type: 'image/png' })] } })
  await screen.findByAltText('Ảnh minh chứng 1')
  fireEvent.submit(screen.getByRole('form', { name: 'Phiên bản khảo sát' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('Reload'))
  expect(screen.getByLabelText('Tên nông trại').value).toBe('Unsaved farm')
  expect(screen.getByAltText('Ảnh minh chứng 1').getAttribute('src')).toBe(`data:image/png;base64,${btoa('unsaved evidence')}`)
})
