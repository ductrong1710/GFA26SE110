# VNPAY Sandbox — MF-01

Chỉ thanh toán thử phí di chuyển trước khảo sát. Không áp dụng tiền thuê/cọc lắp đặt, không dùng merchant production hoặc tiền thật.

## Cấu hình và HTTPS

1. Đăng ký merchant Sandbox với VNPAY để nhận TmnCode/HashSecret. Không gửi secret vào chat, Git, URL hay frontend. Dùng user-secrets cho máy dev hoặc secret store/environment của backend khi deploy.
2. Cấu hình tương đương appsettings (file trong repo giữ Enabled=false và credentials rỗng):

```json
{
  "Vnpay": {
    "Enabled": false,
    "TmnCode": "",
    "HashSecret": "",
    "ReturnUrl": "https://your-frontend.example/payments/vnpay/return",
    "ExpiryMinutes": 15
  }
}
```

Environment keys: `Vnpay__Enabled`, `Vnpay__TmnCode`, `Vnpay__HashSecret`, `Vnpay__ReturnUrl`, `Vnpay__ExpiryMinutes`. Với user-secrets, project là `src/FarmMonitoring.Api`; đặt các key `Vnpay:...` bằng công cụ local và giá trị riêng của bạn. Chỉ bật Enabled=true sau khi cấu hình xong. TmnCode 8 ký tự ASCII chữ/số, expiry 1–60 phút. ReturnUrl HTTPS không query, fragment hoặc userinfo; URL sau khi thêm token phải ≤255 ký tự.

3. Backend và frontend cần HTTPS truy cập công khai (domain dev hoặc tunnel HTTPS). Đăng ký **IPN URL backend** `https://your-api.example/api/payments/vnpay/ipn` trong cấu hình merchant/qua đội hỗ trợ VNPAY. IPN khác Return URL frontend. Backend không tự đăng ký IPN và không thêm IPN URL vào PAY. IPN không yêu cầu JWT; proxy/firewall phải cho phép VNPAY gọi GET trực tiếp, không chuyển sang trang đăng nhập. Không ghi query chứa token/chữ ký vào access log.
4. Route frontend phải hỗ trợ SPA fallback cho `/payments/vnpay/return`. Đặt API base/CORS theo cấu hình hiện hữu. Nếu sau reverse proxy, cấu hình trusted proxy/forwarded headers của ASP.NET Core tại hạ tầng; không tin X-Forwarded-For tùy ý. PAY lấy IP từ Connection.RemoteIpAddress của request Admin tạo link (không suy đoán IP khách mở link sau đó).
5. Sao lưu rồi deploy migration bằng quy trình EF của repo trước khi bật feature. Không đổi merchant/secret khi còn giao dịch chưa xử lý; tắt Enabled vẫn cho IPN đã ký đúng xử lý. Sandbox endpoint được cố định trong code, không nhận endpoint từ request.

## Demo

- Dùng DB Sandbox riêng. T1/T2 chưa được nghiệp vụ chốt: không kích hoạt biểu phí thật. Test suite dùng ngưỡng giả lập 20/80; demo 165km có thể dùng báo giá thủ công được Admin duyệt.
- Kỹ sư gửi ON_SITE → Admin APPROVE → tạo/sao chép link VNPAY Sandbox trong chi tiết tư vấn. Mở link bằng trình duyệt không đăng nhập. Dùng phương thức/thẻ test từ [tài liệu PAY chính thức](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html).
- Return trước IPN phải là “Đang xác nhận thanh toán”; chỉnh vnp_ResponseCode trong URL không thay đổi DB. Khi IPN hợp lệ đến, hiện thành công; Admin cập nhật trạng thái và gọi Chốt lịch khảo sát riêng. IPN lặp không thu thêm.
- Duyệt báo cáo tại vườn 0đ sẽ chốt lịch và báo cho kỹ sư. Nhánh trực tuyến và chuyến đi 0đ không có link. Xác nhận bill thủ công chỉ áp dụng cho hồ sơ cũ. Link hết hạn cần đối soát trước khi thu tiếp.

## QueryDr và đối soát

Admin thấy PENDING/UNKNOWN/RECONCILIATION_REQUIRED phải kiểm tra giao dịch bằng QueryDr hoặc nhờ đơn vị tích hợp/VNPAY kiểm tra; không dựa vào ảnh Return URL. Giữ txnRef, merchant, số tiền và thời điểm tạo giao dịch từ lịch sử. Không xóa hàng, sửa trạng thái trực tiếp hoặc tạo receipt thứ hai để bỏ khóa.

QueryDr server-to-server POST JSON tới `https://sandbox.vnpayment.vn/merchant_webapi/api/transaction`. Dùng `vnp_TxnRef` đã lưu và `vnp_TransactionDate` là **CreateDate gốc của PAY**, không phải PayDate. Thời gian định dạng yyyyMMddHHmmss GMT+7. RequestId mới cho mỗi truy vấn; vnp_CreateDate là thời điểm truy vấn; vnp_IpAddr là IP máy truy vấn. Ký HMACSHA512 UTF-8 chuỗi raw, không URL-encode, theo thứ tự:

```text
RequestId|Version|Command|TmnCode|TxnRef|TransactionDate|CreateDate|IpAddr|OrderInfo
```

Các tên trên tương ứng trường `vnp_...`; Version=2.1.0, Command=querydr. Helper `VnpayProtocol.BuildQueryRequest` và `ValidateQueryResponse` có test độc lập. Không đặt HashSecret trong trình duyệt/Postman cloud chia sẻ.

Xác minh chữ ký phản hồi theo [QueryDr chính thức](https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr&refund.html): thứ tự ResponseId, Command, ResponseCode, Message, TmnCode, TxnRef, Amount, BankCode, PayDate, TransactionNo, TransactionType, TransactionStatus, OrderInfo, PromotionCode, PromotionAmount; nối `|`, giữ giá trị rỗng của trường tùy chọn. Kiểm tra merchant/ref/số tiền khớp DB. ResponseCode=00 chỉ là truy vấn thành công; thanh toán cần TransactionType=01 và TransactionStatus=00 cùng thông tin hợp lệ.

Bản này có helper giao thức QueryDr nhưng **không tự gửi truy vấn, không có nút/API mở khóa đối soát hay hoàn tiền**. Với PENDING còn cùng revision và kết quả hợp lệ, yêu cầu VNPAY gửi lại IPN để xử lý bằng luồng đã kiểm tra. Với revision cũ/kết quả mâu thuẫn đã vào RECONCILIATION_REQUIRED, cần quyết định đối soát có người kiểm tra và triển khai thao tác nghiệp vụ riêng; IPN tiếp theo không tự bỏ khóa. Đây là giới hạn có chủ đích để tránh thu hai lần, không coi trạng thái hết hạn hoặc lỗi mạng là FAILED.

## Kiểm thử local

```powershell
./scripts/Test-Postgres.ps1 -PostgresBin D:/IDECode/PostgreSQL/18/bin -LogName vnpay-full.log
```

Script build và test trên cluster PostgreSQL ngẫu nhiên riêng, dừng cluster khi xong. FrontEnd chạy `npm test` và `npm run build`. Test ký callback bằng secret giả lập; không gọi VNPAY thật. Live demo còn cần credentials Sandbox, hai URL HTTPS đã triển khai, đăng ký IPN với VNPAY và kiểm thử phương thức thanh toán test của merchant.

## Bàn giao và kết quả ngày 2026-11-15

- Backend trên nhánh BackEnd: toàn bộ `dotnet build` sạch (0 warning/error); full suite 222 unit +246 integration pass. Sau bổ sung kiểm tra IPN cạnh tranh bill/ONLINE và Swagger, chạy lại filter Vnpay/AuthContractTests: 23 unit +22 integration pass. PostgreSQL18 cô lập; không migrate DB ứng dụng. `has-pending-model-changes` không có thay đổi thiếu migration.
- FrontEnd: `npm test` 118 node +164 component +2 user-management =284 pass; production build pass, còn cảnh báo bundle lớn hiện hữu. Lint các file sửa và git diff --check pass.
- Chưa thực hiện giao dịch với merchant Sandbox thật. Cần TmnCode/HashSecret, frontend/backend HTTPS và IPN đã đăng ký. Các ngưỡng và tiền trong test là dữ liệu giả lập.

Nhóm file thay đổi backend: `VnpayController.cs`; `VnpayService.cs`, `VnpayProtocol.cs`, `SiteVisitService.cs`, `SiteVisitContracts.cs`; `IVnpayRepository.cs`; `VnpayTransaction.cs`, `SiteVisitProposal.cs`; `ConsultationRepository.Vnpay.cs`, `ConsultationRepository.SiteVisit.cs`; `VnpayConfiguration.cs`, `SiteVisitConfiguration.cs`; `DependencyInjection.cs`, `appsettings.json`; migration SiteVisitVnpaySandbox/Designer/model snapshot; `VnpayTests.cs`, `VnpayProtocolTests.cs`, `AuthContractTests.cs`; API.md, DATABASE.md, BUSINESS_RULES.md và tài liệu này.

Nhóm file FrontEnd: `src/components/app/SiteVisitPanel.jsx`, `src/pages/VnpayReturnPage.jsx`, `src/App.jsx`, `tests/vnpay.test.jsx`, `tests/vnpay-return.test.jsx`, `package.json`.
