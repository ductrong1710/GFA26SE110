# Tóm tắt thay đổi Frontend

## API và đăng nhập

- Chuyển API client sang dùng Axios, cấu hình URL gốc từ `VITE_API_BASE_URL` và timeout mặc định 10 giây.
- Thêm interceptor để tự gửi Access Token, xử lý làm mới phiên và đăng xuất khi phiên hết hạn (HTTP 401).
- Tạo `authStorage` để lưu và đọc Access Token, thông tin người dùng an toàn trong `localStorage`.
- Tích hợp lưu thông tin khi đăng nhập và xóa dữ liệu xác thực khi đăng xuất.

## Tổ chức mã nguồn và giao diện

- Gom cấu hình API vào `src/config/`; giữ client, session, adapter và các hàm gọi API trong `src/services/api/`.
- Dùng các route lồng nhau với `Outlet`. `AppShell` truyền dữ liệu trang qua Outlet context để các trang con đọc bằng `useOutletContext`.
- Cài và tích hợp `react-toastify` để hiển thị thông báo thao tác thành công hoặc thất bại.

## Dữ liệu và chức năng

- Chuyển các luồng giao diện chính sang lấy dữ liệu từ backend; không dùng dữ liệu giả làm phương án thay thế khi API lỗi.
- Gỡ đăng nhập/đổi vai trò demo và một số trang demo khỏi luồng ứng dụng. Fixture trong `src/data/mock/` được giữ lại để phục vụ kiểm thử.
- Trang quản lý người dùng chỉ cho chọn một vai trò Farm Owner hoặc Agricultural Engineer; không cho gán vai trò Administrator cho tài khoản khác và giữ tài khoản quản trị hiện có.
- Bỏ thao tác xóa Farm/Zone và các trường chỉ thuộc dữ liệu giả; giữ các trường và thao tác có API hỗ trợ.
- Ẩn hoặc thông báo chưa hỗ trợ cho chức năng chưa có API phù hợp, như cài đặt hệ thống và đồng bộ thủ công từ trình duyệt.
- Cập nhật trang tài khoản, thiết bị, người dùng, cảm biến, trang tổng quan và các màn hình liên quan để phù hợp với dữ liệu backend.

## Tài liệu và xác minh

- Cập nhật `README.md`, `.env.example` và tài liệu fixture theo cách tích hợp backend hiện tại.
- Cập nhật kiểm thử quyền truy cập và kiểm thử quản lý thông tin xác thực gateway.
- Đã chạy thành công `npm test`, `npm run lint` và `npm run build`.

## Giao diện trên thiết bị di động

- Kiểm tra và xác nhận giao diện frontend đã có responsive, không cần bổ sung thêm mã giao diện cho yêu cầu này.
- Ở màn hình rộng dưới 1024px, thanh điều hướng bên trái chuyển thành drawer; có nút mở/đóng và lớp nền để đóng drawer.
- Ở màn hình dưới 768px, thanh trên cùng và vùng nội dung được sắp xếp lại, giảm khoảng đệm để phù hợp với màn hình nhỏ.
- Các bảng dữ liệu hỗ trợ cuộn ngang; những trang dashboard, quản lý, báo cáo, đồng bộ và nhiệm vụ có breakpoint riêng để điều chỉnh bố cục.
- File `index.html` đã khai báo viewport cho thiết bị di động.

## Trang hồ sơ cá nhân

- Thêm trang hồ sơ dùng chung cho tất cả vai trò, hiển thị tên, email, vai trò hiện tại và các vai trò được cấp.
- Thêm mục Hồ sơ vào thanh điều hướng và liên kết từ menu tài khoản.
- Ẩn biểu mẫu nhập mật khẩu cho đến khi người dùng chọn “Đổi mật khẩu”; cho phép đóng biểu mẫu nếu chưa muốn tiếp tục.
- Thêm chức năng đổi mật khẩu qua API hiện có; yêu cầu xác nhận mật khẩu mới và tối thiểu 12 ký tự.
- Sau khi đổi mật khẩu thành công, đăng xuất phiên hiện tại và hướng người dùng về trang đăng nhập.
- Thiết kế bố cục co giãn phù hợp với màn hình điện thoại.

## Ngôn ngữ giao diện theo vai trò

- Hiển thị các nhãn và nội dung giao diện dành cho Farm Owner bằng tiếng Việt; giao diện dành cho các vai trò khác giữ tiếng Anh.
- Áp dụng bản dịch cho điều hướng, thanh trên cùng, hồ sơ, dashboard và các trang nghiệp vụ mà Farm Owner được phép truy cập.
- Không dịch hoặc thay đổi dữ liệu do API trả về, bao gồm tên, trạng thái, số đo và nội dung bản ghi.
