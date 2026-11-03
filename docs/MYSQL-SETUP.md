# Thiết lập MySQL cho ứng dụng

Ứng dụng dùng MySQL Server 8.4 LTS và Oracle MySQL EF Core Provider 8.0.31. MySQL Workbench là công cụ tạo/quản lý schema; ứng dụng kết nối trực tiếp tới MySQL Server.

## 1. Tạo database và tài khoản ứng dụng

Mở kết nối quản trị trong MySQL Workbench rồi chạy SQL sau. Hãy thay mật khẩu mẫu bằng mật khẩu mạnh của riêng bạn; không gửi hoặc ghi mật khẩu vào mã nguồn.

```sql
CREATE DATABASE RestaurantManagement
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

CREATE DATABASE RestaurantManagement_Test
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

CREATE USER 'restaurant_app'@'localhost'
    IDENTIFIED BY 'REPLACE_WITH_A_STRONG_PASSWORD';

GRANT ALL PRIVILEGES ON RestaurantManagement.*
    TO 'restaurant_app'@'localhost';

GRANT ALL PRIVILEGES ON RestaurantManagement_Test.*
    TO 'restaurant_app'@'localhost';
```

Tài khoản này chỉ có quyền trên hai schema của ứng dụng. Quyền tạo và sửa bảng cần thiết vì ứng dụng tự áp dụng EF Core migrations khi khởi động. Với môi trường triển khai thực tế, nên tách tài khoản chạy ứng dụng và tài khoản triển khai migration.

Khi Admin cấp tài khoản nhân viên, ứng dụng tạo tên đăng nhập từ tên (ví dụ `an.nguyen`, thêm số nếu bị trùng) và một mật khẩu tạm ngẫu nhiên. Mật khẩu tạm được mã hóa bằng Windows DPAPI theo tài khoản Windows hiện tại trước khi lưu vào MySQL; mật khẩu rõ được hiển thị ở bảng tài khoản cho đến khi nhân viên đổi mật khẩu, sau đó cột chuyển thành “Đã đổi mật khẩu”. Vì DPAPI gắn với máy và tài khoản Windows, hãy chạy ứng dụng dưới cùng tài khoản Windows để đọc lại các mật khẩu tạm đã cấp. Hãy gửi riêng mật khẩu cho nhân viên. Ứng dụng yêu cầu đổi mật khẩu ngay trong lần đăng nhập đầu tiên.

Các tài khoản đã tạo trước khi có cơ chế lưu mật khẩu tạm được giữ nguyên, nhưng ứng dụng không thể khôi phục mật khẩu rõ từ mật khẩu băm cũ; những tài khoản chưa đổi mật khẩu sẽ hiện “Cần đặt lại mật khẩu”.

## 2. Cấu hình kết nối để chạy ứng dụng

Mở PowerShell trong thư mục solution, đặt connection string cho phiên terminal hiện tại rồi chạy ứng dụng:

```powershell
$env:ConnectionStrings__RestaurantDatabase = "Server=127.0.0.1;Port=3306;Database=RestaurantManagement;User ID=restaurant_app;Password=YOUR_PASSWORD;CharSet=utf8mb4"
dotnet run --project .\RestaurantManagement.WPF\RestaurantManagement.WPF.csproj
```

Ứng dụng tạo/cập nhật cấu trúc bảng bằng migration khi khởi động. Có thể đặt biến môi trường Windows `ConnectionStrings__RestaurantDatabase` thay vì đặt lại biến trong mỗi terminal. Không đưa connection string có mật khẩu vào `appsettings.json`, `launch.json` đã commit, hoặc Git.

## 3. Chạy kiểm thử tích hợp

Kiểm thử database chỉ chạy khi có biến `RESTAURANT_TEST_CONNECTION_STRING`. Tài khoản kiểm thử cần quyền trên schema `RestaurantManagement_Test`; bài test từ chối mọi tên schema khác và xóa dữ liệu trong schema test trước/sau khi chạy.

```powershell
$env:RESTAURANT_TEST_CONNECTION_STRING = "Server=127.0.0.1;Port=3306;Database=RestaurantManagement_Test;User ID=restaurant_app;Password=YOUR_PASSWORD;CharSet=utf8mb4"
dotnet test .\RestaurantManagement.Tests\RestaurantManagement.Tests.csproj
```

Không đặt biến này trỏ tới database thật hoặc database chứa dữ liệu cần giữ lại.

## 4. Dữ liệu hiện có

Ứng dụng kết nối schema `RestaurantManagement` và không tự nhập dữ liệu từ nguồn khác. Nếu cần dùng dữ liệu hiện có, hãy sao lưu và chuyển dữ liệu vào schema MySQL trước khi chạy ứng dụng.
