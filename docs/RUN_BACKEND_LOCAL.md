# Chạy backend SmartFarm sau khi pull

## 1. Chuẩn bị

- Cài .NET 10 SDK và PostgreSQL 18. Các lệnh dưới đây dùng PowerShell 7.
- Clone repo hoặc cập nhật nhánh `BackEnd`, rồi mở terminal tại thư mục chứa `FarmMonitoring.slnx`.
- Tạo một database PostgreSQL dành riêng cho development, chẳng hạn `smartfarm_dev`, do tài khoản PostgreSQL của bạn sở hữu.

```powershell
git switch BackEnd
git pull --ff-only origin BackEnd
dotnet tool restore
dotnet restore FarmMonitoring.slnx
```

## 2. Cấu hình và cập nhật database

Đặt chuỗi kết nối bằng thông tin PostgreSQL trên máy bạn. Dùng một JWT key ngẫu nhiên, tối thiểu 32 byte, và lưu lại trong User Secrets để lần mở terminal sau vẫn dùng được.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Host=localhost;Port=5432;Database=smartfarm_dev;Username=postgres;Password=<mat-khau-local>' --project src/FarmMonitoring.Api
$smartFarmJwtKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set 'Jwt:SecretKey' $smartFarmJwtKey --project src/FarmMonitoring.Api
Remove-Variable smartFarmJwtKey
dotnet ef database update --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
```

Chạy lại `dotnet ef database update` sau khi pull bản có migration mới. API không tự chạy migration khi khởi động. Migration `RemoveLegacyVnpay` xóa dữ liệu thanh toán VNPAY cũ. Khi nâng cấp một database đã có dữ liệu, kiểm tra đúng môi trường đích và sao lưu trước.

## 3. Tạo Admin test và chạy API

Với database trống, đặt hai biến dưới đây trước lần chạy đầu tiên. Dùng địa chỉ và mật khẩu riêng cho môi trường test. Seeder chỉ tạo Admin đầu tiên, sau đó có thể đăng nhập để tạo tài khoản kỹ sư.

```powershell
$env:SEED_ADMIN_EMAIL = 'admin@example.test'
$env:SEED_ADMIN_PASSWORD = '<mat-khau-manh-danh-cho-test>'
dotnet dev-certs https --trust
dotnet run --project src/FarmMonitoring.Api --launch-profile https
```

Mở `https://localhost:7043/swagger` để kiểm tra API. Sau lần seed thành công, có thể xóa hai biến seed khỏi phiên terminal. Giữ cấu hình cá nhân, mật khẩu, JWT và khóa PayOS ngoài Git.

## 4. Cấu hình PayOS khi cần thử thanh toán

Backend vẫn chạy khi chưa có khóa PayOS. Ba giá trị `ClientId`, `ApiKey` và `ChecksumKey` chỉ cần khi thử tạo link thanh toán. Lấy chúng từ kênh PayOS dành cho thử nghiệm. Nếu cần chia sẻ quyền truy cập với đồng đội, dùng kho mật khẩu hoặc kênh chia sẻ secret riêng.

Chạy các lệnh tại thư mục backend và thay nội dung trong dấu `<...>` bằng giá trị của bạn:

```powershell
dotnet user-secrets set 'PayOS:ClientId' '<client-id>' --project src/FarmMonitoring.Api
dotnet user-secrets set 'PayOS:ApiKey' '<api-key>' --project src/FarmMonitoring.Api
dotnet user-secrets set 'PayOS:ChecksumKey' '<checksum-key>' --project src/FarmMonitoring.Api
dotnet user-secrets set 'PayOS:ReturnUrl' 'https://localhost:5173/payments/payos/return' --project src/FarmMonitoring.Api
dotnet user-secrets set 'PayOS:CancelUrl' 'https://localhost:5173/payments/payos/cancel' --project src/FarmMonitoring.Api
```

Các URL mẫu giả định frontend đang chạy HTTPS trên cổng 5173. Backend yêu cầu cả hai URL dùng HTTPS để bật tạo link. Nếu dùng Docker Compose development của dự án trên Windows, Compose lấy ba khóa từ User Secrets của máy host qua thư mục `%APPDATA%\Microsoft\UserSecrets\FarmMonitoring-Api-Development` và đặt URL trả về theo cấu hình Compose. Sau khi cấu hình, khởi động lại API. Giữ các giá trị thật ngoài Git, kể cả file `.env` và `secrets.json`.

Nếu cần PayOS gửi webhook vào máy local, dùng địa chỉ HTTPS công khai trỏ đến môi trường thử nghiệm và đăng ký endpoint `/api/payments/payos/webhook` cho kênh PayOS đó. URL localhost chỉ dùng để trình duyệt quay về frontend. Xem [API.md](../API.md) để biết hợp đồng hiện tại. Các kiểm thử PayOS dùng client giả lập.

## 5. Kiểm thử trước khi push

Trên Windows, sau khi restore, dùng script sau để chạy các bài kiểm thử trên PostgreSQL tạm, riêng biệt với database development:

```powershell
./scripts/Test-Postgres.ps1 -PostgresBin 'C:/Program Files/PostgreSQL/18/bin' -Configuration Release -NoRestore
```

Điều chỉnh đường dẫn theo nơi cài PostgreSQL. GitHub Actions chạy [Backend CI](../.github/workflows/backend-ci.yml) khi push hoặc mở pull request vào `BackEnd`.
