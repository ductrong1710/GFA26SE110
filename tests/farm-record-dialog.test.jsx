import { afterEach, beforeAll, expect, test, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import FarmRecordDialog from '../src/components/app/FarmRecordDialog'

vi.mock('../src/components/app/BoundaryEditorMap', () => ({
  default: ({ onValidityChange, onChange }) => <>
    <button type="button" onClick={() => onValidityChange(false)}>Invalid boundary</button>
    <button type="button" onClick={() => { onValidityChange(true); onChange({ type: 'Polygon', coordinates: [[[106, 10], [107, 10], [107, 11], [106, 11], [106, 10]]] }) }}>Valid boundary</button>
  </>,
}))
beforeAll(() => {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }
})
afterEach(cleanup)

test('farm dialog saves an existing center without removed mock/delete variables', async () => {
  const save = vi.fn().mockResolvedValue(undefined)
  render(<FarmRecordDialog kind="farm" record={{ name: 'Farm', latitude: 10, longitude: 106 }} farms={[]} onSave={save} onClose={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Save' }))
  await waitFor(() => expect(save).toHaveBeenCalledWith(expect.objectContaining({ name: 'Farm', latitude: 10, longitude: 106, boundary: null })))
})
test('invalid boundary blocks save and a valid boundary supplies its center', async () => {
  const save = vi.fn().mockResolvedValue(undefined)
  render(<FarmRecordDialog kind="farm" record={{ name: 'Farm' }} farms={[]} onSave={save} onClose={vi.fn()} />)
  fireEvent.click(screen.getByText('Invalid boundary'))
  expect(screen.getByRole('button', { name: 'Save' }).disabled).toBe(true)
  fireEvent.click(screen.getByText('Valid boundary'))
  fireEvent.click(screen.getByRole('button', { name: 'Save' }))
  await waitFor(() => expect(save).toHaveBeenCalledWith(expect.objectContaining({ latitude: 10.5, longitude: 106.5, boundary: expect.objectContaining({ type: 'Polygon' }) })))
})
