import { useWorkflowText } from '../../utils/useWorkflowText'
import { useEffect, useRef, useState } from 'react'

export async function readEvidence(files) {
  if (files.length > 5) throw new Error('Mỗi mục tối đa 5 ảnh.')
  return Promise.all([...files].map(file => {
    if (!['image/png', 'image/jpeg'].includes(file.type) || file.size > 2 * 1024 * 1024) throw new Error('Chỉ nhận JPEG/PNG, tối đa 2 MB mỗi ảnh.')
    return new Promise((resolve, reject) => {
      const reader = new FileReader()
      reader.onerror = () => reject(new Error('Không đọc được ảnh. Vui lòng chọn lại.'))
      reader.onload = () => resolve({ contentType: file.type, dataBase64: reader.result.split(',')[1] })
      reader.readAsDataURL(file)
    })
  }))
}
export default function EvidenceUpload({ title, value = [], onChange, required = false, camera = false }) {
  const t = useWorkflowText()

  const [error, setError] = useState('')
  const [reading, setReading] = useState(false)
  const request = useRef(0)
  useEffect(() => () => { request.current++ }, [])
  return <div className="wf-upload"><label className="wf-field"><span>{title}{required && ' *'}</span>
    <span className="wf-file-picker"><span aria-hidden="true">Choose file</span><input aria-label={title ?? 'Choose file'} type="file" accept="image/png,image/jpeg" multiple capture={camera ? 'environment' : undefined} required={required && !value.length} onChange={async e => {
      const generation = ++request.current
      const files = [...e.target.files]; setReading(true); setError(''); onChange([])
      try { const images = await readEvidence(files); if (generation === request.current) onChange(images) }
      catch (err) { if (generation === request.current) { onChange([]); setError(err.message) } }
      finally { if (generation === request.current) setReading(false) }
    }} /></span>
    <small>{reading ? t("Đang đọc ảnh…") : ((value.length) + t("/5 ảnh · JPEG/PNG, tối đa 2 MB/ảnh"))}</small></label>
    {error && <p role="alert" className="wf-error">{t(error)}</p>}
    {value.length > 0 && <div className="wf-thumbnails">{value.map((image, i) => <img key={i} alt={`${title} ${i + 1}`} src={`data:${image.contentType};base64,${image.dataBase64}`} />)}</div>}
  </div>
}
