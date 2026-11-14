import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render as renderComponent, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ManagementContext } from '../src/context/ManagementContext'
import UsersPage from '../src/pages/app/UsersPage'

vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ activeRole: 'ADMINISTRATOR', canAny: () => true }) }))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})
function render(component) { return renderComponent(<MemoryRouter>{component}</MemoryRouter>) }

describe('Users page role selection', () => {
  it.each([['AGRICULTURAL_ENGINEER'], ['TECHNICIAN']])('submits the selected role %s with the Save button', async (selectedRoles) => {
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
    expect(owner.checked).toBe(true)
    expect(engineer.checked).toBe(false)

    const technician = screen.getByRole('radio', { name: 'Technician' })
    if (selectedRoles.includes('TECHNICIAN')) fireEvent.click(technician)
    if (selectedRoles.includes('AGRICULTURAL_ENGINEER')) fireEvent.click(engineer)
    expect(owner.checked).toBe(selectedRoles.includes('FARM_OWNER'))
    expect(technician.checked).toBe(selectedRoles.includes('TECHNICIAN'))
    expect(engineer.checked).toBe(selectedRoles.includes('AGRICULTURAL_ENGINEER'))

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Test User' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'test@example.com' } })
    fireEvent.change(screen.getByLabelText('Initial password'), { target: { value: 'Test-password-123!' } })
    expect(screen.getByRole('button', { name: 'Save', exact: true })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Save User', exact: true })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Save', exact: true }))

    await waitFor(() => expect(changeManagement).toHaveBeenCalledWith(expect.objectContaining({
      values: expect.objectContaining({ roles: [selectedRoles] }),
    })))
  })
})
