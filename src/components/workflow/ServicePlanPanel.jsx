import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, parseJson } from "../../services/api/workflow"
import { Panel, Field, Button, State, Badge, NoteAction, PaperGallery, useAction } from './WorkflowUi'
import EvidenceUpload from './EvidenceUpload'

export default function ServicePlanPanel({ job, technician, admin, onDone }) {
  const t = useWorkflowText()
  const { money } = useWorkflowFormat()

  const base = `/api/operations/service-jobs/${job.id}/plans`
  const query = useApiQuery(`plans:${job.id}:${job.revision}`, signal => api.get(base, {}, signal))
  const [lines, setLines] = useState([])
  const [target, setTarget] = useState('')
  const [evidence, setEvidence] = useState({ signedAgreement: [], oldDeviceImages: [], newDeviceImages: [] })
  const catalog = useApiQuery(`service-catalog:${job.id}`, async signal => ({
    prices: (await allPages('/api/public-content', {}, signal)).filter(x => x.replacementPriceVnd != null),
    devices: await allPages(`/api/service-jobs/${job.id}/replacement-options`, {}, signal),
  }), technician)
  const action = useAction(onDone)
  const plans = query.data ?? []
  const active = plans.find(p => !['REJECTED', 'APPLIED'].includes(p.status))
  const canWork = ['IN_PROGRESS', 'REWORK'].includes(job.status)
  const total = lines.reduce((sum, line) => sum + (line.warranty ? 0 : (catalog.data?.prices.find(p => p.id === Number(line.publicContentId))?.replacementPriceVnd ?? 0) * Number(line.quantity)), 0)
  return <Panel title={t("Phương án sửa chữa & chi phí linh kiện")} description={t("Công bảo trì/sửa chữa miễn phí. Linh kiện thuộc bảo hành có số tiền phải trả bằng 0.")}><State query={query}>
    {plans.map(plan => <article className="wf-plan" key={plan.id}><div className="wf-toolbar"><strong>{t("Phương án #")}{plan.id}</strong><Badge value={plan.status} /><strong>{money(plan.totalVnd)}</strong></div><p>{plan.note}</p>
      <ul className="wf-list">{parseJson(plan.priceLinesJson).map((line, i) => <li key={i}><span>{line.Name}{" × "}{line.Quantity}<small>{line.Warranty ? t(" · Bảo hành") : t(" · Có tính phí")} · {line.Reason}</small></span><span>{money(line.TotalVnd)}</span></li>)}</ul><PaperGallery value={plan.evidenceJson} />
      {plan.reviewNote && <p className="wf-muted">{t("Admin: ")}{plan.reviewNote}</p>}
      {admin && plan.status === 'PROPOSED' && <div className="wf-two-col"><NoteAction path={`/api/operations/service-plans/${plan.id}/review`} body={{ expectedRevision: job.revision, approve: true }} onDone={onDone}>{t("Duyệt phương án trước khi làm")}</NoteAction><NoteAction path={`/api/operations/service-plans/${plan.id}/review`} body={{ expectedRevision: job.revision, approve: false }} onDone={onDone}>{t("Từ chối phương án")}</NoteAction></div>}
    </article>)}
    {technician && canWork && !active && <State query={catalog}><form className="wf-form" onSubmit={event => {
      event.preventDefault(); const form = new FormData(event.currentTarget)
      const device = catalog.data?.devices[Number(target)]
      action.run(() => api.post(base, { expectedRevision: job.revision, note: form.get('note'), lines: lines.map(line => ({ ...line, publicContentId: Number(line.publicContentId), quantity: Number(line.quantity) })),
        ...(target !== '' && device ? { sensorNodeId: device.sensorNodeId, uavId: device.uavId, gatewayId: device.gatewayId } : {}),
      }))
    }}>
      <Field title={t("Thiết bị thay thế toàn bộ (nếu cần)")}><select value={target} onChange={e => setTarget(e.target.value)}><option value="">{t("Không thay toàn bộ thiết bị")}</option>{catalog.data?.devices.map((device, i) => <option key={i} value={i}>{device.name} · {device.code}</option>)}</select></Field>
      {lines.map((line, i) => <fieldset key={i}><legend>{t("Linh kiện ")}{i + 1}</legend><Field title={t("Giá công khai")}><select required value={line.publicContentId} onChange={e => setLines(old => old.map((row, n) => n === i ? { ...row, publicContentId: e.target.value } : row))}><option value="">{t("Chọn linh kiện")}</option>{catalog.data?.prices.map(price => <option key={price.id} value={price.id}>{price.title} · {money(price.replacementPriceVnd)}</option>)}</select></Field>
        <Field title={t("Số lượng")} type="number" min="1" max="1000" required value={line.quantity} onChange={e => setLines(old => old.map((row, n) => n === i ? { ...row, quantity: e.target.value } : row))} />
        <label><input type="checkbox" checked={line.warranty} onChange={e => setLines(old => old.map((row, n) => n === i ? { ...row, warranty: e.target.checked } : row))} />{t(" Thuộc bảo hành, miễn phí linh kiện")}</label>
        <Field title={t("Lý do bảo hành / tính phí")} required maxLength={1000} value={line.reason} onChange={e => setLines(old => old.map((row, n) => n === i ? { ...row, reason: e.target.value } : row))} /><Button onClick={() => setLines(old => old.filter((_, n) => n !== i))}>{t("Bỏ dòng")}</Button></fieldset>)}
      <Button disabled={lines.length >= 100} onClick={() => setLines(old => [...old, { publicContentId: '', quantity: 1, warranty: false, reason: '' }])}>{t("Thêm linh kiện tính giá")}</Button>
      <Field title={t("Nguyên nhân và phương án đề xuất")} name="note" required maxLength={2000} /><p>{t("Tạm tính theo giá công khai: ")}<strong>{money(total)}</strong></p>{action.errorView}
      <Button type="submit" primary disabled={action.busy}>{t("Gửi Admin duyệt phương án")}</Button>
    </form></State>}
    {technician && canWork && active?.status === 'APPROVED' && <form className="wf-form" onSubmit={e => { e.preventDefault(); action.run(() => api.post(`/api/operations/service-plans/${active.id}/execute`, { expectedRevision: job.revision, ...evidence })) }}>
      <p>{t("Khách ký giấy đồng ý trước khi thực hiện. Đính kèm ảnh sau khi thay; thiết bị chỉ cập nhật chính thức sau hậu kiểm.")}</p>
      {Object.keys(evidence).map(key => <EvidenceUpload key={key} title={{ signedAgreement: t("Giấy khách đồng ý phương án / chi phí"), oldDeviceImages: t("Thiết bị cũ, rõ serial"), newDeviceImages: t("Thiết bị mới, rõ serial") }[key]} camera required={key === 'signedAgreement' || Boolean(active.sensorNodeId || active.uavId || active.gatewayId)} value={evidence[key]} onChange={value => setEvidence(old => ({ ...old, [key]: value }))} />)}
      {action.errorView}<Button type="submit" primary disabled={action.busy || !evidence.signedAgreement.length}>{t("Lưu kết quả thực hiện chờ hậu kiểm")}</Button>
    </form>}
  </State></Panel>
}
