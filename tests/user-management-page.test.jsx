import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ManagementContext } from '../src/context/ManagementContext'
import UsersPage from '../src/pages/app/UsersPage'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe('Users page role selection', () => {
  it('hides administrator accounts from the user directory', () => {
    render(<ManagementContext.Provider value={{
      management: { users: [
        { id: 1, fullName: 'Hidden Admin', email: 'admin@example.com', roles: ['ADMINISTRATOR'], status: 'ACTIVE' },
        { id: 2, fullName: 'Visible Owner', email: 'owner@example.com', roles: ['FARM_OWNER'], status: 'ACTIVE', updatedAt: null },
        { id: 3, fullName: 'Recently Updated', email: 'updated@example.com', roles: ['FARM_OWNER'], status: 'ACTIVE', updatedAt: '2026-10-28T08:30:00Z' },
      ] },
      changeManagement: vi.fn(),
      loading: false,
      error: null,
      reload: vi.fn(),
    }}>
      <UsersPage />
    </ManagementContext.Provider>)

    expect(screen.queryByText('Hidden Admin')).toBeNull()
    expect(screen.queryByText('admin@example.com')).toBeNull()
    expect(screen.getByText('Visible Owner')).toBeTruthy()
    expect(screen.getByText('Updated')).toBeTruthy()
    expect(screen.getByText('Not updated')).toBeTruthy()
    expect(screen.getByText('Recently Updated')).toBeTruthy()
    expect(screen.getByText('28 Oct 2026, 15:30')).toBeTruthy()
    expect(screen.getByText(/2 users · Page 1 of 1/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Assign Role' })).toBeNull()
    expect(screen.queryByText('User directory updated.')).toBeNull()
  })

  it('paginates the filtered non-admin directory ten users at a time', () => {
    const users = Array.from({ length: 11 }, (_, index) => ({
      id: index + 1,
      fullName: `User ${index + 1}`,
      email: `user${index + 1}@example.com`,
      roles: ['FARM_OWNER'],
      status: 'ACTIVE',
    }))
    render(<ManagementContext.Provider value={{
      management: { users },
      changeManagement: vi.fn(),
      loading: false,
      error: null,
      reload: vi.fn(),
    }}>
      <UsersPage />
    </ManagementContext.Provider>)

    expect(screen.getByText('User 1')).toBeTruthy()
    expect(screen.getByText('User 10')).toBeTruthy()
    expect(screen.queryByText('User 11')).toBeNull()
    expect(screen.getByText(/11 users · Page 1 of 2/)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Previous' }).disabled).toBe(true)

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.queryByText('User 1')).toBeNull()
    expect(screen.getByText('User 11')).toBeTruthy()
    expect(screen.getByText(/11 users · Page 2 of 2/)).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Previous' }))
    expect(screen.getByText('User 1')).toBeTruthy()
  })

  it('allows only one assignable role and submits it with the Save button', async () => {
    const changeManagement = vi.fn().mockResolvedValue(undefined)
    HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
    HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }

    render(<ManagementContext.Provider value={{
      management: { users: [] },
      changeManagement,
      loading: false,
      error: null,
      reload: vi.fn(),
    }}>
      <UsersPage />
    </ManagementContext.Provider>)

    fireEvent.click(screen.getByRole('button', { name: '+ Add User' }))
    const owner = screen.getByRole('radio', { name: 'Farm Owner' })
    const engineer = screen.getByRole('radio', { name: 'Agricultural Engineer' })
    const password = screen.getByLabelText('Initial password')
    expect(owner.checked).toBe(true)
    expect(engineer.checked).toBe(false)
    expect(password.minLength).toBe(6)

    fireEvent.click(engineer)
    expect(owner.checked).toBe(false)
    expect(engineer.checked).toBe(true)

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Test User' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'test@example.com' } })
    fireEvent.change(password, { target: { value: '123456' } })
    expect(screen.getByRole('button', { name: 'Save', exact: true })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Save User', exact: true })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Save', exact: true }))

    await waitFor(() => expect(changeManagement).toHaveBeenCalledWith(expect.objectContaining({
      values: expect.objectContaining({ roles: ['AGRICULTURAL_ENGINEER'] }),
    })))
    expect(screen.queryByText('User directory updated.')).toBeNull()
  })

  it('updates a user role from the edit dialog without a separate assign-role action', async () => {
    const changeManagement = vi.fn().mockResolvedValue(undefined)
    HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
    HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }

    render(<ManagementContext.Provider value={{
      management: { users: [
        { id: 2, fullName: 'Visible Owner', email: 'owner@example.com', roles: ['FARM_OWNER'], status: 'ACTIVE' },
      ] },
      changeManagement,
      loading: false,
      error: null,
      reload: vi.fn(),
    }}>
      <UsersPage />
    </ManagementContext.Provider>)

    expect(screen.queryByRole('button', { name: 'Assign role to Visible Owner' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Edit Visible Owner' }))
    const engineer = screen.getByRole('radio', { name: 'Agricultural Engineer' })
    fireEvent.click(engineer)
    fireEvent.click(screen.getByRole('button', { name: 'Save', exact: true }))

    await waitFor(() => expect(changeManagement).toHaveBeenCalledWith(expect.objectContaining({
      kind: 'user',
      id: 2,
      mode: 'edit',
      values: expect.objectContaining({ roles: ['AGRICULTURAL_ENGINEER'] }),
    })))
  })
})
