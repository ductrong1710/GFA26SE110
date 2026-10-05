import Icon from '../Icon'

const paths = {
  database: <><ellipse cx="12" cy="5" rx="8" ry="3" /><path d="M4 5v14c0 4 16 4 16 0V5M4 12c0 4 16 4 16 0" /></>,
  mission: <><circle cx="5" cy="6" r="2" /><circle cx="19" cy="18" r="2" /><path d="M7 6h8a4 4 0 0 1 0 8H9a2 2 0 0 0 0 4h8" /></>,
  users: <><circle cx="9" cy="8" r="3" /><path d="M3 21v-2a6 6 0 0 1 12 0v2M16 5a3 3 0 0 1 0 6m2 4a5 5 0 0 1 3 4v2" /></>,
  logout: <><path d="M10 4H4v16h6m4-12 4 4-4 4m-6-4h12" /></>,
}

export default function AppIcon({ name, size = 20 }) {
  return paths[name]
    ? <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>
    : <Icon name={name} size={size} />
}
