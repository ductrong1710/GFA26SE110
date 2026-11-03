import { Link } from 'react-router-dom'
import { Workspace, Panel } from '../../components/workflow/WorkflowUi'
export default function EngineerDashboard() {
  return <Workspace title="Không gian kỹ sư nông nghiệp" description="Tư vấn nhu cầu thiết bị và tiếp nhận báo cáo tình trạng cây trồng."><div className="wf-two-col"><Panel title="Khách gửi form Homepage" description="Người trực xem thông tin và liên hệ bên ngoài, không cần phân công ca trực."><Link className="app-ui-button app-ui-button--primary" to="/app/consultations">Xem khách cần tư vấn →</Link></Panel><Panel title="Báo cáo cây bệnh" description="Xem ảnh, triệu chứng, mùa vụ và dữ liệu cảm biến được đính kèm theo từng báo cáo."><Link className="app-ui-button" to="/app/support">Mở báo cáo cây trồng →</Link></Panel></div></Workspace>
}
