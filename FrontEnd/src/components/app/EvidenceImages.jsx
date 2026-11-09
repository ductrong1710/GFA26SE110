import { useEffect, useRef, useState } from 'react'

const maxBytes = 2 * 1024 * 1024
const imageTypes = ['image/png', 'image/jpeg']

export default function EvidenceImages({ images = [], maxImages = 5, onChange, onBusyChange }) {
  const [error, setError] = useState('')
  const [reading, setReading] = useState(false)
  const active = useRef(true)
  const reader = useRef(null)
  useEffect(() => {
    active.current = true
    return () => { active.current = false; reader.current?.abort() }
  }, [])

  async function add(event) {
    const files = Array.from(event.target.files ?? [])
    event.target.value = ''
    if (!files.length || reading) return
    setError('')
    if (images.length + files.length > maxImages) { setError(`Choose up to ${maxImages} evidence image(s).`); return }
    if (files.some(file => !imageTypes.includes(file.type) || file.size === 0 || file.size > maxBytes)) {
      setError('Choose non-empty PNG or JPEG images, up to 2 MiB each.'); return
    }
    setReading(true); onBusyChange?.(true)
    try {
      const added = []
      for (const file of files) {
        const dataBase64 = await new Promise((resolve, reject) => {
          const current = new FileReader()
          reader.current = current
          current.onload = () => resolve(String(current.result).split(',')[1])
          current.onerror = () => reject(new Error('Could not read the image. Please select it again.'))
          current.onabort = () => reject(new Error('Image reading cancelled.'))
          current.readAsDataURL(file)
        })
        if (!active.current) return
        added.push({ contentType: file.type, dataBase64 })
      }
      onChange([...images, ...added])
    } catch (err) { if (active.current) setError(err.message) }
    finally { if (active.current) { setReading(false); onBusyChange?.(false) } }
  }

  if (!onChange && !images.length) return null
  return <section aria-label="Evidence images">
    {onChange && <><label>Add evidence images<input type="file" accept="image/png,image/jpeg" multiple disabled={reading || images.length >= maxImages} onChange={add} /></label>
      <p>Up to {maxImages} PNG or JPEG images, 2 MiB each. Images are saved with this revision.</p></>}
    {reading && <p role="status">Reading images…</p>}{error && <p role="alert">{error}</p>}
    <div className="management-row-actions">{images.map((image, index) => <figure key={index}>
      {imageTypes.includes(image.contentType) && typeof image.dataBase64 === 'string' && image.dataBase64.length <= Math.ceil(maxBytes / 3) * 4 && /^[A-Za-z0-9+/]+={0,2}$/.test(image.dataBase64)
        ? <a href={`data:${image.contentType};base64,${image.dataBase64}`} download={`evidence-${index + 1}.${image.contentType === 'image/png' ? 'png' : 'jpg'}`} aria-label={`Download evidence image ${index + 1}`}>
          <img src={`data:${image.contentType};base64,${image.dataBase64}`} alt={`Evidence image ${index + 1}`} style={{ maxWidth: '100%', width: 240, height: 180, objectFit: 'contain' }} /></a>
        : <p>Image unavailable</p>}
      {onChange && <figcaption><button type="button" disabled={reading} onClick={() => onChange(images.filter((_, i) => i !== index))}>Remove image {index + 1}</button></figcaption>}
    </figure>)}</div>
  </section>
}
