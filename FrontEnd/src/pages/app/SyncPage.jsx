import { PageHeader } from '../../components/app/ui'

export default function SyncPage() {
  return <div className="app-ui">
    <PageHeader eyebrow="OPERATIONS" title="Đồng bộ dữ liệu" description="Trạng thái hàng đợi và lịch sử đồng bộ gateway." />
    <p role="status" className="management-note">Backend hiện chỉ hỗ trợ đồng bộ qua xác thực thiết bị. Trang quản trị chưa có API tương ứng nên chức năng này đang tắt.</p>
  </div>
}
