import React from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { AuthContext } from '../src/context/AuthContext'
import { api } from '../src/services/api'
import PublicContentPage from '../src/pages/app/PublicContentPage'

const mocks = vi.hoisted(() => ({ rows: [], reload: vi.fn() }))
vi.mock('../src/services/api', () => ({ api: { request: vi.fn(), post: vi.fn() } }))
vi.mock('../src/services/api/useApiQuery', () => ({ useApiQuery: () => ({ data: mocks.rows, reload: mocks.reload, loading: false, error: null }) }))
function mount(userId = 1) {
  return render(<AuthContext.Provider value={{ activeRole: 'ADMINISTRATOR', user: { id: userId } }}><PublicContentPage /></AuthContext.Provider>)
}
const field = title => screen.getByLabelText(title)
function fill() {
  fireEvent.change(field('Content type'), { target: { value: 'DEVICE' } })
  fireEvent.change(field('Name / title'), { target: { value: 'Light sensor' } })
  fireEvent.change(field('Description / specifications / terms'), { target: { value: 'Outdoor light readings' } })
  fireEvent.change(field('Monthly rent (VND)'), { target: { value: '50000' } })
  fireEvent.change(field('Replacement price per unit (VND)'), { target: { value: '250000' } })
}
beforeEach(() => { sessionStorage.clear(); vi.resetAllMocks(); mocks.rows = [] })
afterEach(cleanup)

it('restores unsaved fields and the selected cover image after remounting', async () => {
  const page = mount()
  fill()
  fireEvent.change(field(/Cover photo \(first image only\)/), { target: { files: [new File(['image'], 'cover.png', { type: 'image/png' })] } })
  await screen.findByRole('img', { name: 'Cover photo (first image only) 1' })
  page.unmount()
  mount()
  expect(field('Content type').value).toBe('DEVICE')
  expect(field('Name / title').value).toBe('Light sensor')
  expect(field('Description / specifications / terms').value).toBe('Outdoor light readings')
  expect(field('Monthly rent (VND)').value).toBe('50000')
  expect(screen.getByRole('img').getAttribute('src')).toBe('data:image/png;base64,aW1hZ2U=')
})

it('clears all fields including content type and the stored draft only after successful save', async () => {
  api.request.mockResolvedValue({ id: 1 })
  mount()
  fill()
  fireEvent.click(screen.getByRole('button', { name: 'Save content' }))
  await waitFor(() => expect(screen.queryByLabelText('Name / title')).toBeNull())
  expect(field('Content type').value).toBe('')
  expect(field('Content type').required).toBe(true)
  expect(screen.queryByLabelText('Description / specifications / terms')).toBeNull()
  expect(screen.queryByLabelText('Monthly rent (VND)')).toBeNull()
  expect(sessionStorage.getItem('smartfarm.public-content.draft:1')).toBeNull()
  cleanup()
  mount()
  expect(field('Content type').value).toBe('')
  expect(screen.queryByLabelText('Name / title')).toBeNull()
})

it('retains the draft when saving fails and isolates it from another account', async () => {
  api.request.mockRejectedValue(new Error('Unable to save'))
  mount()
  fill()
  fireEvent.click(screen.getByRole('button', { name: 'Save content' }))
  await screen.findByText('Unable to save')
  expect(field('Name / title').value).toBe('Light sensor')
  cleanup()
  mount(2)
  expect(screen.queryByLabelText('Name / title')).toBeNull()
  cleanup()
  mount(1)
  expect(field('Name / title').value).toBe('Light sensor')
})

it('does not clear an unsaved form when publishing another content entry', async () => {
  mocks.rows = [{ id: 12, kind: 'DEVICE', title: 'Existing sensor', body: 'Saved', revision: 2, published: false }]
  api.post.mockResolvedValue({})
  mount()
  fill()
  fireEvent.click(screen.getByRole('button', { name: 'Publish' }))
  await waitFor(() => expect(mocks.reload).toHaveBeenCalled())
  expect(field('Name / title').value).toBe('Light sensor')
  expect(field('Content type').value).toBe('DEVICE')
})

it('opens the stacked contact editor only after choosing Quick contacts', () => {
  mocks.rows = [{ id: 8, kind: 'CONTACT', title: 'Zalo', body: 'https://zalo.me/84901234567', revision: 1, published: true }]
  mount()
  expect(screen.queryByRole('region', { name: 'Quick contact buttons' })).toBeNull()
  const list = screen.getByRole('region', { name: 'Content list' })
  expect(within(list).queryByText('Zalo')).toBeNull()
  fireEvent.change(field('Content type'), { target: { value: 'CONTACT' } })
  expect(screen.queryByRole('region', { name: 'Content list' })).toBeNull()
  expect(screen.queryByLabelText('Name / title')).toBeNull()
  expect(screen.queryByRole('button', { name: 'Save content' })).toBeNull()
  expect(screen.getByLabelText('Zalo').value).toBe('https://zalo.me/84901234567')
  const stack = screen.getByRole('region', { name: 'Quick contact buttons' })
  expect(stack.querySelectorAll('details')).toHaveLength(3)
  expect(stack.querySelectorAll('details[open]')).toHaveLength(0)
  fireEvent.click(within(stack).getByText('Zalo', { exact: true }))
  expect(within(stack).getByRole('button', { name: 'Save Zalo' })).toBeTruthy()
  expect(stack.querySelector('form form')).toBeNull()
})

it('shows only the selector until a supported content type is selected', () => {
  mount()
  expect(screen.queryByLabelText('Name / title')).toBeNull()
  expect(screen.queryByRole('button', { name: 'Save content' })).toBeNull()
  for (const name of ['Parts', 'Device return', 'Quick contact']) {
    expect(within(field('Content type')).queryByRole('option', { name })).toBeNull()
  }
  fireEvent.change(field('Content type'), { target: { value: 'POLICY' } })
  expect(field('Name / title')).toBeTruthy()
  expect(screen.queryByLabelText(/Cover photo/)).toBeNull()
  expect(screen.queryByLabelText('Monthly rent (VND)')).toBeNull()
  expect(screen.getByRole('button', { name: 'Save content' })).toBeTruthy()
})

it('clears rental-only values when switching to a policy and never submits hidden prices or images', async () => {
  api.request.mockResolvedValue({ id: 5 })
  mount()
  fill()
  fireEvent.change(field('Content type'), { target: { value: 'POLICY' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save content' }))
  await waitFor(() => expect(api.request).toHaveBeenCalledWith('/api/admin/public-content', expect.objectContaining({
    body: expect.objectContaining({ kind: 'POLICY', estimatedMonthlyRentVnd: null, replacementPriceVnd: null, image: null }),
  })))
})

it('groups existing warranty, maintenance and return articles into one Policies category', () => {
  mocks.rows = ['WARRANTY', 'MAINTENANCE', 'RETURN', 'POLICY'].map((kind, i) => ({ id: i + 1, kind, title: `Policy ${i}`, body: 'Text terms', revision: 1 }))
  mount()
  fireEvent.change(field('Content type'), { target: { value: 'POLICY' } })
  const selector = within(field('Content type'))
  expect(selector.getByRole('option', { name: 'Policies' })).toBeTruthy()
  expect(selector.queryByRole('option', { name: 'Warranty' })).toBeNull()
  expect(selector.queryByRole('option', { name: 'Maintenance' })).toBeNull()
  const list = within(screen.getByRole('region', { name: 'Content list' }))
  expect(list.getAllByRole('button', { name: 'Edit' })).toHaveLength(4)
  fireEvent.click(list.getAllByRole('button', { name: 'Edit' })[2])
  expect(field('Content type').value).toBe('POLICY')
  expect(field('Name / title').value).toBe('Policy 2')
  expect(screen.queryByLabelText(/Cover photo/)).toBeNull()
})
