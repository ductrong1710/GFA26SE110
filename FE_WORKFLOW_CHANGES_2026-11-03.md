# Giao diện theo luồng BE — 03/11/2026

## Phạm vi

FE dùng API BE hiện tại, không thay dữ liệu lỗi bằng dữ liệu mẫu. Tách component,
trang và CSS để mỗi file code không quá 300 dòng; `npm run check:lines` kiểm tra
giới hạn này trong cả bộ kiểm thử chính.

## Các màn hình và hành vi

- Homepage: thiết bị, giá thuê tháng, giá linh kiện, chính sách và liên hệ nhanh
  từ nội dung đã công bố. Form tư vấn gửi hồ sơ không cần đăng nhập. Liên hệ
  Zalo/Messenger/điện thoại chỉ mở liên kết, không tạo hồ sơ hay thống kê tiếp nhận.
- Admin: dashboard có biểu đồ dữ liệu thật, pipeline lắp đặt, yêu cầu quan tâm,
  hồ sơ thuê sắp hết hạn và bảo trì. Danh sách tư vấn không có thao tác nhận ca.
- Điều phối: lịch ngày/tuần; kiểm tra KTV rảnh theo khoảng hẹn; có gợi ý từ BE.
  Admin được thay KTV khi cần, gửi revision để BE kiểm tra xung đột lịch.
- Lắp đặt: duyệt yêu cầu, phân công, cấp phát thiết bị, bắt đầu, báo cáo giấy tờ,
  trả bổ sung hoặc duyệt hậu kiểm. KTV chỉ xem công việc được giao.
- Kỹ sư nông nghiệp: mở yêu cầu tư vấn, lưu khảo sát, đề xuất thiết bị/giá đã
  công bố, xác nhận phương án/cọc rồi gửi yêu cầu lắp đặt. Không hỏi tuổi cây
  hoặc vị trí dự kiến lắp cảm biến/gateway; KTV xác định khi khảo sát thực tế.
- Bảo trì/sửa chữa: đề xuất thay thế, Admin duyệt trước khi làm, KTV lưu bằng
  chứng thực hiện, báo cáo hoàn tất, Admin duyệt cuối để BE áp dụng thay thế.
- Báo cáo khách hàng: cây bệnh và thiết bị không nhận dữ liệu. Chủ nông trại
  không phải biết serial; Admin xác định thiết bị trước khi điều phối ca sửa.
- Hồ sơ thuê: giấy tờ, giá chốt, thiết bị, gia hạn và kết thúc; lịch sử thiết bị
  tổng hợp; lịch bảo trì định kỳ; mùa vụ và nhật ký thủ công theo API có sẵn.

## Quy tắc quan trọng

- Chỉ ký giấy, tải ảnh hợp đồng/biên bản đã ký. Không có ký điện tử.
- Ảnh JPEG/PNG, tối đa 5 ảnh mỗi mục và 2 MB mỗi ảnh, phù hợp hợp đồng BE.
- BE quyết định ngày thuê: ngày kế tiếp ngày hoàn tất bàn giao thực tế theo giờ
  Việt Nam. Admin duyệt báo cáo để tạo hồ sơ thuê; FE không tạo hồ sơ lần hai.
- Công bảo trì/sửa chữa miễn phí. Linh kiện dùng giá thay thế đã công bố, chốt
  trong phương án; trường hợp bảo hành cần lý do. Giá thuê tháng là trường riêng.
- Sửa giá công bố không sửa giá snapshot của phương án/hồ sơ đã chốt.
- Không bổ sung kho, kế toán hoặc quy trình ký online.
- Quyền FE dùng để hiển thị; BE tiếp tục kiểm tra quyền, revision, lịch và dữ liệu.
- Cần áp dụng migration BE `20261103084905_OperationalWorkflow` vào môi trường
  chạy trước khi dùng API mới. Chưa áp dụng vào cơ sở dữ liệu ứng dụng của bạn.

## Kiểm tra và chạy

Đặt `VITE_API_BASE_URL` trong `.env.local`, chạy BE và `npm run dev`.

- `npm test`: 176 kiểm thử đạt (113 Node, 51 UI hiện có, 4 quản lý người dùng,
  8 kiểm thử luồng mới), kèm kiểm tra số dòng.
- `npm run build`: thành công; Vite còn cảnh báo bundle chính trên 500 KB.
- Kiểm tra trình duyệt với BE/PostgreSQL tạm riêng: Homepage hiển thị giá thật,
  Admin xem dashboard/lịch/ca làm việc; KTV thấy ca của mình, bắt đầu công việc
  và mở biểu mẫu nghiệm thu giấy. Kiểm tra bố cục ở desktop, tablet và mobile.
- Chưa kiểm thử toàn bộ nghiệm thu bằng giấy thật hoặc gửi thông báo ra dịch vụ
  bên ngoài. Kiểm thử component xác nhận yêu cầu bằng chứng và quyền thao tác.
- `scripts/Preview-Workflow.ps1` tạo môi trường kiểm tra riêng, không dùng DB
  ứng dụng. Tạo file `stop` trong thư mục preview được script in ra để dừng.

Các màn hình xử lý tải dữ liệu, không có dữ liệu và lỗi API rõ ràng. Các khối
minh họa trên Homepage được ghi nhãn, không giả làm số liệu cảm biến trực tiếp.
