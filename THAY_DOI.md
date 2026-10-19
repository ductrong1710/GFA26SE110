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
- Bỏ thao tác xóa Farm/Zone và các trường chỉ thuộc dữ liệu giả; giữ các trường và thao tác có API hỗ trợ.
- Ẩn hoặc thông báo chưa hỗ trợ cho chức năng chưa có API phù hợp, như cài đặt hệ thống và đồng bộ thủ công từ trình duyệt.
- Cập nhật trang tài khoản, thiết bị, người dùng, cảm biến, trang tổng quan và các màn hình liên quan để phù hợp với dữ liệu backend.

## Tài liệu và xác minh

- Cập nhật `README.md`, `.env.example` và tài liệu fixture theo cách tích hợp backend hiện tại.
- Cập nhật kiểm thử quyền truy cập và kiểm thử quản lý thông tin xác thực gateway.
- Đã chạy thành công `npm test`, `npm run lint` và `npm run build`.
