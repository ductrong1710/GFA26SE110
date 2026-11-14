export const contactChannels = [
  { id: 'zalo', title: 'Zalo', label: 'Zalo', placeholder: 'https://zalo.me/84901234567' },
  { id: 'messenger', title: 'Messenger', label: 'Messenger', placeholder: 'https://m.me/your.page' },
  { id: 'phone', title: 'Điện thoại', label: 'Gọi điện', placeholder: '+84 901 234 567' },
]

export function safeContact(value) {
  const text = value?.trim() ?? ''
  if (/^tel:\+?[\d ()-]+$/.test(text)) return text
  try {
    const url = new URL(text)
    return url.protocol === 'https:' ? url.href : null
  } catch {
    return null
  }
}

export function contactChannel(row) {
  if (row?.kind !== 'CONTACT') return null
  const href = safeContact(row.body)
  if (href?.startsWith('tel:')) return 'phone'
  if (href) {
    const host = new URL(href).hostname.toLowerCase()
    if (host === 'zalo.me' || host.endsWith('.zalo.me')) return 'zalo'
    if (['m.me', 'messenger.com', 'facebook.com', 'fb.com'].some(domain => host === domain || host.endsWith(`.${domain}`))) return 'messenger'
  }
  const title = row.title?.trim().toLowerCase() ?? ''
  if (title.includes('zalo')) return 'zalo'
  if (title.includes('messenger')) return 'messenger'
  if (['phone', 'hotline', 'điện thoại', 'gọi điện', 'sđt'].some(name => title.includes(name))) return 'phone'
  return null
}

export function findContact(rows, channel) {
  return rows.filter(row => contactChannel(row) === channel).sort((a, b) => b.id - a.id)[0]
}

export function contactUrl(channel, input) {
  const value = input.trim()
  const href = safeContact(channel === 'phone' && !value.startsWith('tel:') ? `tel:${value}` : value)
  if (!href || (channel === 'phone' ? !href.startsWith('tel:') : !href.startsWith('https:'))) return null
  return href
}
