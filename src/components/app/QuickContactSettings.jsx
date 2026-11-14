import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { contactChannels, contactUrl, findContact } from '../../utils/quickContacts'

function ContactEditor({ channel, record, reload }) {
  const [value, setValue] = useState(record?.body ?? '')
  const [published, setPublished] = useState(record?.published ?? false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const href = contactUrl(channel.id, value)

  async function save(event) {
    event.preventDefault()
    if (saving) return
    if (!href) {
      setError(!value.trim()
        ? (published ? 'Vui lòng nhập đường dẫn trước khi hiển thị trên trang công khai.' : 'Vui lòng nhập đường dẫn liên hệ trước khi lưu.')
        : 'Đường dẫn chưa hợp lệ. Vui lòng kiểm tra lại.')
      setNotice('')
      return
    }
    setSaving(true)
    setError('')
    setNotice('')
    try {
      const result = await api.request(`/api/admin/public-contacts${record ? `/${record.id}` : ''}`, {
        method: record ? 'PUT' : 'POST',
        body: { kind: 'CONTACT', title: channel.title, body: href, expectedRevision: record?.revision ?? 0,
          estimatedMonthlyRentVnd: null, image: null, removeImage: false },
      })
      if (result.published !== published) {
        await api.post(`/api/admin/public-contacts/${result.id}/${published ? 'publish' : 'unpublish'}`, { expectedRevision: result.revision })
      }
      setNotice(published ? 'Đã lưu và hiển thị liên hệ trên trang công khai.' : 'Đã lưu liên hệ ở chế độ ẩn.')
      reload()
    } catch (cause) {
      setError(cause.message)
      reload()
    } finally {
      setSaving(false)
    }
  }

  return <article className={`quick-contact-card${published ? ' is-published' : ''}`}>
    <header className="quick-contact-card-heading">
      <h3>{channel.title}</h3>
      <span className={published ? 'is-published' : 'is-draft'}>{published ? 'Đang hiển thị' : 'Chưa hiển thị'}</span>
    </header>
    <form onSubmit={save}>
      <label className="quick-contact-url-label">{channel.id === 'phone' ? 'Số điện thoại' : `Đường dẫn ${channel.title}`}
        <input aria-label={channel.title} type={channel.id === 'phone' ? 'tel' : 'url'} maxLength={4000}
          placeholder={channel.placeholder} value={value} onChange={event => setValue(event.target.value)} />
      </label>
      <label className="management-check quick-contact-publish">
        <input type="checkbox" checked={published} onChange={event => setPublished(event.target.checked)} />
        Hiển thị trên trang công khai
      </label>
      {error && <p className="quick-contact-message is-error" role="alert">{error}</p>}
      {notice && <p className="quick-contact-message is-success" role="status">{notice}</p>}
      <button className="app-ui-button app-ui-button--primary" type="submit" disabled={saving}>
        {saving ? 'Đang lưu…' : 'Lưu'}
      </button>
    </form>
  </article>
}

export default function QuickContactSettings() {
  const query = useApiQuery('admin-quick-contacts', signal => api.request('/api/admin/public-contacts', { signal }))
  return <section className="quick-contact-management" aria-labelledby="quick-contact-heading">
    <header className="quick-contact-management-heading">
      <h2 id="quick-contact-heading">Liên hệ nhanh trên Homepage</h2>
      <p>Cập nhật Zalo, Messenger và số điện thoại. Bật hiển thị rồi lưu để công khai biểu tượng liên hệ.</p>
    </header>
    {query.loading && <p role="status">Đang tải liên hệ…</p>}
    {query.error && <p role="alert">Chưa tải được liên hệ. <button type="button" onClick={query.reload}>Thử lại</button></p>}
    {!query.loading && !query.error && <div className="quick-contact-grid">
      {contactChannels.map(channel => {
        const record = findContact(query.data ?? [], channel.id)
        return <ContactEditor key={channel.id} channel={channel} record={record} reload={query.reload} />
      })}
    </div>}
  </section>
}
