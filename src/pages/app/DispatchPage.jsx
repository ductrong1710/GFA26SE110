import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { time, label } from '../../services/api/workflow'
import { Workspace, State, Panel, Field, Button, Badge, JobLink } from '../../components/workflow/WorkflowUi'

export default function DispatchPage() {
  const { activeRole } = useAuth()
  const [date, setDate] = useState(() => new Date().toLocaleDateString('en-CA'))
  const [days, setDays] = useState(7)
  const from = new Date(`${date}T00:00:00+07:00`)
  const to = new Date(from.getTime() + days * 86400000)
  const query = useApiQuery(`calendar:${date}:${days}:${activeRole}`, signal => api.get('/api/operations/calendar', { from: from.toISOString(), to: to.toISOString() }, signal), Boolean(date))
  const jobs = [...(query.data?.installations ?? []), ...(query.data?.services ?? [])].sort((a, b) => new Date(a.scheduledAt) - new Date(b.scheduledAt))
  const technicians = [...new Set(jobs.map(row => row.technicianId))]
  const dates = Array.from({ length: days }, (_, i) => new Date(from.getTime() + i * 86400000))
  const sameDay = (a, b) => new Date(a).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) === b.toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })
  return <Workspace title={activeRole === ROLES.TECHNICIAN ? 'Lịch công việc của tôi' : 'Lịch điều phối kỹ thuật viên'} description="Lắp đặt, bảo trì và sửa chữa trên cùng một lịch. Mở công việc để xem địa chỉ và cập nhật kết quả.">
    <div className="wf-toolbar"><Field title="Ngày bắt đầu" type="date" value={date} onChange={e => { if (e.target.value) setDate(e.target.value) }} /><Field title="Chế độ"><select value={days} onChange={e => setDays(Number(e.target.value))}><option value={1}>Theo ngày</option><option value={7}>Theo tuần</option></select></Field><Button onClick={query.reload}>Làm mới</Button></div>
    <State query={query}><Panel title={`${jobs.length} công việc trong khoảng ngày đã chọn`} description="Lịch hiển thị giờ Việt Nam. Các ô trống chỉ phản ánh khoảng ngày đang xem.">
      {!jobs.length ? <p className="wf-empty">Chưa có công việc trong khoảng ngày này.</p> : <>
        <div className="wf-calendar-wrap"><div className="wf-calendar" style={{ '--days': days }}><div className="wf-calendar-head">Kỹ thuật viên</div>{dates.map(day => <div key={day.toISOString()} className="wf-calendar-head">{day.toLocaleDateString('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit', timeZone: 'Asia/Ho_Chi_Minh' })}</div>)}
          {technicians.map(tech => <div className="wf-calendar-row" key={tech}><strong>KTV #{tech}</strong>{dates.map(day => <div key={day.toISOString()} className="wf-calendar-cell">{jobs.filter(job => job.technicianId === tech && sameDay(job.scheduledAt, day)).map(job => <div className="wf-booking" key={`${job.type}-${job.id}`} data-kind={job.type}><small>{label(job.type)} · {time(job.scheduledAt).split(' ')[0]}</small><JobLink kind={job.type === 'INSTALLATION' ? 'installation' : 'service'} id={job.id}>{job.farmName}</JobLink><span>{new Date(job.scheduledAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}–{new Date(job.endAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}</span></div>)}</div>)}</div>)}
        </div></div>
        <div className="wf-mobile-jobs">{jobs.map(job => <article key={`${job.type}-${job.id}`}><Badge value={job.status} /><h3><JobLink kind={job.type === 'INSTALLATION' ? 'installation' : 'service'} id={job.id}>{label(job.type)} · {job.farmName}</JobLink></h3><p>{time(job.scheduledAt)} – {time(job.endAt)}</p><p>{job.address}</p><p>{job.customer} · {job.phone}</p></article>)}</div>
      </>}
    </Panel></State>
  </Workspace>
}
