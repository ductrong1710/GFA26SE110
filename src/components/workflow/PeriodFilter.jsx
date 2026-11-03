import { Field } from './WorkflowUi'

export default function PeriodFilter({ value, onChange }) {
  const date = value => new Date(value).toLocaleDateString('en-CA')
  return <div className="wf-toolbar"><Field title="Từ ngày" type="date" value={date(value.from)} onChange={e => { if (e.target.value) onChange({ ...value, from: new Date(`${e.target.value}T00:00:00+07:00`).toISOString() }) }} />
    <Field title="Đến ngày" type="date" min={date(value.from)} value={date(new Date(new Date(value.to).getTime() - 1).toISOString())} onChange={e => { if (e.target.value) onChange({ ...value, to: new Date(new Date(`${e.target.value}T00:00:00+07:00`).getTime() + 86400000).toISOString() }) }} />
    <small>Thời gian Việt Nam · tối đa 366 ngày</small></div>
}
