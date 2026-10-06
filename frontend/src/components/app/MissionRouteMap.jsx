import { waypointPosition } from '../../data/mock/missionPlanning'

export default function MissionRouteMap({ farm, points = [], nodes = [], waypoints = [] }) {
  const route = waypoints.map((waypoint) => waypointPosition(waypoint, farm)).filter(Boolean)
  const valid = (point) => Number.isFinite(Number(point.latitude)) && Number.isFinite(Number(point.longitude))
  const all = [farm, ...points, ...nodes, ...route].filter((point) => point && valid(point))
  if (!all.length) return <p>Select a farm to preview the planning map.</p>
  const lat = all.map((point) => Number(point.latitude)), lng = all.map((point) => Number(point.longitude))
  const minLat = Math.min(...lat), minLng = Math.min(...lng)
  const scale = Math.max(Math.max(...lat) - minLat, Math.max(...lng) - minLng, 0.002)
  const xy = (point) => [60 + (Number(point.longitude) - minLng) / scale * 580, 310 - (Number(point.latitude) - minLat) / scale * 240]
  return <div className="mission-map"><svg viewBox="0 0 700 360" role="img" aria-label="Planned collection route map">
    <rect width="700" height="360" rx="16" fill="#edf3ec" />
    {[0, 1, 2, 3, 4].map((i) => <path key={i} d={`M${i * 160} 0 L${i * 160 + 100} 360`} stroke="#d8e5d5" strokeWidth="45" />)}
    <path d="M0 160 Q300 210 700 80" fill="none" stroke="#c1dce6" strokeWidth="16" />
    {route.length > 1 && <polyline points={route.map((point) => xy(point).join(',')).join(' ')} fill="none" stroke="#176346" strokeWidth="3" strokeDasharray="7 5" />}
    {nodes.filter(valid).map((node) => <circle key={node.id} cx={xy(node)[0]} cy={xy(node)[1]} r="5" fill={node.status === 'OFFLINE' ? '#b94a48' : '#5e956e'}><title>{node.deviceCode}</title></circle>)}
    {points.filter(valid).map((point, index) => <g key={point.id} transform={`translate(${xy(point).join(' ')})`}><circle r="13" fill="#176346" stroke="white" strokeWidth="2" /><text textAnchor="middle" dy="4" fill="white" fontSize="12">{index + 1}</text><title>{point.name}</title></g>)}
    {farm && valid(farm) && <g transform={`translate(${xy(farm).join(' ')})`}><rect x="-8" y="-8" width="16" height="16" fill="#274257" stroke="white" strokeWidth="2" /><text x="13" y="-12" fill="#274257" fontSize="13">Home</text></g>}
  </svg><p>■ Farm home · ● Sensors · Numbered collection points · Dashed planned route</p><small>Illustrative planning map. Route actions describe a plan; they do not operate a UAV.</small></div>
}
