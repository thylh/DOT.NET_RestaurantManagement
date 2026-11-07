# Plan dự án: Phần mềm quản lý nhà hàng

## 1. Mục tiêu

Xây dựng ứng dụng desktop phục vụ bài tập lớn, mô phỏng các nghiệp vụ nội bộ cơ bản của một nhà hàng: quản lý tài khoản và nhân viên, bàn, thực đơn, order, thanh toán và báo cáo.

### Phạm vi triển khai

- Ứng dụng chạy cục bộ trên một máy Windows.
- Dữ liệu được lưu trên MySQL Server 8.4 LTS chạy cục bộ; có thể quản lý bằng MySQL Workbench.
- Có ba vai trò trong ứng dụng: `Admin`, `Manager` (Quản lý) và `Staff`.
- Không triển khai truy cập qua mạng LAN hoặc nhiều máy dùng chung database.
- Không sử dụng ASP.NET Core, Web API, JWT hay kiến trúc client-server.

MySQL Server là database lưu dữ liệu; các vai trò `Admin`, `Manager` và `Staff` là tài khoản đăng nhập ứng dụng. Hai loại này không phải một.

## 2. Công nghệ

- C# 12 và .NET 8.
- WPF cho giao diện desktop.
- MVVM với CommunityToolkit.Mvvm.
- Entity Framework Core 8 và Oracle MySQL EF Core Provider 8.0.31.
- MySQL Server 8.4 LTS.
- Microsoft.Extensions.DependencyInjection cho Dependency Injection.
- BCrypt.Net-Next để hash mật khẩu.
- Microsoft.Extensions.Logging cho logging.
- FluentValidation nếu cần validation tách khỏi ViewModel/service.
- QuestPDF chỉ cân nhắc nếu việc xuất PDF được đưa vào phạm vi sau khi nghiệp vụ cốt lõi hoàn tất.

Không thêm thư viện hoặc công nghệ mới nếu chưa có nhu cầu cụ thể và chưa kiểm tra tương thích với .NET 8.

MySQL Server 8.4 LTS là hệ quản trị cơ sở dữ liệu được sử dụng xuyên suốt dự án.

## 3. Kiến trúc solution

Tổ chức solution theo trách nhiệm:

- `RestaurantManagement.WPF`: Views, ViewModels, điều hướng, binding, tài nguyên giao diện và khởi tạo ứng dụng.
- `RestaurantManagement.Core`: entities, enums, interfaces và quy tắc nghiệp vụ độc lập với WPF/EF Core.
- `RestaurantManagement.Infrastructure`: `DbContext`, cấu hình EF Core, migrations, khởi tạo dữ liệu và truy cập MySQL Server.
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
- Quản lý toàn bộ hồ sơ nhân viên, bao gồm cả hồ sơ của Quản lý: tạo, xem, sửa, đình chỉ/bỏ đình chỉ và xóa hồ sơ.
- Cấp và quản lý tài khoản đăng nhập cho Quản lý và Staff; quản lý quyền tài khoản và tạo hồ sơ cho các tài khoản đã cấp.
- Xem và lọc Audit Log; không sửa hoặc xóa nhật ký.

### Manager (Quản lý)

- Đăng nhập và đổi mật khẩu; tài khoản phải có hồ sơ nhân viên đang hoạt động.
- Xem danh sách nhân viên Staff và hồ sơ chưa gắn tài khoản; không xem hồ sơ của các tài khoản Quản lý.
- Chỉ được đình chỉ nhân viên Staff; không được bỏ đình chỉ, sửa thông tin hoặc xóa hồ sơ. Admin chịu trách nhiệm các thao tác quản trị hồ sơ còn lại.
- Quản lý bàn, danh mục món, món ăn và khuyến mãi.
- Xem toàn bộ lịch sử order, chi tiết/biên nhận, kể cả order đang mở, cùng báo cáo.
- Không quản lý tài khoản đăng nhập/phân quyền và không xử lý order hoặc thanh toán thay Staff.

### Staff

- Đăng nhập và đổi mật khẩu.
- Nếu tài khoản chưa từng có hồ sơ hoặc hồ sơ đã bị xóa, vẫn được đăng nhập nhưng chỉ xem nghiệp vụ; giao diện luôn hiển thị cảnh báo và không bắt buộc đổi mật khẩu tạm.
- Xem bàn, danh mục và món đang kinh doanh.
- Tạo/cập nhật order.
- Thực hiện thanh toán; tra cứu lịch sử chỉ gồm order đã thanh toán và xem chi tiết/biên nhận của các order đó.

Quyền phải được kiểm tra trong service nghiệp vụ, không chỉ ẩn hoặc hiện nút trên giao diện.

## 5. Phạm vi chức năng

### 5.1 Đăng nhập và tài khoản

- Đăng nhập/đăng xuất.
- Hash và xác minh mật khẩu bằng BCrypt; tuyệt đối không lưu mật khẩu dạng rõ.
- Admin tạo, cập nhật và xóa tài khoản; xóa tài khoản thu hồi vĩnh viễn quyền đăng nhập nhưng giữ bản ghi để bảo toàn lịch sử order/thanh toán.
- Liên kết tài khoản với hồ sơ nhân viên.
- Tạm đình chỉ hồ sơ nhân viên sẽ chặn đăng nhập bằng thông báo cụ thể. Không có chức năng khóa tài khoản riêng.
- Xóa hồ sơ nhân viên giữ tài khoản đăng nhập và lịch sử; tài khoản vẫn đăng nhập được ở chế độ chỉ xem, đồng thời hiển thị cảnh báo cố định.
- Tài khoản không có hồ sơ không bị buộc đổi mật khẩu tạm; hiển thị cảnh báo cố định nhắc nhân viên đổi mật khẩu được cấp.
- Tên đăng nhập không tìm thấy được báo riêng ngay trên form đăng nhập.
- Khi database chưa có tài khoản nào, hiển thị quy trình thiết lập lần đầu để tạo tài khoản Admin; không tạo sẵn mật khẩu mặc định trong mã nguồn.
- Sau khi khởi tạo Admin, không cho quy trình thiết lập lần đầu chạy lại nếu đã tồn tại tài khoản.

### 5.1.1 Audit Log

- Lưu nhật ký tập trung trong một bảng `AuditLogs`, phân loại theo tài khoản, nhân viên, danh mục và order.
- Ghi tự động các thao tác tạo/sửa/xóa, đổi vai trò/mật khẩu, đình chỉ/bỏ đình chỉ, thanh toán và hủy order; lưu thời điểm, người thao tác/vai trò, đối tượng, tóm tắt và thay đổi trước/sau.
- Admin có thể tìm kiếm theo người thao tác/đối tượng/nội dung, lọc theo nhóm và khoảng ngày; hiển thị tối đa 500 kết quả gần nhất cho mỗi truy vấn.
- Chỉ Admin được đọc nhật ký. Không có thao tác sửa/xóa nhật ký trong ứng dụng.
- Không lưu mật khẩu, hash mật khẩu, mật khẩu tạm, token hoặc secret vào nội dung thay đổi.

### 5.2 Quản lý nhân viên

- Admin quản lý toàn bộ hồ sơ nhân viên, bao gồm hồ sơ của tài khoản Quản lý.
- Quản lý chỉ xem nhân viên Staff và hồ sơ chưa gắn tài khoản; chỉ có quyền đình chỉ, không được sửa thông tin, xóa hồ sơ hoặc bỏ đình chỉ.
- Lưu thông tin cần thiết cho bài tập lớn, ví dụ họ tên, liên hệ và trạng thái làm việc.
- Phân biệt hồ sơ nhân viên (`Employee`) với tài khoản đăng nhập (`UserAccount`); đình chỉ hồ sơ chặn đăng nhập, còn xóa hồ sơ cho phép đăng nhập chỉ xem để có thể tái sử dụng tài khoản khi nhân viên quay lại.
- Hồ sơ mới và hồ sơ đã bị xóa có trạng thái hiển thị riêng trong danh sách tài khoản; tạo lại hồ sơ sẽ xóa trạng thái đã bị xóa.
- Số điện thoại (nếu có) chỉ chứa chữ số và ký tự định dạng thông dụng, có 7–15 chữ số; email (nếu có) phải đúng định dạng.
- Không triển khai lương, chấm công hoặc quản lý ca trong phiên bản đầu.

### 5.3 Quản lý bàn và thực đơn

- Quản lý quản lý bố trí bàn (khu vực, số hiệu dạng văn bản và số chỗ ngồi), danh mục và món ăn.
- Số hiệu bàn là văn bản tự do (ví dụ `Bàn 1`, `A1`); không trùng số hiệu trong cùng một khu vực.
- Khu vực chọn từ danh sách `Chung`, `Riêng tư`, `VIP` và `Ngoài trời`; khu vực cũ đã có trong dữ liệu vẫn được giữ để không làm mất cấu hình.
- Quản lý quản lý trạng thái phục vụ: ngừng phục vụ hoặc mở lại bàn; không thể ngừng bàn đang có order mở.
- Nhân viên có hồ sơ đang hoạt động được xem thực đơn và cập nhật trạng thái món còn/hết theo tình hình thực tế; không được sửa thông tin món, danh mục hoặc trạng thái kinh doanh.
- Quản lý quản lý trạng thái kinh doanh của danh mục và món. Trạng thái còn/hết món độc lập với trạng thái kinh doanh.
- Sơ đồ bố trí bàn trực quan trên mặt bằng nhà hàng là phần mở rộng sau; phiên bản hiện tại quản lý khu vực bằng thông tin bàn.
- Staff xem tình trạng sử dụng là `Trống` hoặc `Có khách` (suy ra từ order mở) để phục vụ order; không quản lý trạng thái phục vụ.
- Chỉ xóa bàn chưa từng có order; không ngừng phục vụ hoặc xóa bàn đang có order mở.
- Món ăn có tên, giá, tags tùy chọn, ảnh tùy chọn, danh mục, trạng thái còn/hết và trạng thái kinh doanh. Không quản lý tồn kho/nguyên liệu.
- Có tìm kiếm/lọc món.
- Dùng trạng thái ngừng kinh doanh thay vì xóa món đã được tham chiếu trong giao dịch.

### 5.4 Order

- Chỉ Staff có hồ sơ đang hoạt động được tạo và xử lý order; Admin và Quản lý không thao tác order.
- Khu vực Staff tách riêng thao tác chọn bàn và xử lý order; bắt đầu order từ bàn trống ở mục `Chọn bàn`, còn mục `Order` hiển thị thực đơn theo danh mục để chọn món.
- Chỉ có tối đa một order đang mở trên mỗi bàn.
- Thêm món, số lượng và ghi chú; hiển thị tổng tiền từ snapshot giá tại thời điểm gọi.
- Ghi nhận nhân viên tạo hoặc phụ trách order.
- Chỉ được hủy order chưa thanh toán; yêu cầu xác nhận và lưu người hủy, thời điểm hủy cùng lý do.
- Order đã thanh toán không được sửa hoặc hủy trực tiếp trong phiên bản đầu; hoàn tiền/điều chỉnh giao dịch nằm ngoài phạm vi.
- Chuyển bàn, gộp bàn và order mang đi để ngoài phạm vi ban đầu, trừ khi được yêu cầu bổ sung.

### 5.5 Thanh toán và biên nhận

- Hỗ trợ tiền mặt và ghi nhận chuyển khoản mô phỏng; không tích hợp ngân hàng/cổng thanh toán thật.
- Chỉ thanh toán order đang mở, ngăn thanh toán trùng.
- Lưu số tiền, phương thức, thời điểm và nhân viên thực hiện.
- Sau thanh toán, cập nhật trạng thái order và giải phóng bàn nhất quán.
- Order hiển thị tạm tính, khuyến mãi được áp dụng, số tiền giảm và thành tiền.
- Khi thanh toán, lưu tên chương trình và số tiền giảm vào order để bảo toàn lịch sử nếu chương trình khuyến mãi được sửa sau đó.
- Tra cứu lịch sử order theo mã hoặc ngày; xem chi tiết và biên nhận từ order cùng payment đã lưu.
- Chỉ tạo entity `Invoice` riêng nếu có dữ liệu hóa đơn cần quản lý độc lập; nếu không, có thể tạo biên nhận từ order và payment.
- Thuế chưa thuộc phạm vi hiện tại.

### 5.6 Khuyến mãi

- Quản lý tạo, cập nhật và bật/tắt chương trình khuyến mãi theo món cụ thể hoặc danh mục.
- Mỗi chương trình có tên, mức giảm phần trăm và thời gian bắt đầu/kết thúc; ngày kết thúc được tính hết ngày đã chọn.
- Chỉ chương trình đang bật và còn hiệu lực mới được xét áp dụng.
- Khuyến mãi được tính tự động trên order. Nếu có nhiều chương trình đủ điều kiện, chỉ áp dụng chương trình đem lại số tiền giảm lớn nhất; không cộng dồn.
- Mức giảm chỉ tính trên các món thuộc phạm vi của chương trình; giá snapshot của món không bị thay đổi.
- Staff không được quản lý chương trình hoặc tự chọn mức giảm.
- Mã coupon, giảm số tiền cố định, ngưỡng hóa đơn, cộng dồn và các điều kiện nâng cao chưa thuộc phạm vi hiện tại.

### 5.7 Báo cáo

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
- `RestaurantOrder`: bàn, nhân viên, thời điểm/trạng thái order, snapshot tên khuyến mãi và số tiền giảm.
- `OrderDetail`: món, số lượng, ghi chú, tên món snapshot và đơn giá snapshot.
- `Payment`: số tiền, phương thức, thời điểm và trạng thái thanh toán.
- `Promotion`: tên, mức giảm, thời gian hiệu lực, trạng thái và phạm vi áp dụng theo món hoặc danh mục.
- `Invoice` (tùy chọn): số/mã hóa đơn nếu cần lưu tách biệt.

Các enum dự kiến:

- `UserRole`: `Admin`, `Manager`, `Staff`.
- Trạng thái có khách được suy ra từ order đang mở; trạng thái phục vụ do Quản lý quản lý và không thể ngừng bàn khi bàn có order mở.
- `OrderStatus`: tối thiểu `Open`, `Paid`, `Cancelled`.
- `PaymentMethod`: tối thiểu `Cash`, `BankTransfer`.
- `PaymentStatus`: tối thiểu `Completed`; chỉ bổ sung trạng thái khác khi có luồng nghiệp vụ tương ứng.

EF Core mapping, khóa ngoại, chỉ mục và cấu hình database thuộc `Infrastructure`, không đặt trong Core.

## 7. Quy tắc nghiệp vụ và dữ liệu

- Dùng `decimal` cho tiền; số lượng phải lớn hơn 0.
- Lưu tên món và đơn giá tại thời điểm gọi trong `OrderDetail` để lịch sử không đổi khi món được sửa.
- Tính tổng tiền từ chi tiết order trong service; không tin tổng tiền do giao diện gửi lên.
- Chọn khuyến mãi hợp lệ có số tiền giảm lớn nhất trong service; không tin mức giảm do giao diện gửi lên và không cộng dồn chương trình.
- Chụp tên chương trình và số tiền giảm vào order trong giao dịch thanh toán; payment lưu thành tiền sau giảm giá.
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
- Xác nhận MySQL Server có thể chạy trên môi trường phát triển.

**Kết quả:** solution build được và ứng dụng khởi động.

**Trạng thái:** Hoàn thành. Môi trường và solution được cấu hình để sử dụng MySQL Server 8.4 LTS.

### Giai đoạn B — Database và dữ liệu nền

- Tạo entities, enums, quan hệ và cấu hình EF Core.
- Cấu hình connection string MySQL bên ngoài mã nguồn; không lưu mật khẩu trong repository.
- Tạo migration đầu tiên và quy trình cập nhật database.
- Tạo dữ liệu mẫu tối thiểu và cách khởi tạo Admin ban đầu an toàn.
- Cung cấp service thiết lập Admin lần đầu khi database chưa có tài khoản; xác minh không thể tạo lại tài khoản sau khi đã hoàn tất. Giao diện thiết lập được triển khai ở Giai đoạn C.
- Xác minh dữ liệu tồn tại sau khi đóng và mở lại ứng dụng.

**Kết quả:** ứng dụng đọc/ghi được dữ liệu trên MySQL Server.

**Trạng thái:** Hoàn thành. Ứng dụng tự áp dụng migration MySQL khi khởi động; xem [hướng dẫn thiết lập MySQL](./MYSQL-SETUP.md).

### Giai đoạn C — Đăng nhập và nhân viên

- Xây dựng đăng nhập/đăng xuất và xử lý phiên đăng nhập.
- Hash/verify mật khẩu.
- Tách riêng quản lý hồ sơ nhân viên và cấp tài khoản đăng nhập.
- Tự sinh tên đăng nhập theo tên nhân viên, thêm hậu tố khi trùng; tạo mật khẩu tạm ngẫu nhiên an toàn.
- Bắt buộc nhân viên đổi mật khẩu tạm ở lần đăng nhập đầu tiên hoặc sau khi Admin đặt lại mật khẩu.
- Xây dựng luồng thiết lập Admin lần đầu khi database chưa có tài khoản.
- Áp dụng kiểm tra quyền trong service và điều hướng giao diện.

**Kết quả:** Admin, Manager và Staff chỉ sử dụng được chức năng tương ứng.

**Trạng thái:** Hoàn thành. Luồng thiết lập Admin lần đầu, đăng nhập/đăng xuất, đổi mật khẩu và phân quyền đã được triển khai. Admin cấp/quản lý tài khoản, tạo hồ sơ cho tài khoản đã cấp và quản lý toàn bộ hồ sơ nhân viên, kể cả hồ sơ Quản lý. Manager chỉ xem nhân viên Staff/hồ sơ chưa gắn tài khoản và chỉ được đình chỉ; không thể sửa hoặc xóa hồ sơ, cũng không thể bỏ đình chỉ. Staff xử lý nghiệp vụ phục vụ. Tạm đình chỉ hồ sơ chặn đăng nhập. Xóa hồ sơ giữ tài khoản và lịch sử order, bỏ liên kết hồ sơ khỏi order, đồng thời ghi nhận trạng thái “Hồ sơ đã bị xóa” để phân biệt với tài khoản chưa từng có hồ sơ. Tài khoản Staff không có hồ sơ vẫn đăng nhập được ở chế độ chỉ xem, không bị buộc đổi mật khẩu tạm và luôn thấy cảnh báo cố định; tài khoản Manager phải có hồ sơ hoạt động. Không còn chức năng khóa/mở khóa tài khoản; xóa tài khoản đánh dấu tài khoản đã bị xóa, chặn đăng nhập và ẩn khỏi danh sách tài khoản nhưng giữ hồ sơ/order/payment; hồ sơ nhân viên liên kết được chuyển sang ngừng hoạt động. Có thể tạo lại hồ sơ từ tài khoản chưa bị xóa. Số điện thoại/email được kiểm tra định dạng ở cả giao diện và service. Audit Log đã có bảng riêng dùng chung cho các nhóm Account, Employee, Catalog và Order, chỉ Admin được tra cứu.

### Giai đoạn D — Bàn và thực đơn

- Xây dựng CRUD bàn, danh mục và món.
- Thêm validation, tìm kiếm/lọc và trạng thái còn/hết.
- Bảo vệ lịch sử bằng cách ngừng kinh doanh thay vì xóa dữ liệu đã sử dụng.

**Kết quả:** dữ liệu bàn và thực đơn được quản lý, lưu bền vững.

**Trạng thái:** Hoàn thành. Manager quản lý khu vực, số hiệu dạng văn bản, số chỗ ngồi, trạng thái phục vụ, danh mục và món ăn; Staff xem các bàn đang phục vụ cùng tình trạng `Trống`/`Có khách` để thao tác order. Đã có tìm kiếm/lọc thực đơn, tags chọn nhiều, công tắc trạng thái trực tiếp trong bảng và bảo vệ dữ liệu đã được order tham chiếu. Staff có hồ sơ đang hoạt động được phép cập nhật còn/hết món; Manager quản lý trạng thái kinh doanh. Không quản lý kho/nguyên liệu. Chưa có sơ đồ bố trí bàn trực quan.

### Giai đoạn E — Order

- Xây dựng màn hình chọn bàn và tạo order.
- Thêm món với số lượng và ghi chú; bổ sung sửa/xóa từng món đã thêm.
- Tính tạm tính/tổng tiền và hiển thị trạng thái order/bàn.
- Lưu thông tin snapshot món và giá.
- Hủy order chưa thanh toán với xác nhận và lý do; giải phóng bàn và giữ lại order đã hủy trong lịch sử.

**Kết quả:** hoàn thành được luồng gọi món cơ bản; quản lý số lượng đã gọi, bỏ món và chuyển/gộp bàn là phần bổ sung sau.

**Trạng thái:** Staff có hồ sơ đang hoạt động có thể bắt đầu order, thêm/sửa/bỏ món, điều chỉnh số lượng/ghi chú và hủy order có lý do; mỗi bàn chỉ có một order mở. Nhân viên có thể làm việc với nhiều order cùng lúc bằng các tab riêng; đóng tab không hủy order. Admin không có màn hình hoặc quyền thao tác order. Order có hiển thị tạm tính/khuyến mãi/giảm giá/thành tiền. Lịch sử order và biên nhận được triển khai ở Giai đoạn F.

### Giai đoạn F — Thanh toán và tra cứu

- Xây dựng thanh toán tiền mặt/chuyển khoản mô phỏng.
- Chặn order trống, order đã thanh toán và thanh toán trùng.
- Ghi payment và cập nhật order/bàn trong transaction.
- Sau thanh toán thành công, giải phóng bàn; không cho sửa/hủy giao dịch đã thanh toán trong bản đầu.
- Xây dựng xem/tìm kiếm biên nhận.

**Kết quả:** order được thanh toán và tra cứu nhất quán.

**Trạng thái:** Staff có hồ sơ đang hoạt động có thể thanh toán tiền mặt/chuyển khoản mô phỏng; payment và trạng thái order được lưu trong transaction, bàn tự trở về trạng thái trống khi order thanh toán hoặc hủy. Khuyến mãi đủ điều kiện được tính tự động; tên chương trình và số tiền giảm được lưu snapshot. Lịch sử có thể tra cứu theo mã hoặc khoảng ngày; chi tiết/biên nhận được dựng từ snapshot order/payment. Manager xem toàn bộ lịch sử và báo cáo; Staff chỉ xem order đã thanh toán. Admin không xem lịch sử hoặc báo cáo; Audit Log riêng được dành cho Admin.

### Giai đoạn G — Khuyến mãi

- Quản lý chương trình khuyến mãi theo món hoặc danh mục.
- Tự động tính chương trình hợp lệ có mức giảm cao nhất, không cộng dồn.
- Hiển thị tạm tính, khoản giảm và thành tiền trong order.
- Lưu snapshot khuyến mãi khi thanh toán.

**Trạng thái:** Hoàn thành. Đã triển khai quản lý khuyến mãi, quy tắc áp dụng tự động, hiển thị giảm giá và lưu snapshot trên order. Migration được ứng dụng tự áp dụng khi khởi động.

### Giai đoạn H — Báo cáo và hoàn thiện giao diện

- Xây dựng dashboard doanh thu và món bán chạy.
- Chuẩn hóa điều hướng, thông báo lỗi, xác nhận thao tác và trạng thái tải; thông báo thông thường không chặn luồng, xác nhận dành cho thao tác xóa, hủy và hoàn tất thanh toán.
- Ảnh nền đăng nhập/thiết lập đặt trong `RestaurantManagement.WPF/Assets/Images`; hỗ trợ JPG/JPEG và PNG.
- Chỉ bổ sung PDF/in nếu đã hoàn thành chức năng bắt buộc.

**Kết quả:** báo cáo dùng dữ liệu thật và giao diện thống nhất.

**Trạng thái:** Chức năng báo cáo đã triển khai theo khoảng ngày, gồm số order đã thanh toán, doanh thu trước giảm, tổng giảm giá, doanh thu thực nhận, phân tách theo phương thức thanh toán và top 10 món bán chạy. Thông báo thành công/lỗi dùng banner chung; các thao tác xóa, hủy và hoàn tất thanh toán yêu cầu xác nhận. Tiếp tục rà soát và hoàn thiện UI/UX.

**Kế hoạch rà soát UI/UX còn lại:**

- Chỉ áp dụng quy tắc khung hở đáy cho giao diện sau đăng nhập; màn hình đăng nhập, thiết lập Admin lần đầu và thông báo lỗi khởi động trước đăng nhập giữ kiểu viền thông thường.
- Rà các thành phần dùng chung có thể xuất hiện ở cả trước và sau đăng nhập (đặc biệt banner và hộp thoại) để kiểu viền được chọn theo ngữ cảnh, không bị áp dụng nhầm toàn cục.
- Với khung thuộc phạm vi sau đăng nhập, thống nhất độ dày/brush và cạnh trên-trái-phải; cạnh đáy để mở. Cạnh dọc chỉ kéo dài trong vùng bố cục chứa khung và phải dừng trước nội dung/vật cản.
- Rà đường chia cột của DataGrid, nhất là đường ở cột cuối cạnh khung, để không vẽ chồng; giữ đường kẻ dọc phân cột và không thêm đường kẻ ngang.
- Giữ nguyên nghiệp vụ, binding, command, dữ liệu và hành vi điều khiển; thay đổi chỉ thuộc trình bày/layout.
- Xác minh trực quan tại các tab/màn hình sau đăng nhập, gồm bảng có dữ liệu/trống, nội dung mở rộng và các mức scale DPI thường dùng; kiểm tra không có viền đôi, nét đậm/mảnh bất thường, đường kẻ chạy xuyên nội dung hoặc bị cắt sai.

### Giai đoạn I — Kiểm thử và bàn giao

- Kiểm thử đăng nhập, quyền, CRUD, order, thanh toán và báo cáo.
- Kiểm tra dữ liệu sai/thiếu, order trống, thanh toán trùng và lỗi database.
- Viết hướng dẫn cài đặt prerequisites, cấu hình MySQL, cập nhật database và chạy ứng dụng.
- Chuẩn bị dữ liệu demo và kịch bản trình diễn.

**Kết quả:** ứng dụng chạy ổn định trên một máy và có tài liệu bàn giao.

**Trạng thái:** Đã chạy toàn bộ bộ test với MySQL thật: 41/41 đạt, không có test bị bỏ qua. Ca tích hợp xác nhận các luồng tài khoản, phân quyền, nhân viên, danh mục/món, order, thanh toán, báo cáo, Audit Log và dọn dữ liệu database test. Đã sửa các assertion lỗi thời và thứ tự xóa dữ liệu test theo khóa ngoại. Còn chuẩn bị dữ liệu demo, kịch bản trình diễn và tiếp tục rà soát UI/UX trước khi bàn giao.

## 9. Kiểm thử và tiêu chí nghiệm thu

### Kiểm thử tối thiểu

- Đã xác nhận: `dotnet test .\RestaurantManagement.Tests\RestaurantManagement.Tests.csproj` với `RESTAURANT_TEST_CONNECTION_STRING` trỏ chính xác tới `RestaurantManagement_Test`: 41 đạt, 0 lỗi, 0 bỏ qua.
- Đăng nhập đúng/sai; tài khoản đã bị xóa, nhân viên bị đình chỉ hoặc tài khoản không có hồ sơ; kiểm tra quyền Admin/Manager/Staff.
- Thiết lập Admin lần đầu thành công trên database chưa có tài khoản; không thể chạy lại sau khi đã có tài khoản.
- CRUD nhân viên, bàn, danh mục và món.
- Tạm đình chỉ nhân viên làm họ không thể đăng nhập; bỏ đình chỉ khôi phục khả năng đăng nhập.
- Sửa hồ sơ nhân viên chỉ cập nhật số điện thoại và email; xóa hồ sơ không xóa hoặc khóa tài khoản, cũng không xóa lịch sử order. Ghi rõ hồ sơ đã bị xóa, bỏ liên kết nhân viên khỏi order và cho phép tạo lại hồ sơ.
- Đăng nhập báo ngay trên form khi tài khoản không tồn tại hoặc nhân viên đang bị đình chỉ; tài khoản chưa có hồ sơ được phép vào chế độ chỉ xem.
- Email và số điện thoại tùy chọn phải đúng định dạng khi nhập.
- Giá/số lượng không hợp lệ bị từ chối.
- Tạo order, thêm món với số lượng/ghi chú và tính tổng.
- Áp dụng khuyến mãi đúng phạm vi và thời gian; chọn mức giảm tốt nhất mà không cộng dồn; xác nhận payment và snapshot order khớp số tiền sau giảm.
- Staff hợp lệ được báo còn/hết món nhưng không thể thay đổi trạng thái kinh doanh hoặc quản lý khuyến mãi.
- Hủy order chưa thanh toán cần xác nhận, lưu lý do/người hủy/thời điểm và giải phóng bàn.
- Không thể hủy hoặc chỉnh sửa order đã thanh toán trong phiên bản đầu.
- Thông tin món/giá cũ không đổi khi sửa món hiện tại.
- Không thanh toán order trống hoặc thanh toán order hai lần.
- Lỗi trong quy trình thanh toán không để lại dữ liệu cập nhật dở dang.
- Báo cáo khớp với dữ liệu thanh toán mẫu.
- Xác nhận màn hình đăng nhập/thiết lập và lỗi khởi động trước đăng nhập không dùng khung hở đáy; các khung thuộc giao diện sau đăng nhập dùng đúng kiểu đã thống nhất.
- Xác nhận viền ngoài và đường chia cột không bị vẽ trùng; cạnh kéo dài dừng trước nội dung/vật cản và không vượt vùng bố cục của khung.

### Điều kiện hoàn thành

- Solution build thành công.
- Có thể khởi chạy ứng dụng trên một máy Windows với MySQL Server 8.4 LTS.
- Admin quản lý được tài khoản và toàn bộ hồ sơ nhân viên; Manager chỉ đình chỉ nhân viên, đồng thời quản lý bàn, thực đơn và khuyến mãi.
- Manager xem được toàn bộ lịch sử order và báo cáo; Admin không có quyền truy cập các màn nghiệp vụ này.
- Staff xử lý được order và thanh toán theo quyền.
- Dữ liệu được lưu và truy xuất lại sau khi khởi động lại ứng dụng.
- Có thể tra cứu biên nhận và xem báo cáo chính.
- Các lỗi nghiệp vụ phổ biến được kiểm tra và hiển thị rõ ràng.
- Các bảng và khung trong phạm vi UI đã rà soát có viền nhất quán, không chồng nét và không che khuất nội dung.
- Có hướng dẫn chạy và dữ liệu/kịch bản demo.

## 10. Phần chưa hoàn thiện và mở rộng

Các phần chức năng bắt buộc theo phạm vi hiện tại đã được triển khai; bộ test tích hợp MySQL đã đạt. Các đầu việc bàn giao còn lại:

- Chuẩn bị dữ liệu demo và hoàn thiện kịch bản trình diễn.
- Tiếp tục rà soát UI/UX trước khi bàn giao.

Các ý tưởng mở rộng chưa thuộc phạm vi hiện tại:

- Chuyển bàn và gộp bàn.
- Order mang đi.
- Quản lý ca làm, chấm công hoặc bàn giao tiền.
- Quản lý kho, nguyên liệu và nhập hàng.
- Quản lý khách hàng và đặt bàn.
- Coupon, điều kiện giảm giá nâng cao, cộng dồn khuyến mãi.
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
