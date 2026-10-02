import { useState } from 'react'
import Icon from './Icon'

// Add the official contact details here once supplied by the project owner.
const contacts = {
  zalo: '',
  messenger: '',
  phone: '',
}

const channelInfo = [
  { id: 'zalo', label: 'Zalo', value: contacts.zalo, icon: 'chat' },
  { id: 'messenger', label: 'Messenger', value: contacts.messenger, icon: 'chat' },
  { id: 'phone', label: 'Gọi điện', value: contacts.phone ? `tel:${contacts.phone}` : '', icon: 'phone' },
]

export default function ConsultationButton() {
  const [open, setOpen] = useState(false)
  return (
    <div className="consultation-control">
      <button className="consultation-trigger" type="button" aria-expanded={open} aria-controls="consultation-options" onClick={() => setOpen((value) => !value)}>
        Tư vấn thiết bị <Icon name={open ? 'close' : 'arrow'} size={15} />
      </button>
      {open && <div className="consultation-popover" id="consultation-options"><strong>Chọn kênh tư vấn</strong>{channelInfo.map((channel) => channel.value ? <a href={channel.value} key={channel.id} target={channel.id === 'phone' ? undefined : '_blank'} rel="noreferrer">{channel.label}<Icon name="arrow" size={13} /></a> : <span className="consultation-pending" key={channel.id}>{channel.label}<small>Chưa cập nhật</small></span>)}</div>}
    </div>
  )
}
