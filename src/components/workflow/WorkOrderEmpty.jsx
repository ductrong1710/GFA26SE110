import AppIcon from '../app/AppIcon'
import { useUiText } from '../../utils/useUiText'

export default function WorkOrderEmpty({ selected = false, filtered = false }) {
  const t = useUiText({
    'Work order': 'Công việc', Farm: 'Nông trại', Technician: 'Kỹ thuật viên', Scheduled: 'Lịch hẹn', Status: 'Trạng thái',
    'Choose a work order type': 'Chọn loại công việc',
    'Select Installation or Maintenance / repair above to view the work list.': 'Chọn Lắp đặt hoặc Bảo trì / sửa chữa phía trên để xem danh sách.',
    'No work orders scheduled': 'Chưa có công việc được lên lịch',
    'Assigned work orders will appear here when a technician and appointment are scheduled.': 'Công việc sẽ hiển thị tại đây khi được phân công kỹ thuật viên và lịch hẹn.',
    'No matching work orders': 'Không có công việc phù hợp',
    'Try another status to view other work orders.': 'Chọn trạng thái khác để xem công việc.',
  })
  return <div className="wf-work-empty"><table aria-label={t('Work order')}>
    <thead><tr>{['Work order', 'Farm', 'Technician', 'Scheduled', 'Status'].map(name => <th key={name} scope="col">{t(name)}</th>)}</tr></thead>
    <tbody><tr><td colSpan={5}><div className="wf-work-empty-message" role="status">
      <AppIcon name="mission" size={32} />
      <h3>{t(!selected ? 'Choose a work order type' : filtered ? 'No matching work orders' : 'No work orders scheduled')}</h3>
      <p>{t(!selected ? 'Select Installation or Maintenance / repair above to view the work list.' : filtered ? 'Try another status to view other work orders.' : 'Assigned work orders will appear here when a technician and appointment are scheduled.')}</p>
    </div></td></tr></tbody>
  </table></div>
}
