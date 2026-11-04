import { useWorkflowText } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { api } from '../../services/api'
import { Field, Button, useAction } from './WorkflowUi'
import EvidenceUpload from './EvidenceUpload'

export default function ServiceReportForm({ job, onDone }) {
  const t = useWorkflowText()

  const [images, setImages] = useState([])
  const [signedHandover, setSignedHandover] = useState([])
  const action = useAction(onDone)
  return <form className="wf-form" onSubmit={event => {
    event.preventDefault(); const form = new FormData(event.currentTarget)
    action.run(() => api.post(`/api/service-jobs/${job.id}/reports`, { expectedRevision: job.revision, images, signedHandover,
      ...Object.fromEntries(['cause', 'work', 'components', 'testResults', 'notes'].map(key => [key, form.get(key)])), connectivityPassed: form.has('connectivityPassed'), operationPassed: form.has('operationPassed'),
    }))
  }}>
    {[['cause', t("Nguyên nhân lỗi")], ['work', t("Công việc đã thực hiện")], ['components', t("Linh kiện đã dùng (ghi “Không thay” nếu không có)")], ['testResults', t("Kết quả chạy thử")], ['notes', t("Ghi chú nghiệm thu")]].map(([name, title]) => <Field key={name} title={title} name={name} required maxLength={name === 'testResults' ? 2000 : 4000} />)}
    <div className="wf-checks"><label><input type="checkbox" name="connectivityPassed" />{t(" Kết nối đạt")}</label><label><input type="checkbox" name="operationPassed" />{t(" Vận hành đạt")}</label></div>
    <EvidenceUpload title={t("Biên bản nghiệm thu ký giấy")} camera required value={signedHandover} onChange={setSignedHandover} /><EvidenceUpload title={t("Ảnh kiểm tra, bill nếu có")} value={images} onChange={setImages} />
    <p className="wf-muted">{t("Phải có phương án đã được Admin duyệt và đã lưu kết quả thực hiện. Không ký trên App.")}</p>{action.errorView}<Button type="submit" primary disabled={action.busy || !signedHandover.length}>{t("Gửi báo cáo hoàn thành chờ Admin duyệt")}</Button>
  </form>
}
