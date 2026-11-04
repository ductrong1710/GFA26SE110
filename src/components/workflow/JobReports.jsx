import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, jobPath, parseJson } from "../../services/api/workflow"
import { Panel, State, Badge, NoteAction, PaperGallery, EvidenceGallery, Field } from './WorkflowUi'

export default function JobReports({ kind, job, admin, onDone }) {
  const t = useWorkflowText()
  const { time } = useWorkflowFormat()

  const path = jobPath(kind, job.id)
  const list = useApiQuery(`reports:${kind}:${job.id}:${job.revision}`, signal => allPages(`${path}/reports`, {}, signal))
  const [selected, setSelected] = useState('')
  const id = list.data?.some(row => row.id === Number(selected)) ? selected : ''
  const query = useApiQuery(`report:${kind}:${id}:${job.revision}`, signal => api.get(`${path}/reports/${id}`, {}, signal), Boolean(id))
  const report = query.data
  const latest = Number(id) === list.data?.[0]?.id
  return <Panel title={t("Báo cáo & giấy tờ hậu kiểm")}><State query={list}>
    {!list.data?.length ? <p className="wf-empty">{t("Chưa có báo cáo gửi lên.")}</p> : <>
      <Field title={t("Phiên bản báo cáo")}><select value={id ?? ''} onChange={e => setSelected(e.target.value)}><option value="" disabled>Select report revision</option>{list.data.map(row => <option key={row.id} value={row.id}>{t("Phiên bản ")}{row.revision} · {time(row.createdAt)}</option>)}</select></Field>
      <State query={query}>{report && <>
        <p>{report.notes}</p>{report.cause && <dl className="wf-facts"><div><dt>{t("Nguyên nhân")}</dt><dd>{report.cause}</dd></div><div><dt>{t("Xử lý")}</dt><dd>{report.work}</dd></div><div><dt>{t("Linh kiện")}</dt><dd>{report.components}</dd></div><div><dt>{t("Chạy thử")}</dt><dd>{report.testResults}</dd></div></dl>}
        {report.devices?.map((device, i) => <p key={i}>{t("Thiết bị #")}{device.sensorNodeId ?? device.uavId ?? device.gatewayId}: {device.installationLocation} · {device.testResults} · {device.connectivityPassed && device.operationPassed && device.accessoriesChecked ? t("Kiểm tra đạt") : t("Có kiểm tra chưa đạt")}</p>)}
        {Array.isArray(parseJson(report.paperworkJson, {})) ? <EvidenceGallery images={report.paperworkJson} title={t("Biên bản nghiệm thu ký giấy")} /> : <PaperGallery value={report.paperworkJson} />}
        <EvidenceGallery images={report.images} title={t("Ảnh báo cáo")} />
        {report.suggestedRentalStartAt && <p className="wf-callout">{t("Ngày bắt đầu thuê đề xuất: ")}<strong>{time(report.suggestedRentalStartAt)}</strong>{t(" — ngày sau khi hoàn tất bàn giao.")}</p>}
        {report.reviewNote && <p>{t("Admin: ")}{report.reviewNote}</p>}
        {admin && latest && ['REPORT_SUBMITTED', 'OWNER_CONFIRMED'].includes(job.status) && <>
          <div className="wf-callout">{t("Đối chiếu khách/nông trại, mã và số lượng thiết bị, giá đã chốt, thời hạn, chữ ký, kết quả chạy thử và bill. Duyệt sẽ ")}{kind === 'installation' ? t("tạo hồ sơ thuê chính thức") : t("áp dụng thay thế thiết bị và đóng ca")}.</div>
          <div className="wf-two-col"><NoteAction path={`${path}/approve`} body={{ expectedRevision: job.revision, reportId: Number(id) }} onDone={onDone}>{t("Duyệt báo cáo cuối")}</NoteAction><NoteAction path={`${path}/return`} body={{ expectedRevision: job.revision, reportId: Number(id) }} onDone={onDone} danger>{t("Trả bổ sung / xử lý lại")}</NoteAction></div>
        </>}
        {report.approved != null && <Badge value={report.approved ? 'APPROVED' : 'REWORK'} />}
      </>}</State>
    </>}
  </State></Panel>
}
