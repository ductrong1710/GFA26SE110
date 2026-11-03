import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import ConsultationBinding from '../src/components/app/ConsultationBinding'
import { api } from '../src/services/api'
import * as resources from '../src/services/api/resources'

afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('links only a selected current farm member and reports the saved binding', async () => {
  vi.spyOn(resources, 'listAll').mockImplementation(async path => path === '/api/farms'
    ? [{ id: 8, name: 'Rice farm' }] : [{ userId: 3, fullName: 'Owner', email: 'owner@example.com', roles: ['FarmOwner'] }])
  const request = vi.spyOn(api, 'request').mockResolvedValue({ farmId: 8, ownerId: 3 })
  const onSaved = vi.fn()
  render(<ConsultationBinding consultation={{ id: 42 }} onSaved={onSaved} />)
  await screen.findByRole('option', { name: 'Rice farm' })
  fireEvent.change(screen.getByRole('combobox', { name: 'Customer farm' }), { target: { value: '8' } })
  await screen.findByRole('option', { name: 'Owner · owner@example.com' })
  fireEvent.change(screen.getByLabelText('Farm owner'), { target: { value: '3' } })
  fireEvent.click(screen.getByRole('button', { name: 'Confirm customer farm' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/consultations/42/customer-farm', { method: 'PUT', body: { farmId: 8, ownerId: 3 } }))
  expect(onSaved).toHaveBeenCalled()
})
