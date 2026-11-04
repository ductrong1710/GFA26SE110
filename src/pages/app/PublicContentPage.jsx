import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { usePublicContentDraft } from '../../utils/usePublicContentDraft'
import { websiteContentKinds, websiteContentGroup } from '../../config/websiteContent'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages } from "../../services/api/workflow"
import { Workspace, Panel, State, Field, Button, useAction } from '../../components/workflow/WorkflowUi'
import EvidenceUpload from '../../components/workflow/EvidenceUpload'
import QuickContactSettings from '../../components/workflow/QuickContactSettings'

export default function PublicContentPage() {
  const t = useWorkflowText()
  const { money } = useWorkflowFormat()
  const kinds = { DEVICE: t('Thiết bị cho thuê'), SERVICE: t('Giới thiệu dịch vụ'), INSTALLATION: t('Quy trình lắp đặt'), POLICY: 'Policies', CONTACT: 'Quick contacts' }

  const query = useApiQuery('managed-content', signal => allPages('/api/admin/public-content', {}, signal))
  const { user } = useAuth()
  const { record, image, removeImage, update, reset, storageError } = usePublicContentDraft(user?.id)
  const isContacts = record.kind === 'CONTACT'
  const selectedKind = websiteContentKinds.includes(record.kind) && !isContacts
  const contentRows = (query.data ?? []).filter(row => row.kind !== 'CONTACT' && websiteContentKinds.includes(websiteContentGroup(row.kind)) && (!selectedKind || websiteContentGroup(row.kind) === record.kind))
  const [uploadKey, setUploadKey] = useState(0)
  const clearForm = () => { reset(); setUploadKey(key => key + 1) }
  const action = useAction(() => { clearForm(); query.reload() })
  const publishing = useAction(query.reload)
  const change = (key, value) => update(draft => ({ record: { ...draft.record, [key]: value } }))
  const changeKind = kind => {
    update(draft => ({ record: { ...draft.record, kind, estimatedMonthlyRentVnd: '', replacementPriceVnd: '' }, image: [], removeImage: false }))
    setUploadKey(key => key + 1)
  }
  const edit = row => { update(() => ({ record: { ...row, kind: websiteContentGroup(row.kind) }, image: [], removeImage: false })); setUploadKey(key => key + 1) }
  return <Workspace title={t("Nội dung & bảng giá Homepage")} description="Manage rental devices, rental rates and public service information. Agreed prices in existing dossiers remain unchanged.">
    <div className={isContacts ? 'wf-contact-editor' : 'wf-two-col'}><Panel title={isContacts ? 'Quick contacts' : record.id ? (t("Sửa nội dung #") + (record.id)) : t("Thêm nội dung")}><form className="wf-form" onSubmit={e => {
      e.preventDefault(); if (!Object.hasOwn(kinds, record.kind) || isContacts) return
      action.run(() => api.request(`/api/admin/public-content${record.id ? `/${record.id}` : ''}`, { method: record.id ? 'PUT' : 'POST', body: {
        kind: record.kind, title: record.title, body: record.body, expectedRevision: record.revision ?? 0,
        estimatedMonthlyRentVnd: record.kind === 'DEVICE' && record.estimatedMonthlyRentVnd !== '' && record.estimatedMonthlyRentVnd != null ? Number(record.estimatedMonthlyRentVnd) : null,
        replacementPriceVnd: record.kind === 'DEVICE' && record.replacementPriceVnd !== '' && record.replacementPriceVnd != null ? Number(record.replacementPriceVnd) : null,
        image: record.kind === 'DEVICE' ? image[0] ?? null : null, removeImage: record.kind === 'DEVICE' ? removeImage : true,
      } }))
    }}>
      <Field title={t("Loại nội dung")}><select required value={record.kind} onChange={e => changeKind(e.target.value)}><option value="" disabled>Select content type</option>{Object.entries(kinds).map(([key, title]) => <option key={key} value={key}>{title}</option>)}</select></Field>
      {selectedKind && <>
      <Field title={t("Tên / tiêu đề")} required maxLength={200} value={record.title} onChange={e => change('title', e.target.value)} />
      <Field title={t("Mô tả / thông số / điều kiện")}><textarea required maxLength={4000} rows={5} value={record.body} onChange={e => change('body', e.target.value)} /></Field>
      {record.kind === 'DEVICE' && <Field title={t("Giá thuê / tháng (VND)")} type="number" min="0" step="0.01" value={record.estimatedMonthlyRentVnd ?? ''} onChange={e => change('estimatedMonthlyRentVnd', e.target.value)} />}
      {record.kind === 'DEVICE' && <Field title={t("Giá thay thế / đơn vị (VND)")} type="number" min="0" step="0.01" value={record.replacementPriceVnd ?? ''} onChange={e => change('replacementPriceVnd', e.target.value)} />}
      {record.kind === 'DEVICE' && <EvidenceUpload key={uploadKey} title={t("Ảnh đại diện (chỉ dùng ảnh đầu tiên)")} value={image} onChange={value => update(() => ({ image: value.slice(0, 1), removeImage: false }))} />}
      {record.kind === 'DEVICE' && record.hasImage && <label><input type="checkbox" checked={removeImage} onChange={e => { const checked = e.target.checked; update(() => ({ removeImage: checked, image: [] })) }} />{t(" Gỡ ảnh hiện tại")}</label>}
      {storageError && <p role="alert" className="wf-error">Your browser could not retain this draft. Keep this page open until you save.</p>}
      {action.errorView}<div className="wf-toolbar"><Button type="submit" primary disabled={action.busy}>{t("Lưu nội dung")}</Button>{record.id && <Button onClick={clearForm}>{t("Hủy chỉnh sửa")}</Button>}</div>
      </>}
    </form>{isContacts && <QuickContactSettings query={query} />}</Panel>{!isContacts && <Panel title={t("Danh sách nội dung")}>{publishing.errorView}<State query={query}><ul className="wf-list">{contentRows.map(row => <li key={row.id}><div><small>{kinds[websiteContentGroup(row.kind)]} · {row.published ? t("Đang công khai") : t("Đang ẩn")}</small><h3>{row.title}</h3><p>{row.estimatedMonthlyRentVnd != null ? ((money(row.estimatedMonthlyRentVnd)) + t("/tháng")) : row.replacementPriceVnd != null ? money(row.replacementPriceVnd) : ''}</p><div className="wf-toolbar"><Button onClick={() => { edit(row) }}>{t("Chỉnh sửa")}</Button><Button disabled={publishing.busy} onClick={() => publishing.run(() => api.post(`/api/admin/public-content/${row.id}/${row.published ? 'unpublish' : 'publish'}`, { expectedRevision: row.revision }))}>{row.published ? t("Ẩn nội dung") : t("Đăng công khai")}</Button></div></div></li>)}</ul>{!contentRows.length && <p className="wf-empty">{t("Chưa có nội dung. Thêm sản phẩm đầu tiên để hiển thị trên Homepage.")}</p>}</State></Panel>}</div>
  </Workspace>
}
