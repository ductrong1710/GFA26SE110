import { useState } from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import EvidenceImages from '../src/components/app/EvidenceImages'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
function Editor() {
  const [images, setImages] = useState([])
  return <EvidenceImages images={images} onChange={setImages} />
}
it('rejects unsupported, empty, oversized and excessive selections before reading', () => {
  const read = vi.spyOn(FileReader.prototype, 'readAsDataURL')
  render(<Editor />)
  for (const files of [
    [new File(['<svg/>'], 'image.svg', { type: 'image/svg+xml' })],
    [new File([], 'empty.png', { type: 'image/png' })],
    [new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'large.png', { type: 'image/png' })],
    Array.from({ length: 6 }, () => new File(['image'], 'image.png', { type: 'image/png' })),
  ]) {
    fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files } })
    expect(screen.getByRole('alert')).toBeTruthy()
  }
  expect(read).not.toHaveBeenCalled()
})
it('previews selected evidence and removes it from the draft', async () => {
  render(<Editor />)
  fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files: [new File(['photo'], 'image.jpg', { type: 'image/jpeg' })] } })
  expect((await screen.findByAltText('Evidence image 1')).getAttribute('src')).toBe(`data:image/jpeg;base64,${btoa('photo')}`)
  fireEvent.click(screen.getByRole('button', { name: 'Remove image 1' }))
  expect(screen.queryByAltText('Evidence image 1')).toBeNull()
})
it('renders saved evidence without editing and refuses active content types', () => {
  render(<EvidenceImages images={[{ contentType: 'image/png', dataBase64: btoa('photo') }, { contentType: 'image/svg+xml', dataBase64: btoa('<svg/>') }]} />)
  expect(screen.getAllByRole('img')).toHaveLength(1)
  expect(screen.getByRole('link', { name: 'Download evidence image 1' }).getAttribute('download')).toBe('evidence-1.png')
  expect(screen.queryByLabelText('Add evidence images')).toBeNull()
  expect(screen.queryByRole('button')).toBeNull()
  expect(screen.getByText('Image unavailable')).toBeTruthy()
})
