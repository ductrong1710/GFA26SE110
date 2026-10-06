export default function MissionMonitoringMap({ data }) {
  const { farm, waypoints, points, rows, telemetry, currentWaypoint } = data
  const valid = (point) => point && Number.isFinite(point.latitude) && Number.isFinite(point.longitude)
  const nodes = rows.map(({ node }) => node).filter(valid)
  const route = waypoints.filter(valid)
  const locations = [farm, ...route, ...points, ...nodes, telemetry].filter(valid)
  if (!locations.length) return <p>No route coordinates are configured for this mission.</p>
  const minLat = Math.min(...locations.map(({ latitude }) => latitude)), maxLat = Math.max(...locations.map(({ latitude }) => latitude))
  const minLng = Math.min(...locations.map(({ longitude }) => longitude)), maxLng = Math.max(...locations.map(({ longitude }) => longitude))
  const xy = (point) => [65 + (point.longitude - minLng) / Math.max(maxLng - minLng, 0.001) * 620, 395 - (point.latitude - minLat) / Math.max(maxLat - minLat, 0.001) * 320]
  return <div className="monitor-map"><svg viewBox="0 0 760 480" role="img" aria-label={`Mission monitoring map: ${data.progress.completedWaypoints} completed of ${waypoints.length} waypoints${currentWaypoint ? `, current waypoint ${currentWaypoint.sequenceNo}` : ', current UAV position unavailable'}`}>
    <rect width="760" height="480" rx="14" fill="#eef3eb" />
    {[0, 1, 2, 3, 4, 5].map((index) => <path key={index} d={`M${index * 160 - 70} 0l170 480`} stroke="#dce8d7" strokeWidth="72" />)}
    <path d="M0 280Q240 320 420 210T760 240" stroke="#c8e1e7" strokeWidth="20" fill="none" />
    <polyline points={route.map((point) => xy(point).join(',')).join(' ')} fill="none" stroke="#8c9c92" strokeWidth="3" strokeDasharray="7 6" />
    {route.slice(1).map((point, index) => point.status === 'COMPLETED' && route[index].status === 'COMPLETED' ? <line key={point.id} x1={xy(route[index])[0]} y1={xy(route[index])[1]} x2={xy(point)[0]} y2={xy(point)[1]} stroke="#176346" strokeWidth="5" /> : null)}
    {nodes.map((node) => <g key={node.id} transform={`translate(${xy(node).join(' ')})`}><circle r="5" fill={node.status === 'OFFLINE' ? '#b84940' : '#176346'} stroke="white" strokeWidth="2" /><title>{node.deviceCode} · {node.status}</title></g>)}
    {points.filter(valid).map((point) => <g key={point.id} transform={`translate(${xy(point).join(' ')})`}><path d="M0 -21L21 0 0 21 -21 0Z" stroke="#739c82" strokeWidth="2" fill="#e3f0e6" fillOpacity=".6" /><title>{point.name} · {point.sensorIds.length} sensors</title></g>)}
    {route.map((waypoint) => { const current = currentWaypoint?.id === waypoint.id; return <g key={waypoint.id} transform={`translate(${xy(waypoint).join(' ')})`}><circle r={current ? 15 : 12} fill={current ? '#bb7a17' : waypoint.status === 'COMPLETED' ? '#176346' : '#fff'} stroke={waypoint.status === 'FAILED' ? '#b84940' : '#476250'} strokeWidth="2" /><text textAnchor="middle" dy="4" fontSize="12" fontWeight="700" fill={current || waypoint.status === 'COMPLETED' ? 'white' : '#37513f'}>{waypoint.sequenceNo}</text><title>Waypoint {waypoint.sequenceNo} · {current ? 'Current waypoint' : waypoint.status}</title></g> })}
    {valid(farm) && <g transform={`translate(${xy(farm).join(' ')})`}><path d="M-7 5V-5L0 -11 7 -5V5Z" fill="#334c61" stroke="white" strokeWidth="2" /><text x="12" y="5" fontSize="12" fill="#334c61">Home</text></g>}
    {valid(telemetry) && <g transform={`translate(${xy(telemetry).join(' ')})`}><circle r="25" fill="#e9f2ff" stroke="#5283b5" strokeWidth="2" /><path d="M-10 -10L10 10M-10 10L10 -10" stroke="#285880" strokeWidth="3" />{[[-10, -10], [10, 10], [-10, 10], [10, -10]].map(([x, y]) => <circle key={`${x},${y}`} cx={x} cy={y} r="5" fill="#285880" />)}<circle r="6" fill="#fff" stroke="#285880" strokeWidth="2" /><text x="31" y="5" fontSize="13" fontWeight="700" fill="#285880">{data.uav?.code}</text><title>Current UAV location · Recorded mock telemetry</title></g>}
  </svg><ul className="monitor-map-legend"><li><i className="monitor-line" />Planned route</li><li><i className="monitor-line monitor-line--done" />Completed route</li><li><i className="monitor-dot monitor-dot--done" />Completed waypoint</li><li><i className="monitor-dot monitor-dot--current" />Current waypoint</li><li><i className="monitor-dot" />Remaining waypoint</li><li>◇ Collection point</li><li>● Sensor node</li><li><i className="monitor-dot monitor-dot--uav" />UAV location</li></ul>
    <p className="mission-muted">Illustrative coordinates · Recorded demo snapshot · {telemetry ? 'UAV marker uses the latest recorded position.' : 'No current UAV position is available for this mission.'}</p>
  </div>
}
