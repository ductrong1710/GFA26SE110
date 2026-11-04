import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'

import { Workspace, State, Panel, Field, Button, Badge, JobLink } from '../../components/workflow/WorkflowUi'

export default function DispatchPage() {
  const t = useWorkflowText()
  const { time, label, locale } = useWorkflowFormat()

  const { activeRole } = useAuth()
  const [date, setDate] = useState('')
  const [days, setDays] = useState('')
  const filtered = Boolean(date && days)
  const from = filtered ? new Date(`${date}T00:00:00+07:00`) : null
  const to = filtered ? new Date(from.getTime() + Number(days) * 86400000) : null
  const query = useApiQuery(`calendar:${date}:${days}:${activeRole}`, signal => api.get('/api/operations/calendar', filtered ? { from: from.toISOString(), to: to.toISOString() } : {}, signal))
  const jobs = [...(query.data?.installations ?? []), ...(query.data?.services ?? [])].sort((a, b) => new Date(a.scheduledAt) - new Date(b.scheduledAt))
  const technicians = [...new Set(jobs.map(row => row.technicianId))]
  const dates = Array.from({ length: filtered ? Number(days) : 0 }, (_, i) => new Date(from.getTime() + i * 86400000))
  const sameDay = (a, b) => new Date(a).toLocaleDateString(locale, { timeZone: 'Asia/Ho_Chi_Minh' }) === b.toLocaleDateString(locale, { timeZone: 'Asia/Ho_Chi_Minh' })
  return <Workspace title={activeRole === ROLES.TECHNICIAN ? t("Lịch công việc của tôi") : t("Lịch điều phối kỹ thuật viên")} description={t("Lắp đặt, bảo trì và sửa chữa trên cùng một lịch. Mở công việc để xem địa chỉ và cập nhật kết quả.")}>
    <div className="wf-toolbar"><Field title={t("Ngày bắt đầu")} type="date" value={date} onChange={e => setDate(e.target.value)} /><Field title={t("Chế độ")}><select value={days} onChange={e => setDays(e.target.value)}><option value="">All</option><option value={1}>{t("Theo ngày")}</option><option value={7}>{t("Theo tuần")}</option></select></Field><Button onClick={query.reload}>{t("Làm mới")}</Button></div>
    <State query={query}><Panel title={filtered ? jobs.length + t(" công việc trong khoảng ngày đã chọn") : `${jobs.length} jobs · All dates`} description={filtered ? t("Lịch hiển thị giờ Việt Nam. Các ô trống chỉ phản ánh khoảng ngày đang xem.") : "Select a start date and view to filter jobs."}>
      {!jobs.length ? <p className="wf-empty">{filtered ? t("Chưa có công việc trong khoảng ngày này.") : "No jobs yet."}</p> : <>
        {filtered && <div className="wf-calendar-wrap"><div className="wf-calendar" style={{ '--days': days }}><div className="wf-calendar-head">{t("Kỹ thuật viên")}</div>{dates.map(day => <div key={day.toISOString()} className="wf-calendar-head">{day.toLocaleDateString(locale, { weekday: 'short', day: '2-digit', month: '2-digit', timeZone: 'Asia/Ho_Chi_Minh' })}</div>)}
          {technicians.map(tech => <div className="wf-calendar-row" key={tech}><strong>{t("KTV #")}{tech}</strong>{dates.map(day => <div key={day.toISOString()} className="wf-calendar-cell">{jobs.filter(job => job.technicianId === tech && sameDay(job.scheduledAt, day)).map(job => <div className="wf-booking" key={`${job.type}-${job.id}`} data-kind={job.type}><small>{label(job.type)} · {time(job.scheduledAt).split(' ')[0]}</small><JobLink kind={job.type === 'INSTALLATION' ? 'installation' : 'service'} id={job.id}>{job.farmName}</JobLink><span>{new Date(job.scheduledAt).toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' })}–{new Date(job.endAt).toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' })}</span></div>)}</div>)}</div>)}
        </div></div>}
        <div className={filtered ? "wf-mobile-jobs" : "wf-list"}>{jobs.map(job => <article key={`${job.type}-${job.id}`}><Badge value={job.status} /><h3><JobLink kind={job.type === 'INSTALLATION' ? 'installation' : 'service'} id={job.id}>{label(job.type)} · {job.farmName}</JobLink></h3><p>{time(job.scheduledAt)} – {time(job.endAt)}</p><p>{job.address}</p><p>{job.customer} · {job.phone}</p></article>)}</div>
      </>}
    </Panel></State>
  </Workspace>
}
