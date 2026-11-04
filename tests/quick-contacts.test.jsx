import React from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AuthContext } from '../src/context/AuthContext'
import { api } from '../src/services/api'
import FloatingContactDock from '../src/components/FloatingContactDock'
import QuickContactSettings from '../src/components/workflow/QuickContactSettings'

vi.mock('../src/services/api', () => ({ api: { request: vi.fn(), post: vi.fn() } }))
beforeEach(() => vi.resetAllMocks())
afterEach(cleanup)
const contact = (id, title, body, extra = {}) => ({ id, kind: 'CONTACT', title, body, revision: 3, published: true, ...extra })

it('does not show icons for unconfigured contacts', () => {
  render(<FloatingContactDock />)
  expect(screen.queryByRole('complementary')).toBeNull()
})

it('uses published configuration for icon links and applies changed phone/page URLs on refresh', () => {
  const initial = [contact(1, 'Zalo', 'https://zalo.me/84901234567'), contact(2, 'Messenger', 'https://m.me/old.page'), contact(3, 'Phone', 'tel:+84901234567')]
  const { rerender } = render(<FloatingContactDock contacts={initial} />)
  expect(screen.getByRole('link', { name: 'Zalo' }).getAttribute('href')).toBe(initial[0].body)
  expect(screen.getByRole('link', { name: 'Messenger' }).getAttribute('target')).toBe('_blank')
  expect(screen.getByRole('link', { name: 'Gọi điện' }).getAttribute('target')).toBeNull()
  rerender(<FloatingContactDock contacts={[initial[0], contact(2, 'Messenger', 'https://m.me/new.page'), contact(3, 'Phone', 'tel:+84987654321')]} />)
  expect(screen.getByRole('link', { name: 'Messenger' }).getAttribute('href')).toBe('https://m.me/new.page')
  expect(screen.getByRole('link', { name: 'Gọi điện' }).getAttribute('href')).toBe('tel:+84987654321')
})

it('does not render icons with unsafe URLs', () => {
  render(<FloatingContactDock contacts={[contact(1, 'Zalo', 'javascript:alert(1)'), contact(2, 'Messenger', 'http://m.me/page')]} />)
  expect(screen.queryAllByRole('link')).toHaveLength(0)
  expect(screen.queryByRole('complementary')).toBeNull()
})

it('lets admin update an existing phone number with revision checking', async () => {
  const reload = vi.fn()
  api.request.mockResolvedValue({ id: 3, revision: 4, published: true })
  render(<AuthContext.Provider value={{ activeRole: 'ADMINISTRATOR' }}>
    <QuickContactSettings query={{ data: [contact(3, 'Phone', 'tel:+84901234567')], reload }} />
  </AuthContext.Provider>)
  fireEvent.click(screen.getByText('Phone', { exact: true }))
  fireEvent.change(screen.getByLabelText('Phone'), { target: { value: '+84 987 654 321' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save Phone' }))
  await waitFor(() => expect(api.request).toHaveBeenCalledWith('/api/admin/public-content/3', expect.objectContaining({ method: 'PUT', body: expect.objectContaining({ kind: 'CONTACT', title: 'Phone', body: 'tel:+84 987 654 321', expectedRevision: 3 }) })))
  expect(api.post).not.toHaveBeenCalled()
  expect(reload).toHaveBeenCalled()
})

it('creates and publishes a configured Messenger link without code changes', async () => {
  const reload = vi.fn()
  api.request.mockResolvedValue({ id: 8, revision: 1, published: false })
  api.post.mockResolvedValue({ id: 8, revision: 2, published: true })
  render(<QuickContactSettings query={{ data: [], reload }} />)
  fireEvent.click(screen.getByText('Messenger', { exact: true }))
  fireEvent.change(screen.getByLabelText('Messenger'), { target: { value: 'https://m.me/our.page' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save Messenger' }))
  await waitFor(() => expect(api.post).toHaveBeenCalledWith('/api/admin/public-content/8/publish', { expectedRevision: 1 }))
  expect(reload).toHaveBeenCalled()
})

it('hides the homepage icon when a contact is unpublished and shows it again when published', () => {
  const row = contact(2, 'Messenger', 'https://m.me/our.page')
  const { rerender } = render(<FloatingContactDock contacts={[row]} />)
  expect(screen.getByRole('link', { name: 'Messenger' })).toBeTruthy()
  rerender(<FloatingContactDock contacts={[]} />)
  expect(screen.queryByRole('link', { name: 'Messenger' })).toBeNull()
  rerender(<FloatingContactDock contacts={[{ ...row, published: false }]} />)
  expect(screen.queryByRole('link', { name: 'Messenger' })).toBeNull()
  rerender(<FloatingContactDock contacts={[row]} />)
  expect(screen.getByRole('link', { name: 'Messenger' })).toBeTruthy()
})

it('provides an immediate Hide action independent of unsaved URL edits', async () => {
  const reload = vi.fn()
  api.post.mockResolvedValue(contact(2, 'Messenger', 'https://m.me/our.page', { published: false, revision: 4 }))
  render(<QuickContactSettings query={{ data: [contact(2, 'Messenger', 'https://m.me/our.page')], reload }} />)
  fireEvent.click(screen.getByText('Messenger', { exact: true }))
  expect(screen.queryByText('Messenger URL')).toBeNull()
  fireEvent.change(screen.getByLabelText('Messenger'), { target: { value: '' } })
  fireEvent.click(screen.getByRole('button', { name: 'Hide Messenger' }))
  await waitFor(() => expect(api.post).toHaveBeenCalledWith('/api/admin/public-content/2/unpublish', { expectedRevision: 3 }))
  expect(api.request).not.toHaveBeenCalled()
  expect(reload).toHaveBeenCalled()
  expect(screen.getByRole('button', { name: 'Show Messenger' })).toBeTruthy()
})
