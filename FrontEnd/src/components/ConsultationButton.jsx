import { Link } from 'react-router-dom'
import Icon from './Icon'

export default function ConsultationButton({ className = 'consultation-trigger' }) {
  return <Link className={className} to="/tu-van">Tư vấn thiết bị <Icon name="arrow" size={15} /></Link>
}
