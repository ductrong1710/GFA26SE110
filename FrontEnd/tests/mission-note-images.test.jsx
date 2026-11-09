import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import MissionNoteImages from '../src/components/app/MissionNoteImages'
import { api } from '../src/services/api'

vi.mock('../src/services/api', () => ({ api: { get: vi.fn() } }))
afterEach(() => { cleanup(); vi.resetAllMocks() })

test('mission evidence is loaded only when opened and rendered read-only', async () => {
  api.get.mockResolvedValue([{ contentType: 'image/png', dataBase64: btoa('photo') }])
  render(<MissionNoteImages missionId={7} noteId={11} count={1} />)
  expect(api.get).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole('button', { name: 'View 1 evidence image' }))
  expect(await screen.findByAltText('Evidence image 1')).toBeTruthy()
  expect(api.get.mock.calls[0][0]).toBe('/api/missions/7/notes/11/images')
  expect(screen.queryByLabelText('Add evidence images')).toBeNull()
})

test('failed evidence loads expose an error instead of fabricated images', async () => {
  api.get.mockRejectedValue(new Error('Access denied'))
  render(<MissionNoteImages missionId={7} noteId={11} count={1} />)
  fireEvent.click(screen.getByRole('button', { name: 'View 1 evidence image' }))
  expect(await screen.findByText('Access denied')).toBeTruthy()
  expect(screen.queryByRole('img')).toBeNull()
})
