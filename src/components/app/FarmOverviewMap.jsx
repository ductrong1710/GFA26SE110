import { useId } from 'react'
import GeographicFarmMap from './GeographicFarmMap'
import { useUiText } from '../../utils/useUiText'

// Schematic geography only. Domain identity/status and the route come from the farm view model.
const plots = [
  { path: 'M40 42H324L300 262H40Z', x: 72, y: 85, width: 200, height: 125, fill: '#dbe9cb' },
  { path: 'M344 42H592V172H330Z', x: 362, y: 73, width: 194, height: 67, fill: '#e3eee0' },
  { path: 'M328 194H592V326H310Z', x: 353, y: 226, width: 200, height: 65, fill: '#d9e9e7' },
]

export default function FarmOverviewMap({ farm, zones, nodes, mission }) {
  const t = useUiText({
    'Farm geographic map': 'Bản đồ địa lý trang trại',
    'Recorded geographic locations. Records without boundaries appear as center points when available.': 'Các vị trí địa lý đã ghi nhận. Những hồ sơ không có ranh giới sẽ hiển thị dưới dạng điểm trung tâm nếu có.',
    'Select zone': 'Chọn khu vực',
    'Location': 'Vị trí',
    'Farm overview map': 'Bản đồ tổng quan trang trại',
    'Schematic of': 'Sơ đồ',
    'Planned target route shown for': 'Hiển thị lộ trình mục tiêu dự kiến cho',
    'current UAV position is unavailable.': 'không có thông tin vị trí UAV hiện tại.',
    'No mission is running.': 'Không có nhiệm vụ nào đang hoạt động.',
    'Farm schematic · not to scale': 'Sơ đồ trang trại · không theo tỷ lệ',
    'Online sensor': 'Cảm biến trực tuyến',
    'Offline sensor': 'Cảm biến ngoại tuyến',
    'Disabled sensor': 'Cảm biến đã tắt',
    'Active route': 'Lộ trình đang hoạt động',
    'UAV · last collected location': 'UAV · vị trí thu thập gần nhất',
    'N': 'B',
    'Location requires HTTPS or localhost. You can still enter coordinates manually.': 'Tính năng vị trí cần HTTPS hoặc localhost. Bạn vẫn có thể nhập tọa độ thủ công.',
    'This browser does not support location. You can still enter coordinates manually.': 'Trình duyệt này không hỗ trợ xác định vị trí. Bạn vẫn có thể nhập tọa độ thủ công.',
    'Location permission was denied. Allow location in browser settings or enter coordinates manually.': 'Quyền truy cập vị trí đã bị từ chối. Hãy cho phép truy cập vị trí trong cài đặt trình duyệt hoặc nhập tọa độ thủ công.',
    'Location request timed out after two attempts. Check your Internet connection and device location settings, then retry or enter coordinates manually.': 'Yêu cầu xác định vị trí đã hết thời gian sau hai lần thử. Hãy kiểm tra kết nối Internet và cài đặt vị trí trên thiết bị, sau đó thử lại hoặc nhập tọa độ thủ công.',
    'Current location is unavailable. Check your Internet connection and device location settings, then retry or enter coordinates manually.': 'Không thể xác định vị trí hiện tại. Hãy kiểm tra kết nối Internet và cài đặt vị trí trên thiết bị, sau đó thử lại hoặc nhập tọa độ thủ công.',
    'Locating…': 'Đang xác định vị trí…',
    'Current location': 'Vị trí hiện tại',
    'Current coordinates': 'Tọa độ hiện tại',
    'Your location:': 'Vị trí của bạn:',
    'Accuracy: approximately': 'Độ chính xác: khoảng',
    'm.': 'm.',
  })
  const id = useId()
  if (farm?.boundary || zones.some(zone => zone.boundary)) return <GeographicFarmMap {...{ farm, zones, nodes, mission }} text={t} />
  const layout = zones.length <= 3 ? plots : zones.map((_, index) => {
    const rowHeight = 280 / Math.ceil(zones.length / 2)
    const x = 30 + index % 2 * 300
    const y = 30 + Math.floor(index / 2) * rowHeight
    return { path: `M${x} ${y}h275v${rowHeight - 12}h-275Z`, x: x + 15, y: y + 24, width: 245, height: rowHeight - 12, fill: '#dbe9cb' }
  })
  const points = zones.flatMap((zone, zoneIndex) => {
    const plot = layout[zoneIndex]
    const zoneNodes = nodes.filter(({ zoneId }) => zoneId === zone.id)
    return zoneNodes.map((node, index) => ({ ...node,
      x: plot.x + 20 + (index % 4) * (plot.width - 40) / 3,
      y: plot.y + 46 + Math.floor(index / 4) * 19,
    }))
  })
  const route = mission?.targets.map((target) => points.find(({ id: nodeId }) => nodeId === target.sensorNodeId)).filter(Boolean) ?? []
  const completedRoute = route.slice(0, mission?.progress.collectedTargets ?? 0)
  const currentPosition = null
  const coordinates = (items) => items.map(({ x, y }) => `${x},${y}`).join(' ')

  return <div className="owner-map">
    <svg viewBox="0 0 632 360" role="img" aria-labelledby={`${id}-title ${id}-description`}>
      <title id={`${id}-title`}>{t('Farm overview map')}</title>
      <desc id={`${id}-description`}>{t('Schematic of')} {zones.map(({ name }) => name).join(', ')}. {nodes.filter(({ status, isActive }) => status === 'ONLINE' && isActive).length} online, {nodes.filter(({ status, isActive }) => status === 'OFFLINE' && isActive).length} offline, and {nodes.filter(({ isActive }) => !isActive).length} disabled sensors.{mission ? ` ${t('Planned target route shown for')} ${mission.name}; ${t('current UAV position is unavailable.')}` : ` ${t('No mission is running.')}`}</desc>
      <defs>
        <pattern id={`${id}-rows`} width="18" height="18" patternUnits="userSpaceOnUse" patternTransform="rotate(-12)"><path d="M0 0V18" stroke="#b9cfa7" strokeWidth="5" opacity=".45" /></pattern>
        <pattern id={`${id}-grid`} width="24" height="24" patternUnits="userSpaceOnUse"><path d="M24 0H0V24" fill="none" stroke="#d8e2d6" strokeWidth=".7" /></pattern>
      </defs>
      <rect width="632" height="360" rx="12" fill="#f0f3e9" />
      <rect width="632" height="360" rx="12" fill={`url(#${id}-grid)`} />
      <path d="M0 304Q120 273 230 310T632 340" stroke="#bdd9e2" strokeWidth="22" fill="none" />
      <path d="M332 0L295 285M310 183H632" stroke="#fffdfa" strokeWidth="17" fill="none" />
      {zones.map((zone, index) => {
        const plot = layout[index]
        const [name, description] = zone.name.split(' — ')
        return <g key={zone.id}>
          <path d={plot.path} fill={plot.fill} stroke="#b4c8af" strokeWidth="1.5" />
          <path d={plot.path} fill={`url(#${id}-rows)`} />
          <text x={plot.x} y={plot.y} className="owner-map-zone">{name}</text>
          <text x={plot.x} y={plot.y + 18} className="owner-map-crop">{description}</text>
        </g>
      })}
      {route.length > 0 && <polyline points={coordinates(route)} fill="none" stroke="#739686" strokeWidth="2" strokeDasharray="6 6" />}
      {completedRoute.length > 0 && <polyline points={coordinates(completedRoute)} fill="none" stroke="#176346" strokeWidth="3" strokeLinejoin="round" />}
      {points.map((point) => <g key={point.id}>
        <title>{point.deviceCode}: {!point.isActive ? 'Disabled' : point.status === 'ONLINE' ? 'Online' : 'Offline'}</title>
        <circle cx={point.x} cy={point.y} r="8" fill="white" />
        <circle cx={point.x} cy={point.y} r="5" fill={!point.isActive ? '#718078' : point.status === 'ONLINE' ? '#176346' : '#be4940'} />
        {point.status === 'OFFLINE' && <path d={`M${point.x - 2} ${point.y - 2}l4 4m0-4-4 4`} stroke="white" strokeWidth="1.2" />}
      </g>)}
      {currentPosition && <g transform={`translate(${currentPosition.x},${currentPosition.y - 20})`}>
        <circle r="19" fill="#176346" opacity=".12" /><circle r="13" fill="#176346" stroke="white" strokeWidth="2" />
        <path d="M-7-7L7 7M7-7L-7 7" stroke="white" strokeWidth="2" />
        {[-1, 1].flatMap((x) => [-1, 1].map((y) => <circle key={`${x}-${y}`} cx={x * 6} cy={y * 6} r="3" stroke="white" fill="#176346" strokeWidth="1.5" />))}
      </g>}
      <g transform="translate(607 22)"><path d="M0 23V4m-4 7 4-7 4 7" stroke="#60796b" fill="none" /><text y="0" textAnchor="middle" className="owner-map-crop">{t('N')}</text></g>
      <text x="40" y="340" className="owner-map-crop">{t('Farm schematic · not to scale')}</text>
    </svg>
    <div className="owner-map-legend"><span><i />{t('Online sensor')}</span><span><i className="is-offline" />{t('Offline sensor')}</span>{nodes.some(({ isActive }) => !isActive) && <span><i className="is-disabled" />{t('Disabled sensor')}</span>}{mission && <><span><i className="is-route" />{t('Active route')}</span><span><i className="is-uav" />{t('UAV · last collected location')}</span></>}</div>
  </div>
}
