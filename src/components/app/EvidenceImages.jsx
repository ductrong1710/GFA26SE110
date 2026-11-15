import { useEffect, useRef, useState } from 'react'

const maxBytes = 2 * 1024 * 1024
const imageTypes = ['image/png', 'image/jpeg']

export default function EvidenceImages({ images = [], maxImages = 5, onChange, onBusyChange, vietnamese = false }) {
  const label = (vi, en) => vietnamese ? vi : en
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
    if (images.length + files.length > maxImages) { setError(label(`Chọn tối đa ${maxImages} ảnh minh chứng.`, `Choose up to ${maxImages} evidence image(s).`)); return }
    if (files.some(file => !imageTypes.includes(file.type) || file.size === 0 || file.size > maxBytes)) {
      setError(label('Chọn ảnh PNG hoặc JPEG có dung lượng lớn hơn 0 và tối đa 2 MiB mỗi ảnh.', 'Choose non-empty PNG or JPEG images, up to 2 MiB each.')); return
    }
    setReading(true); onBusyChange?.(true)
    try {
      const added = []
      for (const file of files) {
        const dataBase64 = await new Promise((resolve, reject) => {
          const current = new FileReader()
          reader.current = current
          current.onload = () => resolve(String(current.result).split(',')[1])
          current.onerror = () => reject(new Error(label('Không thể đọc ảnh. Vui lòng chọn lại.', 'Could not read the image. Please select it again.')))
          current.onabort = () => reject(new Error(label('Đã hủy đọc ảnh.', 'Image reading cancelled.')))
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
  return <section aria-label={label('Ảnh minh chứng', 'Evidence images')}>
    {onChange && <><label>{label('Thêm ảnh minh chứng', 'Add evidence images')}<input type="file" accept="image/png,image/jpeg" multiple disabled={reading || images.length >= maxImages} onChange={add} /></label>
      <p>{label(`Tối đa ${maxImages} ảnh PNG hoặc JPEG, mỗi ảnh không quá 2 MiB. Ảnh được lưu cùng phiên bản này.`, `Up to ${maxImages} PNG or JPEG images, 2 MiB each. Images are saved with this revision.`)}</p></>}
    {reading && <p role="status">{label('Đang đọc ảnh…', 'Reading images…')}</p>}{error && <p role="alert">{error}</p>}
    <div className="management-row-actions">{images.map((image, index) => <figure key={index}>
      {imageTypes.includes(image.contentType) && typeof image.dataBase64 === 'string' && image.dataBase64.length <= Math.ceil(maxBytes / 3) * 4 && /^[A-Za-z0-9+/]+={0,2}$/.test(image.dataBase64)
        ? <a href={`data:${image.contentType};base64,${image.dataBase64}`} download={`evidence-${index + 1}.${image.contentType === 'image/png' ? 'png' : 'jpg'}`} aria-label={label(`Tải ảnh minh chứng ${index + 1}`, `Download evidence image ${index + 1}`)}>
          <img src={`data:${image.contentType};base64,${image.dataBase64}`} alt={label(`Ảnh minh chứng ${index + 1}`, `Evidence image ${index + 1}`)} style={{ maxWidth: '100%', width: 240, height: 180, objectFit: 'contain' }} /></a>
        : <p>{label('Không có ảnh', 'Image unavailable')}</p>}
      {onChange && <figcaption><button type="button" disabled={reading} onClick={() => onChange(images.filter((_, i) => i !== index))}>{label(`Xóa ảnh ${index + 1}`, `Remove image ${index + 1}`)}</button></figcaption>}
    </figure>)}</div>
  </section>
}
