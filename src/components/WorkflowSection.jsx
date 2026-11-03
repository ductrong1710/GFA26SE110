const steps = [
  ['01', 'Tư vấn nhu cầu', 'Để lại thông tin trên Homepage. Kỹ sư liên hệ, ghi nhận nông trại và đề xuất thiết bị phù hợp.'],
  ['02', 'Chốt phương án & cọc', 'Thống nhất thiết bị, giá thuê, thời hạn và lịch mong muốn. Cọc được xác nhận bên ngoài hệ thống.'],
  ['03', 'Khảo sát thực tế & lắp đặt', 'KTV đến vườn, xác định vị trí, lắp thiết bị và kiểm tra dữ liệu trước khi bàn giao.'],
  ['04', 'Ký giấy & bắt đầu thuê', 'Khách ký hợp đồng và biên bản giấy. Sau hậu kiểm, hồ sơ thuê bắt đầu từ ngày sau ngày hoàn tất.'],
  ['05', 'Sử dụng & hỗ trợ', 'Chủ vườn theo dõi dữ liệu và tự vận hành UAV. Báo tình trạng cây hoặc thiết bị khi cần hỗ trợ.'],
  ['06', 'Bảo trì & gia hạn', 'Bảo trì theo lịch; linh kiện bảo hành được miễn phí. Nhận nhắc gia hạn hoặc sắp xếp thu hồi khi kết thúc.'],
]
export default function WorkflowSection() {
  return <section id="how-it-works" className="public-flow"><div className="page-width"><p className="eyebrow">TỪ TƯ VẤN ĐẾN ĐỒNG HÀNH</p><h2>Quy trình rõ ràng,<br />ngay từ ngày đầu.</h2><div>{steps.map(([number,title,text])=><article key={number}><span>{number}</span><h3>{title}</h3><p>{text}</p></article>)}</div></div></section>
}
