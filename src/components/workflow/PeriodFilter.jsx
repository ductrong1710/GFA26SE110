import { useWorkflowText } from '../../utils/useWorkflowText'
import { Field } from './WorkflowUi'

export default function PeriodFilter({ value, onChange }) {
  const t = useWorkflowText()

  const date = value => value ? new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : ''
  return <div className="wf-toolbar"><Field title={t("Từ ngày")} type="date" max={value.to ? date(new Date(new Date(value.to).getTime() - 1)) : undefined} value={date(value.from)} onChange={e => onChange({ ...value, from: e.target.value ? new Date(`${e.target.value}T00:00:00+07:00`).toISOString() : '' })} />
    <Field title={t("Đến ngày")} type="date" min={date(value.from) || undefined} value={value.to ? date(new Date(new Date(value.to).getTime() - 1)) : ''} onChange={e => onChange({ ...value, to: e.target.value ? new Date(new Date(`${e.target.value}T00:00:00+07:00`).getTime() + 86400000).toISOString() : '' })} />
    <small>{t("Thời gian Việt Nam · tối đa 366 ngày")}</small></div>
}
