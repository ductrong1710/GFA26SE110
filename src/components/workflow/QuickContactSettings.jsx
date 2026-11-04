import { useState } from 'react'
import { api } from '../../services/api'
import { contactChannels, contactUrl, findContact } from '../../utils/quickContacts'
import { Button, State, useAction } from './WorkflowUi'

function ContactForm({ channel, record, onDone }) {
  const [current, setCurrent] = useState(record)
  const [url, setUrl] = useState(record?.body ?? '')
  const [saved, setSaved] = useState(false)
  const action = useAction(onDone)
  const href = contactUrl(channel.id, url)
  const save = event => {
    event.preventDefault()
    if (!href) return
    action.run(async () => {
      setSaved(false)
      const result = await api.request(`/api/admin/public-content${current ? `/${current.id}` : ''}`, {
        method: current ? 'PUT' : 'POST',
        body: { kind: 'CONTACT', title: channel.title, body: href, expectedRevision: current?.revision ?? 0,
          estimatedMonthlyRentVnd: null, replacementPriceVnd: null, image: null, removeImage: false },
      })
      setCurrent(result)
      const published = current?.published ?? true
      if (result.published !== published) setCurrent(await api.post(`/api/admin/public-content/${result.id}/${published ? 'publish' : 'unpublish'}`, { expectedRevision: result.revision }))
      setSaved(true)
    })
  }
  return <form className="wf-form" onSubmit={save}>
    <div className="wf-field"><input aria-label={channel.title} required type={channel.id === 'phone' ? 'tel' : 'url'}
      maxLength={4000} placeholder={channel.placeholder} value={url} onChange={event => { setUrl(event.target.value); setSaved(false) }} /></div>
    {url.trim() && !href && <p role="alert" className="wf-error">{channel.id === 'phone' ? 'Enter a valid phone number.' : 'Enter a valid HTTPS link.'}</p>}
    {action.errorView}{saved && <p role="status">Contact saved.</p>}
    <div className="wf-toolbar"><Button type="submit" primary disabled={!href || action.busy}>{action.busy ? 'Saving…' : `Save ${channel.title}`}</Button>
      {current && <Button disabled={action.busy} aria-label={`${current.published ? 'Hide' : 'Show'} ${channel.title}`} onClick={() => action.run(async () => {
        const result = await api.post(`/api/admin/public-content/${current.id}/${current.published ? 'unpublish' : 'publish'}`, { expectedRevision: current.revision })
        setCurrent(result); setSaved(false)
      })}>{current.published ? 'Hide' : 'Show'}</Button>}
    </div>
  </form>
}

export default function QuickContactSettings({ query }) {
  return <section className="wf-contact-settings" aria-label="Quick contact buttons">
    <p className="wf-muted">Open a channel to update its link or phone number.</p>
    <State query={query}><div className="wf-contact-stack">{contactChannels.map(channel => {
      const record = findContact(query.data ?? [], channel.id)
      return <details name="quick-contact-settings" className="wf-contact-item" key={channel.id}>
        <summary><strong>{channel.title}</strong><span>{record?.published ? 'Enabled' : 'Not enabled'}</span></summary>
        <ContactForm key={`${record?.id ?? 'new'}:${record?.revision ?? 0}`} channel={channel} record={record} onDone={query.reload} />
      </details>
    })}</div></State>
  </section>
}
