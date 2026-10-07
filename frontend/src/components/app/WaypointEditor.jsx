import { DataTable } from './ui'
import { makePlannedWaypoints, WAYPOINT_ACTIONS } from '../../data/mock/missionPlanning'

// Route planning data only; these actions never issue UAV flight commands.
export default function WaypointEditor({ farm, points, waypoints, onChange }) {
  const update = ({ waypoints: next }) => onChange(next)
  const changeWaypoint = (id, values) => onChange(waypoints.map((waypoint) => waypoint.id === id ? { ...waypoint, ...values } : waypoint))
  const moveWaypoint = (index, direction) => {
    const next = [...waypoints]
    ;[next[index], next[index + direction]] = [next[index + direction], next[index]]
    onChange(next)
  }
  return <><p>GPS: latitude / longitude. RELATIVE: east / north meters from farm home. Altitude is in meters (0–120 in this demo; landing must be 0). These are planned actions only.</p>
        <div className="mission-actions"><button className="app-ui-button" onClick={() => update({ waypoints: makePlannedWaypoints(farm, points) })}>Generate Route from Points</button><button className="app-ui-button" onClick={() => { const waypoints = [...waypoints]; waypoints.splice(Math.max(0, waypoints.length - 2), 0, { id: Math.max(0, ...waypoints.map(({ id }) => id)) + 1, coordinateType: 'GPS', x: farm.latitude, y: farm.longitude, altitude: 30, action: 'MOVE', pointId: '' }); update({ waypoints }) }}>Add Waypoint</button></div>
        <DataTable caption="Waypoint editor" rows={waypoints} columns={[
          { key: 'sequence', header: 'Sequence', render: (_, row) => waypoints.indexOf(row) + 1 },
          { key: 'coordinateType', header: 'Coordinate Type', render: (value, row) => <select aria-label={`Waypoint ${row.id} coordinate type`} value={value} onChange={(event) => changeWaypoint(row.id, { coordinateType: event.target.value, x: event.target.value === 'GPS' ? farm.latitude : 0, y: event.target.value === 'GPS' ? farm.longitude : 0 })}><option>GPS</option><option>RELATIVE</option></select> },
          { key: 'coordinates', header: 'Coordinates', render: (_, row) => <div className="mission-coordinate">{['x', 'y'].map((key) => <label key={key}>{row.coordinateType === 'GPS' ? key === 'x' ? 'Latitude' : 'Longitude' : key === 'x' ? 'East (m)' : 'North (m)'}<input aria-label={`Waypoint ${row.id} ${key}`} type="number" step="any" value={row[key]} onChange={(event) => changeWaypoint(row.id, { [key]: event.target.value })} /></label>)}</div> },
          { key: 'altitude', header: 'Altitude', render: (value, row) => <input className="mission-altitude" aria-label={`Waypoint ${row.id} altitude`} type="number" value={value} onChange={(event) => changeWaypoint(row.id, { altitude: event.target.value })} /> },
          { key: 'action', header: 'Action', render: (value, row) => <div><select aria-label={`Waypoint ${row.id} action`} value={value} onChange={(event) => changeWaypoint(row.id, { action: event.target.value, ...(event.target.value === 'LAND' ? { altitude: 0 } : {}) })}>{WAYPOINT_ACTIONS.map((action) => <option key={action}>{action}</option>)}</select>{value === 'COLLECT_SENSOR' && <select aria-label={`Waypoint ${row.id} collection point`} value={row.pointId} onChange={(event) => { const point = points.find(({ id }) => id === Number(event.target.value)); changeWaypoint(row.id, { pointId: Number(event.target.value), ...(point ? { coordinateType: 'GPS', x: point.latitude, y: point.longitude } : {}) }) }}><option value="">Select point</option>{points.map((point) => <option key={point.id} value={point.id}>{point.name}</option>)}</select>}</div> },
          { key: 'order', header: 'Reorder / Remove', render: (_, row) => { const index = waypoints.indexOf(row); return <div className="mission-actions"><button className="app-ui-button" aria-label={`Move waypoint ${index + 1} up`} disabled={index === 0} onClick={() => moveWaypoint(index, -1)}>↑</button><button className="app-ui-button" aria-label={`Move waypoint ${index + 1} down`} disabled={index === waypoints.length - 1} onClick={() => moveWaypoint(index, 1)}>↓</button><button className="app-ui-button" onClick={() => update({ waypoints: waypoints.filter(({ id }) => id !== row.id) })}>Remove</button></div> } },
        ]} /></>
}
