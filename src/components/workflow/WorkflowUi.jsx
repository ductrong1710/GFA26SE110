import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../services/api'
import { parseJson } from "../../services/api/workflow"
import ApiState from '../app/ApiState'
import { PageHeader, SectionCard } from '../app/ui'
import '../../styles/workflow.css'

export function Workspace({ title, description, actions, children, className = '' }) {
  return <div className={`app-ui workflow ${className}`}><PageHeader title={title} description={description} actions={actions} />{children}</div>
}
export function Panel({ title, description, children, actions }) {
  return <SectionCard title={title} description={description} actions={actions}>{children}</SectionCard>
}
export function State({ query, children }) {
  return query.loading || query.error ? <ApiState {...query} /> : children
}
export function Badge({ value }) {
  const { label } = useWorkflowFormat()
  return <span className="wf-badge" data-state={value}>{label(value)}</span>
}
export function Field({ title, children, ...props }) {
  return <label className="wf-field"><span>{title}</span>{children ?? <input {...props} />}</label>
}
export function Button({ children, primary = false, ...props }) {
  return <button type="button" className={`app-ui-button${primary ? ' app-ui-button--primary' : ''}`} {...props}>{children}</button>
}
export function useAction(after) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const run = async task => {
    if (busy) return
    setBusy(true); setError(null)
    try { await task(); await after?.() } catch (e) { setError(e.message) } finally { setBusy(false) }
  }
  return { busy, error, run, errorView: error && <p className="wf-error" role="alert">{error}</p> }
}
export function NoteAction({ path, body = {}, children, onDone, danger = false }) {
  const t = useWorkflowText()

  const [note, setNote] = useState('')
  const action = useAction(onDone)
  return <form className="wf-inline-form" onSubmit={e => { e.preventDefault(); action.run(() => api.post(path, { ...body, note })) }}>
    <Field title={t("Nội dung xác nhận / lý do")} required maxLength={2000} value={note} onChange={e => setNote(e.target.value)} />
    <Button type="submit" disabled={action.busy} data-danger={danger}>{action.busy ? t("Đang lưu…") : children}</Button>{action.errorView}
  </form>
}
export function Pager({ page, total, pageSize = 20, onChange }) {
  const t = useWorkflowText()

  return <div className="wf-pager"><span>{total ?? 0}{t(" kết quả · Trang ")}{page}</span><Button disabled={page <= 1} onClick={() => onChange(page - 1)}>{t("Trước")}</Button><Button disabled={page * pageSize >= (total ?? 0)} onClick={() => onChange(page + 1)}>{t("Sau")}</Button></div>
}
export function JobLink({ kind, id, children }) {
  const t = useWorkflowText()
  return <Link className="wf-link" to={`/app/work-orders/${kind}/${id}`}>{children ?? `${t('Công việc #')}${id}`}</Link>
}
export function EvidenceGallery({ images, title = 'Giấy tờ và hình ảnh' }) {
  const t = useWorkflowText()
  title = t(title)
  const rows = parseJson(images, [])
  if (!Array.isArray(rows) || !rows.length) return null
  return <details className="wf-evidence"><summary>{title} ({rows.length})</summary><div>{rows.map((image, i) =>
    ['image/png', 'image/jpeg'].includes(image?.contentType) && <a key={i} href={`data:${image.contentType};base64,${image.dataBase64}`} download={`bang-chung-${i + 1}.${image.contentType === 'image/png' ? 'png' : 'jpg'}`}><img alt={`${title} ${i + 1}`} src={`data:${image.contentType};base64,${image.dataBase64}`} /></a>)}</div></details>
}
export function PaperGallery({ value }) {
  const t = useWorkflowText()

  const paper = parseJson(value, {})
  return <>{Object.entries(paper).filter(([, images]) => Array.isArray(images)).map(([key, images]) => <EvidenceGallery key={key} images={images} title={{ Contract: t("Hợp đồng giấy"), Handover: t("Bàn giao"), Bills: t("Bill thanh toán"), SignedAgreement: t("Giấy đồng ý phương án"), OldDeviceImages: t("Thiết bị cũ"), NewDeviceImages: t("Thiết bị mới"), contract: t("Hợp đồng giấy"), handover: t("Bàn giao"), bills: 'Bill' }[key] ?? key} />)}</>
}
