# Plan dự án: Phần mềm quản lý nhà hàng

## 1. Mục tiêu

Xây dựng ứng dụng desktop phục vụ bài tập lớn, mô phỏng các nghiệp vụ nội bộ cơ bản của một nhà hàng: quản lý tài khoản và nhân viên, bàn, thực đơn, order, thanh toán và báo cáo.

### Phạm vi triển khai

- Ứng dụng chạy cục bộ trên một máy Windows.
- Dữ liệu được lưu bằng SQL Server LocalDB trên cùng máy.
- Chỉ có hai vai trò trong ứng dụng: `Admin` và `Staff`.
- Không triển khai truy cập qua mạng LAN hoặc nhiều máy dùng chung database.
- Không sử dụng ASP.NET Core, Web API, JWT hay kiến trúc client-server.

LocalDB là database lưu dữ liệu; tài khoản `Admin` và `Staff` là tài khoản đăng nhập ứng dụng. Hai loại này không phải một.

## 2. Công nghệ

- C# 12 và .NET 8.
- WPF cho giao diện desktop.
- MVVM với CommunityToolkit.Mvvm.
- Entity Framework Core 8 và provider SQL Server.
- SQL Server LocalDB.
- Microsoft.Extensions.DependencyInjection cho Dependency Injection.
- BCrypt.Net-Next để hash mật khẩu.
- Microsoft.Extensions.Logging cho logging.
- FluentValidation nếu cần validation tách khỏi ViewModel/service.
- QuestPDF chỉ cân nhắc nếu việc xuất PDF được đưa vào phạm vi sau khi nghiệp vụ cốt lõi hoàn tất.

Không thêm thư viện hoặc công nghệ mới nếu chưa có nhu cầu cụ thể và chưa kiểm tra tương thích với .NET 8.

## 3. Kiến trúc solution

Tổ chức solution theo trách nhiệm:

- `RestaurantManagement.WPF`: Views, ViewModels, điều hướng, binding, tài nguyên giao diện và khởi tạo ứng dụng.
- `RestaurantManagement.Core`: entities, enums, interfaces và quy tắc nghiệp vụ độc lập với WPF/EF Core.
- `RestaurantManagement.Infrastructure`: `DbContext`, cấu hình EF Core, migrations, khởi tạo dữ liệu và truy cập SQL Server LocalDB.
- `RestaurantManagement.Tests`: kiểm thử nghiệp vụ và các luồng dữ liệu quan trọng.

Luồng phụ thuộc dự kiến:

```text
WPF -> Core
WPF -> Infrastructure (chỉ tại composition root/khởi tạo DI)
Infrastructure -> Core
Tests -> Core và Infrastructure khi cần
```

View và code-behind không truy vấn database trực tiếp. ViewModel gọi service; service thực hiện nghiệp vụ và làm việc với dữ liệu qua các abstraction phù hợp. Không bắt buộc tạo Repository cho mọi entity.

## 4. Vai trò và quyền

### Admin

- Đăng nhập và đổi mật khẩu.
- Quản lý tài khoản và hồ sơ nhân viên.
- Quản lý bàn, danh mục món và món ăn.
- Xem hóa đơn, lịch sử giao dịch và báo cáo.

### Staff

- Đăng nhập và đổi mật khẩu.
- Xem bàn, danh mục và món đang kinh doanh.
- Tạo/cập nhật order.
- Thực hiện thanh toán và tra cứu biên nhận theo quyền được cấp.

Quyền phải được kiểm tra trong service nghiệp vụ, không chỉ ẩn hoặc hiện nút trên giao diện. Không tạo thêm vai trò ngoài Admin/Staff trong phiên bản đầu.

## 5. Phạm vi chức năng

### 5.1 Đăng nhập và tài khoản

- Đăng nhập/đăng xuất.
- Hash và xác minh mật khẩu bằng BCrypt; tuyệt đối không lưu mật khẩu dạng rõ.
- Admin tạo, cập nhật, khóa hoặc mở khóa tài khoản.
- Liên kết tài khoản với hồ sơ nhân viên.
- Không xóa cứng tài khoản đã gắn với giao dịch; khóa tài khoản khi cần.
- Khi database chưa có tài khoản nào, hiển thị quy trình thiết lập lần đầu để tạo tài khoản Admin; không tạo sẵn mật khẩu mặc định trong mã nguồn.
- Sau khi khởi tạo Admin, không cho quy trình thiết lập lần đầu chạy lại nếu đã tồn tại tài khoản.

### 5.2 Quản lý nhân viên

- Tạo, xem, sửa và ngừng hoạt động hồ sơ nhân viên.
- Lưu thông tin cần thiết cho bài tập lớn, ví dụ họ tên, liên hệ và trạng thái làm việc.
- Phân biệt hồ sơ nhân viên (`Employee`) với tài khoản đăng nhập (`UserAccount`).
- Không triển khai lương, chấm công hoặc quản lý ca trong phiên bản đầu.

### 5.3 Quản lý bàn và thực đơn

- CRUD bàn, danh mục và món ăn.
- Món ăn có tên, giá, mô tả, ảnh tùy chọn, danh mục và trạng thái còn/hết hoặc đang kinh doanh.
- Có tìm kiếm/lọc món.
- Dùng trạng thái ngừng kinh doanh thay vì xóa món đã được tham chiếu trong giao dịch.

### 5.4 Order

- Tạo order cho một bàn.
- Thêm món, tăng/giảm số lượng, xóa món và ghi chú cho từng món.
- Hiển thị tạm tính và tổng tiền.
- Ghi nhận nhân viên tạo hoặc phụ trách order.
- Chỉ cho phép tối đa một order đang mở trên một bàn trong phiên bản đầu.
- Chỉ được hủy order chưa thanh toán; yêu cầu xác nhận và lưu người hủy, thời điểm hủy cùng lý do.
- Order đã thanh toán không được sửa hoặc hủy trực tiếp trong phiên bản đầu; hoàn tiền/điều chỉnh giao dịch nằm ngoài phạm vi.
- Chuyển bàn, gộp bàn và order mang đi để ngoài phạm vi ban đầu, trừ khi được yêu cầu bổ sung.

### 5.5 Thanh toán và biên nhận

- Hỗ trợ tiền mặt và ghi nhận chuyển khoản mô phỏng; không tích hợp ngân hàng/cổng thanh toán thật.
- Chỉ thanh toán order đang mở, ngăn thanh toán trùng.
- Lưu số tiền, phương thức, thời điểm và nhân viên thực hiện.
- Sau thanh toán, cập nhật trạng thái order và giải phóng bàn nhất quán.
- Cho phép xem chi tiết và tìm biên nhận theo mã hoặc ngày.
- Chỉ tạo entity `Invoice` riêng nếu có dữ liệu hóa đơn cần quản lý độc lập; nếu không, có thể tạo biên nhận từ order và payment.
- Giảm giá và thuế chỉ thêm khi đã xác định rõ quy tắc tính.

### 5.6 Báo cáo

- Doanh thu theo ngày hoặc khoảng ngày.
- Số order đã thanh toán.
- Doanh thu theo phương thức thanh toán.
- Món bán chạy.
- Báo cáo phải truy vấn dữ liệu đã lưu, không dùng số liệu viết cứng.

## 6. Mô hình dữ liệu dự kiến

Các entity nghiệp vụ đặt trong `RestaurantManagement.Core/Entities`:

- `Employee`: hồ sơ nhân viên.
- `UserAccount`: tên đăng nhập, password hash, vai trò, trạng thái và liên kết nhân viên.
- `RestaurantTable`: thông tin và trạng thái bàn.
- `Category`: danh mục món.
- `Product`: thông tin món ăn.
- `RestaurantOrder`: bàn, nhân viên, thời điểm và trạng thái order.
- `OrderDetail`: món, số lượng, ghi chú, tên món snapshot và đơn giá snapshot.
- `Payment`: số tiền, phương thức, thời điểm và trạng thái thanh toán.
- `Invoice` (tùy chọn): số/mã hóa đơn nếu cần lưu tách biệt.

Các enum dự kiến:

- `UserRole`: `Admin`, `Staff`.
- Trạng thái bàn được xác định từ order đang mở: có order mở thì bàn đang phục vụ, không có order mở thì bàn trống. Chỉ lưu trạng thái riêng cho bàn ngừng sử dụng nếu cần.
- `OrderStatus`: tối thiểu `Open`, `Paid`, `Cancelled`.
- `PaymentMethod`: tối thiểu `Cash`, `BankTransfer`.
- `PaymentStatus`: tối thiểu `Completed`; chỉ bổ sung trạng thái khác khi có luồng nghiệp vụ tương ứng.

EF Core mapping, khóa ngoại, chỉ mục và cấu hình database thuộc `Infrastructure`, không đặt trong Core.

## 7. Quy tắc nghiệp vụ và dữ liệu

- Dùng `decimal` cho tiền; số lượng phải lớn hơn 0.
- Lưu tên món và đơn giá tại thời điểm gọi trong `OrderDetail` để lịch sử không đổi khi món được sửa.
- Tính tổng tiền từ chi tiết order trong service; không tin tổng tiền do giao diện gửi lên.
- Dùng transaction khi thanh toán để ghi payment và cập nhật order/bàn cùng nhau.
- Khi order được thanh toán hoặc hủy hợp lệ, bàn tự trở về trạng thái trống; không lưu trạng thái bàn phục vụ nếu trạng thái đó có thể suy ra từ order mở.
- Hủy order chỉ áp dụng trước thanh toán, cần xác nhận và ghi nhận lý do/người thực hiện/thời điểm; không xóa order khỏi lịch sử.
- Không hỗ trợ hoàn tiền hoặc sửa giao dịch đã thanh toán trong phiên bản đầu.
- Không xóa cứng order/payment/hóa đơn đã phát sinh; dùng trạng thái hoặc lưu lịch sử.
- Kiểm tra dữ liệu đầu vào và quyền thao tác tại service.
- Lỗi database phải được thông báo/log phù hợp; không hiển thị thành công nếu thao tác chưa lưu được.

## 8. Các giai đoạn triển khai

### Giai đoạn A — Khảo sát và khởi tạo

- Kiểm tra workspace và các hướng dẫn hiện có trước khi sửa.
- Tạo solution/projects theo kiến trúc đã thống nhất.
- Cấu hình target framework, project references, MVVM, DI và logging.
- Xác nhận LocalDB có thể chạy trên môi trường phát triển.

**Kết quả:** solution build được và ứng dụng khởi động.

**Trạng thái:** Hoàn thành ngày 2026-10-26.

### Giai đoạn B — Database và dữ liệu nền

- Tạo entities, enums, quan hệ và cấu hình EF Core.
- Cấu hình connection string LocalDB bên ngoài mã nguồn nếu phù hợp.
- Tạo migration đầu tiên và quy trình cập nhật database.
- Tạo dữ liệu mẫu tối thiểu và cách khởi tạo Admin ban đầu an toàn.
- Cung cấp service thiết lập Admin lần đầu khi database chưa có tài khoản; xác minh không thể tạo lại tài khoản sau khi đã hoàn tất. Giao diện thiết lập được triển khai ở Giai đoạn C.
- Xác minh dữ liệu tồn tại sau khi đóng và mở lại ứng dụng.

**Kết quả:** ứng dụng đọc/ghi được dữ liệu trên LocalDB.

**Trạng thái:** Hoàn thành ngày 2026-10-26. Migration được quản lý bằng EF Core; chạy `dotnet ef database update` từ thư mục solution để áp dụng thủ công khi cần. Ứng dụng cũng tự áp dụng migration khi khởi động.

### Giai đoạn C — Đăng nhập và nhân viên

- Xây dựng đăng nhập/đăng xuất và xử lý phiên đăng nhập.
- Hash/verify mật khẩu.
- Xây dựng chức năng Admin quản lý nhân viên và tài khoản.
- Xây dựng luồng thiết lập Admin lần đầu khi database chưa có tài khoản.
- Áp dụng kiểm tra quyền trong service và điều hướng giao diện.

**Kết quả:** Admin và Staff chỉ sử dụng được chức năng tương ứng.

**Trạng thái:** Hoàn thành ngày 2026-10-27. Luồng thiết lập Admin lần đầu, đăng nhập/đăng xuất, đổi mật khẩu, quản lý hồ sơ nhân viên và tài khoản, khóa/mở khóa, cùng kiểm tra quyền tại service đã được triển khai. Giao diện thao tác bàn, thực đơn và order thuộc các giai đoạn sau.

### Giai đoạn D — Bàn và thực đơn

- Xây dựng CRUD bàn, danh mục và món.
- Thêm validation, tìm kiếm/lọc và trạng thái còn/hết.
- Bảo vệ lịch sử bằng cách ngừng kinh doanh thay vì xóa dữ liệu đã sử dụng.

**Kết quả:** dữ liệu bàn và thực đơn được quản lý, lưu bền vững.

**Trạng thái:** Hoàn thành ngày 2026-10-28. Admin có thể quản lý bàn, danh mục và món ăn; Staff chỉ xem dữ liệu đang sử dụng. Đã bổ sung tìm kiếm/lọc món, kiểm tra dữ liệu, trạng thái bàn/món và bảo vệ dữ liệu đã được order tham chiếu. Các nhãn và thông báo của giao diện cùng luồng tài khoản hiện dùng tiếng Việt.

### Giai đoạn E — Order

- Xây dựng màn hình chọn bàn và tạo order.
- Thêm/sửa/xóa món, số lượng và ghi chú.
- Tính tạm tính/tổng tiền và hiển thị trạng thái order/bàn.
- Lưu thông tin snapshot món và giá.
- Hủy order chưa thanh toán với xác nhận và lý do; giải phóng bàn và giữ lại order đã hủy trong lịch sử.

**Kết quả:** hoàn thành được luồng gọi món trước thanh toán.

### Giai đoạn F — Thanh toán và tra cứu

- Xây dựng thanh toán tiền mặt/chuyển khoản mô phỏng.
- Chặn order trống, order đã thanh toán và thanh toán trùng.
- Ghi payment và cập nhật order/bàn trong transaction.
- Sau thanh toán thành công, giải phóng bàn; không cho sửa/hủy giao dịch đã thanh toán trong bản đầu.
- Xây dựng xem/tìm kiếm biên nhận.

**Kết quả:** order được thanh toán và tra cứu nhất quán.

### Giai đoạn G — Báo cáo và hoàn thiện giao diện

- Xây dựng dashboard doanh thu và món bán chạy.
- Chuẩn hóa điều hướng, thông báo lỗi, xác nhận thao tác và trạng thái tải.
- Chỉ bổ sung PDF/in nếu đã hoàn thành chức năng bắt buộc.

**Kết quả:** báo cáo dùng dữ liệu thật và giao diện thống nhất.

### Giai đoạn H — Kiểm thử và bàn giao

- Kiểm thử đăng nhập, quyền, CRUD, order, thanh toán và báo cáo.
- Kiểm tra dữ liệu sai/thiếu, order trống, thanh toán trùng và lỗi database.
- Viết hướng dẫn cài đặt prerequisites, cấu hình LocalDB, cập nhật database và chạy ứng dụng.
- Chuẩn bị dữ liệu demo và kịch bản trình diễn.

**Kết quả:** ứng dụng chạy ổn định trên một máy và có tài liệu bàn giao.

## 9. Kiểm thử và tiêu chí nghiệm thu

### Kiểm thử tối thiểu

- Đăng nhập đúng/sai; tài khoản bị khóa; kiểm tra quyền Admin/Staff.
- Thiết lập Admin lần đầu thành công trên database chưa có tài khoản; không thể chạy lại sau khi đã có tài khoản.
- CRUD nhân viên, bàn, danh mục và món.
- Giá/số lượng không hợp lệ bị từ chối.
- Tạo order, cập nhật số lượng, ghi chú và tính tổng.
- Hủy order chưa thanh toán cần xác nhận, lưu lý do/người hủy/thời điểm và giải phóng bàn.
- Không thể hủy hoặc chỉnh sửa order đã thanh toán trong phiên bản đầu.
- Thông tin món/giá cũ không đổi khi sửa món hiện tại.
- Không thanh toán order trống hoặc thanh toán order hai lần.
- Lỗi trong quy trình thanh toán không để lại dữ liệu cập nhật dở dang.
- Báo cáo khớp với dữ liệu thanh toán mẫu.

### Điều kiện hoàn thành

- Solution build thành công.
- Có thể khởi chạy ứng dụng trên một máy Windows với SQL Server LocalDB.
- Admin quản lý được tài khoản/nhân viên, bàn và thực đơn.
- Staff xử lý được order và thanh toán theo quyền.
- Dữ liệu được lưu và truy xuất lại sau khi khởi động lại ứng dụng.
- Có thể tra cứu biên nhận và xem báo cáo chính.
- Các lỗi nghiệp vụ phổ biến được kiểm tra và hiển thị rõ ràng.
- Có hướng dẫn chạy và dữ liệu/kịch bản demo.

## 10. Bổ sung tùy chọn — ngoài phạm vi bản đầu

Chỉ triển khai các mục dưới đây sau khi toàn bộ chức năng bắt buộc đã hoàn thành và được kiểm thử. Không xem chúng là điều kiện nghiệm thu phiên bản đầu:

- Chuyển bàn và gộp bàn.
- Order mang đi.
- Quản lý ca làm, chấm công hoặc bàn giao tiền.
- Quản lý kho, nguyên liệu và nhập hàng.
- Quản lý khách hàng, đặt bàn và khuyến mãi.
- Xuất PDF hoặc in hóa đơn.
- Hoàn tiền, điều chỉnh giao dịch đã thanh toán và các quy trình kiểm soát nâng cao.

## 11. Hướng dẫn cho coding agent

- Trước khi sửa, kiểm tra cấu trúc workspace, file hướng dẫn và trạng thái hiện có; không giả định solution đã được tạo.
- Thực hiện theo từng giai đoạn, giữ thay đổi tập trung và theo đúng kiến trúc.
- Không thêm API, mạng LAN, nhiều máy, SQLite hoặc tính năng ngoài phạm vi nếu chưa được yêu cầu.
- Không lưu mật khẩu dạng rõ, secret hoặc connection string có thông tin nhạy cảm vào mã nguồn.
- Không bỏ qua lỗi bằng catch rộng hoặc trả về kết quả thành công giả.
- Sau mỗi phần thay đổi, chạy build và các kiểm thử nhỏ nhất liên quan; báo rõ lệnh và kết quả.
- Không ghi đè hoặc xóa các thay đổi có sẵn trong workspace mà agent không tạo.
