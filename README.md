# DineEase - Quản lý nhà hàng

DineEase là ứng dụng desktop WPF hỗ trợ vận hành nhà hàng trên một máy Windows. Ứng dụng quản lý tài khoản và nhân viên, bàn, thực đơn, order, thanh toán, khuyến mãi, lịch sử và báo cáo; dữ liệu được lưu trong MySQL.

## Chức năng chính

- **Admin:** cấp và quản lý tài khoản, quản lý hồ sơ nhân viên và tra cứu Audit Log.
- **Manager:** quản lý nhân viên trong phạm vi quyền, bàn, danh mục, món ăn và khuyến mãi; xem lịch sử order và báo cáo doanh thu.
- **Staff:** xem bàn/thực đơn, tạo và cập nhật order, thanh toán và tra cứu order đã thanh toán.
- Thông báo nội bộ qua biểu tượng chuông: gửi theo nhân viên/vai trò, đánh dấu mức khẩn, xem số chưa đọc và đánh dấu đã đọc.
- Nút làm mới hệ thống ở thanh trên tải lại dữ liệu nghiệp vụ theo vai trò hiện đăng nhập; chuông chỉ mở hộp thư.
- Staff gửi yêu cầu cấp lại mật khẩu từ màn hình đăng nhập; Admin xử lý bằng mật khẩu tạm mới, bắt buộc đổi sau khi đăng nhập.
- Thiết lập tài khoản Admin lần đầu khi database chưa có tài khoản.
- Lưu lịch sử order/thanh toán và hỗ trợ xem chi tiết/biên nhận.
- Báo cáo doanh thu có biểu đồ xu hướng theo ngày, top 5 món bán chạy và tỷ trọng phương thức thanh toán; bảng số liệu đầy đủ vẫn có sẵn để đối chiếu.
- KPI đếm số tăng dần; đường và thanh biểu đồ chạy tăng dần, còn thẻ Số liệu trượt lên khi mở. Thẻ Biểu đồ không chạy hiệu ứng trượt toàn trang.
- Biểu tượng và biểu đồ dùng vector/WPF gốc, không thêm thư viện giao diện hoặc ảnh chụp món ăn.

Quyền nghiệp vụ được kiểm tra ở tầng service. Ứng dụng hiện chạy cục bộ; không cung cấp Web API hoặc truy cập database nhiều máy qua mạng LAN.
Thông báo dùng chung database MySQL cục bộ; mỗi phiên bản tải lại hộp thư khi mở chuông và dữ liệu nghiệp vụ khi bấm Làm mới hệ thống, không dùng push/realtime.

## Công nghệ

- C# 12, .NET 8 và WPF
- MVVM với CommunityToolkit.Mvvm
- Entity Framework Core 8 và Oracle MySQL EF Core Provider 8.0.31
- MySQL Server 8.4 LTS

Solution gồm:

| Project | Trách nhiệm |
|---|---|
| `RestaurantManagement.WPF` | Giao diện, ViewModel và khởi tạo ứng dụng |
| `RestaurantManagement.Core` | Mô hình và quy tắc nghiệp vụ cốt lõi |
| `RestaurantManagement.Infrastructure` | EF Core, migrations và truy cập MySQL |
| `RestaurantManagement.Tests` | Kiểm thử nghiệp vụ và tích hợp |

## Yêu cầu

- Windows 10/11
- .NET 8 SDK
- MySQL Server 8.4 LTS đang chạy cục bộ
- MySQL Workbench (tùy chọn, dùng để quản lý database)

## Cài đặt và chạy

1. Tạo database và tài khoản MySQL theo hướng dẫn tại [`docs/MYSQL-SETUP.md`](docs/MYSQL-SETUP.md).
2. Mở PowerShell tại thư mục gốc repository, đặt mật khẩu MySQL vào biến môi trường và dùng biến đó tạo connection string:

   ```powershell
   $env:MYSQL_APP_PASSWORD = Read-Host "MySQL app password"
   $env:ConnectionStrings__RestaurantDatabase = "Server=127.0.0.1;Port=3306;Database=RestaurantManagement;User ID=restaurant_app;Pass" + "word=" + $env:MYSQL_APP_PASSWORD + ";CharSet=utf8mb4"
   ```

3. Chạy ứng dụng:

   ```powershell
   dotnet run --project .\RestaurantManagement.WPF\RestaurantManagement.WPF.csproj
   ```

Ứng dụng tự áp dụng EF Core migrations khi khởi động. Khi database chưa có tài khoản, làm theo màn hình để thiết lập Admin đầu tiên. Không lưu connection string chứa mật khẩu trong repository.

## Kiểm thử

Các bài kiểm thử tích hợp MySQL cần một database kiểm thử riêng tên `RestaurantManagement_Test`. Chúng có thể xóa dữ liệu trong database này; **không trỏ biến kiểm thử vào database thật hoặc database chứa dữ liệu cần giữ**.

Sau khi tạo database kiểm thử và cấp quyền theo [`docs/MYSQL-SETUP.md`](docs/MYSQL-SETUP.md), đặt connection string rồi chạy:

```powershell
$env:RESTAURANT_TEST_CONNECTION_STRING = "Server=127.0.0.1;Port=3306;Database=RestaurantManagement_Test;User ID=restaurant_app;Pass" + "word=" + $env:MYSQL_APP_PASSWORD + ";CharSet=utf8mb4"
dotnet test .\RestaurantManagement.Tests\RestaurantManagement.Tests.csproj
```

Để build toàn bộ solution:

```powershell
dotnet build .\RestaurantManagement.WPF.sln
```

## Tài liệu

- [Hướng dẫn thiết lập MySQL](docs/MYSQL-SETUP.md)
- [Kế hoạch và phạm vi dự án](docs/PLAN.md)
