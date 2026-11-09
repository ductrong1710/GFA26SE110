import { useState } from 'react'

const channels = [
  { id: 'zalo', label: 'Zalo', href: '' },
  { id: 'messenger', label: 'Messenger', href: '' },
  { id: 'phone', label: 'Gọi điện', href: '' },
]

function ContactIcon({ type }) {
  if (type === 'zalo') return <svg viewBox="0 0 48 48" aria-hidden="true"><path d="M24 2.5C11.7 2.5 3 11.1 3 22.1c0 5.5 2.2 10.1 6.2 13.3v7.2l7.3-3.8c2.3.7 4.8 1.1 7.5 1.1 12.2 0 21-8.2 21-19.2S36.2 2.5 24 2.5Z" fill="#0866FF"/><path d="M25 5.7c-10.8 0-18.1 7-18.1 16.2 0 4.4 1.6 8 4.7 10.7l1.6 1.4-.2 3.3 4-2.1 1.2.3c2 .6 4.2.9 6.8.9 10.6 0 18.1-6.7 18.1-15.8S35.6 5.7 25 5.7Z" fill="white"/><text x="24" y="26" fill="#0866FF" fontFamily="Arial, sans-serif" fontSize="11.5" fontWeight="600" textAnchor="middle">Zalo</text></svg>
  if (type === 'messenger') return <svg viewBox="0 0 48 48" aria-hidden="true"><defs><linearGradient id="messengerGradient" x1="7" y1="4" x2="41" y2="45" gradientUnits="userSpaceOnUse"><stop stopColor="#FF6A5E"/><stop offset=".48" stopColor="#C43DFF"/><stop offset="1" stopColor="#087BFF"/></linearGradient></defs><circle cx="24" cy="24" r="24" fill="url(#messengerGradient)"/><path d="M24 10.5c-8 0-14 5.8-14 13.3 0 4 1.8 7.5 4.8 9.9v5l4.9-2.7c1.4.4 2.8.6 4.3.6 8 0 14-5.7 14-13.2S32 10.5 24 10.5Z" fill="white"/><path d="m16.5 27.7 6.2-6.7 4.2 3.4 4.6-3.4-6.2 6.7-4.3-3.4-4.5 3.4Z" fill="#6952E8"/></svg>
  return <svg viewBox="0 0 48 48" aria-hidden="true"><circle cx="24" cy="24" r="24" fill="#08B95A"/><path d="M16.1 11.8c.7-.7 2.3-.9 3.2-.4l3 4.3c.5.7.5 1.7-.1 2.4l-1.7 2.1a23.2 23.2 0 0 0 7.3 7.3l2.1-1.7c.7-.6 1.7-.6 2.4-.1l4.3 3c.9.6.9 2.2.4 3.2l-1.2 2.2c-.9 1.6-2.8 2.4-4.6 1.9C20.8 32.7 15.3 27.2 12 16.8c-.6-1.8.2-3.7 1.9-4.6l2.2-1.2Z" fill="white"/></svg>
}

export default function FloatingContactDock() {
  const [notice, setNotice] = useState('')
  return (
    <aside className="floating-contact-dock" aria-label="Liên hệ Smart Farm">
      {channels.map((channel) => channel.href ? <a className={`floating-contact-button ${channel.id}`} href={channel.href} key={channel.id} aria-label={channel.label} title={channel.label} target={channel.id === 'phone' ? undefined : '_blank'} rel="noreferrer"><ContactIcon type={channel.id} /></a> : <button className={`floating-contact-button ${channel.id}`} type="button" key={channel.id} aria-label={channel.label} title={channel.label} onClick={() => setNotice((current) => current === channel.label ? '' : channel.label)}><ContactIcon type={channel.id} /></button>)}
      {notice && <div className="floating-contact-notice" role="status">Thông tin {notice} đang được cập nhật.</div>}
    </aside>
  )
}
