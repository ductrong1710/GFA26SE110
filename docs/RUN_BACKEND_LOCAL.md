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

PayOS chỉ cần cấu hình khi thử luồng thanh toán. Xem hợp đồng hiện tại trong [API.md](../API.md). Các kiểm thử PayOS dùng client giả lập và không cần khóa thanh toán thật.

## 4. Kiểm thử trước khi push

Trên Windows, sau khi restore, dùng script sau để chạy các bài kiểm thử trên PostgreSQL tạm, riêng biệt với database development:

```powershell
./scripts/Test-Postgres.ps1 -PostgresBin 'C:/Program Files/PostgreSQL/18/bin' -Configuration Release -NoRestore
```

Điều chỉnh đường dẫn theo nơi cài PostgreSQL. GitHub Actions chạy [Backend CI](../.github/workflows/backend-ci.yml) khi push hoặc mở pull request vào `BackEnd`.
