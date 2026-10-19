import { PageHeader } from '../../components/app/ui'

export default function SettingsPage() {
  return <div className="app-ui">
    <PageHeader eyebrow="ADMINISTRATION" title="Cài đặt" description="Cấu hình mặc định và tùy chọn hệ thống." />
    <p role="status" className="management-note">Tính năng cài đặt chung chưa được backend hỗ trợ. Bạn có thể cấu hình ngưỡng cho từng kênh cảm biến tại trang chi tiết cảm biến.</p>
  </div>
}
