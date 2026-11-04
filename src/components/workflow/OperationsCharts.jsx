import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend } from 'recharts'
import { chartRows } from "../../services/api/workflow"
import { Panel } from './WorkflowUi'

const shades = ['#176346', '#8ab99c', '#c5d8cc', '#5e7468', '#c29c54', '#9ca9a1']
function Chart({ title, data, field = 'status' }) {
  const t = useWorkflowText()
  const { label } = useWorkflowFormat()

  const rows = chartRows(data, field)
  const keys = [...new Set(data.map(row => row[field]))]
  return <Panel title={title}>{!rows.length ? <p className="wf-empty">{t("Chưa có dữ liệu trong khoảng ngày đã chọn.")}</p> :
    <div className="wf-chart"><ResponsiveContainer width="100%" height={220}><BarChart data={rows} accessibilityLayer>
      <CartesianGrid vertical={false} stroke="#e5ebe7" /><XAxis dataKey="date" tickFormatter={v => v.slice(5).split('-').reverse().join('/')} tickLine={false} axisLine={false} />
      <YAxis allowDecimals={false} width={32} axisLine={false} tickLine={false} /><Tooltip formatter={(value, key) => [value, label(key)]} />
      <Legend formatter={label} />{keys.map((key, i) => <Bar key={key} dataKey={key} name={key} stackId="total" fill={shades[i % shades.length]} maxBarSize={32} />)}
    </BarChart></ResponsiveContainer></div>}</Panel>
}
export default function OperationsCharts({ data }) {
  const t = useWorkflowText()

  return <div className="wf-charts"><Chart title={t("Cảnh báo theo ngày")} data={data.alerts ?? []} field="alertType" />
    <Chart title={t("Tiến độ lắp đặt & dịch vụ")} data={data.work ?? []} /><Chart title={t("Nhiệm vụ UAV")} data={data.missions ?? []} /></div>
}
