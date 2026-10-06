import { useId } from 'react'

export default function FarmZoneMap({ zones, selectedZoneId, onSelect, nodes }) {
  const patternId = useId()
  const rows = Math.ceil(zones.length / 2)
  return <div className="farm-zone-map">
    <svg viewBox={`0 0 660 ${Math.max(250, rows * 160 + 40)}`} role="group" aria-label="Farm zones schematic">
      <defs><pattern id={patternId} width="18" height="18" patternUnits="userSpaceOnUse" patternTransform="rotate(-15)"><path d="M0 0V18" stroke="#bdd2b4" strokeWidth="5" opacity=".5" /></pattern></defs>
      {zones.map((zone, index) => {
        const x = 25 + index % 2 * 320
        const y = 25 + Math.floor(index / 2) * 160
        const zoneNodes = nodes.filter(({ zoneId }) => zoneId === zone.id)
        return <g key={zone.id} role="button" tabIndex={0} aria-label={`Select ${zone.name}`} aria-pressed={zone.id === selectedZoneId} onClick={() => onSelect(zone.id)} onKeyDown={(event) => { if (['Enter', ' '].includes(event.key)) { event.preventDefault(); onSelect(zone.id) } }} className="farm-map-zone">
          <rect x={x} y={y} width="290" height="130" rx="12" fill={zone.id === selectedZoneId ? '#dcebd4' : '#edf2e5'} stroke={zone.id === selectedZoneId ? '#176346' : '#b6c6ae'} strokeWidth={zone.id === selectedZoneId ? 3 : 1} />
          <rect x={x} y={y} width="290" height="130" rx="12" fill={`url(#${patternId})`} />
          <text x={x + 18} y={y + 30} className="farm-map-name">{zone.name.length > 30 ? `${zone.name.slice(0, 29)}…` : zone.name}</text>
          <text x={x + 18} y={y + 54} className="farm-map-caption">{zone.areaHectares} ha · {zoneNodes.length} sensors</text>
          {zoneNodes.slice(0, 12).map((node, nodeIndex) => <circle key={node.id} cx={x + 25 + nodeIndex % 8 * 32} cy={y + 84 + Math.floor(nodeIndex / 8) * 22} r="6" fill={node.status === 'ONLINE' ? '#176346' : '#b42318'} stroke="white" strokeWidth="2"><title>{node.deviceCode}: {node.status}</title></circle>)}
        </g>
      })}
      {!zones.length && <text x="330" y="125" textAnchor="middle" className="farm-map-caption">No zones in this farm yet</text>}
    </svg>
    <p>Illustrative boundaries · Green: online · Red: offline · Select a zone to inspect it</p>
  </div>
}
