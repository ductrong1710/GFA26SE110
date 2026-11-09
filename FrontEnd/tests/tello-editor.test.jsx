import React from 'react'
import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import WaypointEditor from '../src/components/app/WaypointEditor'
import MissionRouteMap from '../src/components/app/MissionRouteMap'
import { allowedMissionStatuses } from '../src/services/api/monitoring'

afterEach(cleanup)
test('Tello editor generates local collection actions at one meter without GPS', () => {
  const change = vi.fn()
  render(<WaypointEditor tello farm={{ latitude: 10, longitude: 106 }} points={[{ id: 1, x: 1, y: 0, sensorIds: [2] }]} waypoints={[]} onChange={change} />)
  fireEvent.click(screen.getByText('Generate Route from Points'))
  expect(change).toHaveBeenCalledWith([expect.objectContaining({ coordinateType: 'RELATIVE', x: 1, y: 0, altitude: 1, action: 'COLLECT', holdSeconds: 0 })])
})
test('local preview preserves forward/right coordinates without a geographic map', () => {
  render(<MissionRouteMap tello farm={{ latitude: 10, longitude: 106 }} waypoints={[{ id: 1, x: 2, y: -1, action: 'COLLECT' }]} />)
  expect(screen.getByText(/forward 2 m, right -1 m/)).toBeTruthy()
  expect(screen.getByText(/Return to origin/)).toBeTruthy()
  expect(screen.queryByRole('img')).toBeNull()
})
test('Tello must start on gateway; generic UAV retains human status flow', () => {
  const mission = { status: 'SCHEDULED', uavId: 1 }
  expect(allowedMissionStatuses(mission, { uavs: [{ id: 1, model: 'DJI Tello' }] })).toEqual(['CANCELLED'])
  expect(allowedMissionStatuses(mission, { uavs: [{ id: 1, model: 'Other' }] })).toContain('RUNNING')
})
