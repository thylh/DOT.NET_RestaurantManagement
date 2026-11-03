# Plan dự án: Phần mềm quản lý nhà hàng

## 1. Mục tiêu

Xây dựng ứng dụng desktop phục vụ bài tập lớn, mô phỏng các nghiệp vụ nội bộ cơ bản của một nhà hàng: quản lý tài khoản và nhân viên, bàn, thực đơn, order, thanh toán và báo cáo.

### Phạm vi triển khai

- Ứng dụng chạy cục bộ trên một máy Windows.
- Dữ liệu được lưu trên MySQL Server 8.4 LTS chạy cục bộ; có thể quản lý bằng MySQL Workbench.
- Chỉ có hai vai trò trong ứng dụng: `Admin` và `Staff`.
- Không triển khai truy cập qua mạng LAN hoặc nhiều máy dùng chung database.
- Không sử dụng ASP.NET Core, Web API, JWT hay kiến trúc client-server.

MySQL Server là database lưu dữ liệu; tài khoản `Admin` và `Staff` là tài khoản đăng nhập ứng dụng. Hai loại này không phải một.

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
- Quản lý tài khoản và hồ sơ nhân viên.
- Quản lý bàn, danh mục món và món ăn.
- Xem hóa đơn, lịch sử giao dịch và báo cáo.

### Staff

- Đăng nhập và đổi mật khẩu.
- Nếu tài khoản chưa từng có hồ sơ hoặc hồ sơ đã bị xóa, vẫn được đăng nhập nhưng chỉ xem nghiệp vụ; giao diện luôn hiển thị cảnh báo và không bắt buộc đổi mật khẩu tạm.
- Xem bàn, danh mục và món đang kinh doanh.
- Tạo/cập nhật order.
- Thực hiện thanh toán và tra cứu biên nhận theo quyền được cấp.

Quyền phải được kiểm tra trong service nghiệp vụ, không chỉ ẩn hoặc hiện nút trên giao diện. Không tạo thêm vai trò ngoài Admin/Staff trong phiên bản đầu.

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

### 5.2 Quản lý nhân viên

- Tạo, xem, sửa và ngừng hoạt động hồ sơ nhân viên.
- Lưu thông tin cần thiết cho bài tập lớn, ví dụ họ tên, liên hệ và trạng thái làm việc.
- Phân biệt hồ sơ nhân viên (`Employee`) với tài khoản đăng nhập (`UserAccount`); đình chỉ hồ sơ chặn đăng nhập, còn xóa hồ sơ cho phép đăng nhập chỉ xem để có thể tái sử dụng tài khoản khi nhân viên quay lại.
- Hồ sơ mới và hồ sơ đã bị xóa có trạng thái hiển thị riêng trong danh sách tài khoản; tạo lại hồ sơ sẽ xóa trạng thái đã bị xóa.
- Số điện thoại (nếu có) chỉ chứa chữ số và ký tự định dạng thông dụng, có 7–15 chữ số; email (nếu có) phải đúng định dạng.
- Không triển khai lương, chấm công hoặc quản lý ca trong phiên bản đầu.

### 5.3 Quản lý bàn và thực đơn

- Admin quản lý bố trí bàn (khu vực, số hiệu dạng văn bản và số chỗ ngồi), danh mục và món ăn.
- Số hiệu bàn là văn bản tự do (ví dụ `Bàn 1`, `A1`); không trùng số hiệu trong cùng một khu vực.
- Khu vực chọn từ danh sách `Chung`, `Riêng tư`, `VIP` và `Ngoài trời`; khu vực cũ đã có trong dữ liệu vẫn được giữ để không làm mất cấu hình.
- Admin quản lý trạng thái phục vụ: ngừng phục vụ hoặc mở lại bàn; không thể ngừng bàn đang có order mở.
- Nhân viên chỉ xem thực đơn đang sử dụng, không thay đổi cấu hình.
- Sơ đồ bố trí bàn trực quan trên mặt bằng nhà hàng là phần mở rộng sau; phiên bản hiện tại quản lý khu vực bằng thông tin bàn.
- Staff xem tình trạng sử dụng là `Trống` hoặc `Có khách` (suy ra từ order mở) để phục vụ order; không quản lý trạng thái phục vụ.
- Chỉ xóa bàn chưa từng có order; không ngừng phục vụ hoặc xóa bàn đang có order mở.
- Món ăn có tên, giá, mô tả, ảnh tùy chọn, danh mục và trạng thái còn/hết hoặc đang kinh doanh.
- Có tìm kiếm/lọc món.
- Dùng trạng thái ngừng kinh doanh thay vì xóa món đã được tham chiếu trong giao dịch.

### 5.4 Order

- Chỉ Staff có hồ sơ đang hoạt động được tạo và xử lý order; Admin chỉ quản lý cấu hình, không thao tác order.
- Bắt đầu order từ bàn trống; chỉ có tối đa một order đang mở trên mỗi bàn.
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
- Trạng thái có khách được suy ra từ order đang mở; trạng thái ngừng phục vụ do Staff cập nhật và không thể thay đổi khi bàn có order mở.
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

**Kết quả:** Admin và Staff chỉ sử dụng được chức năng tương ứng.

**Trạng thái:** Hoàn thành. Luồng thiết lập Admin lần đầu, đăng nhập/đăng xuất, đổi mật khẩu, quản lý hồ sơ nhân viên và tài khoản, cùng kiểm tra quyền tại service đã được triển khai. Tạo hồ sơ nhân viên được thực hiện từ tài khoản; danh mục Nhân viên cung cấp thao tác tạm đình chỉ/bỏ đình chỉ, sửa số điện thoại và email, hoặc xóa hồ sơ. Tạm đình chỉ chặn đăng nhập. Xóa hồ sơ giữ tài khoản và lịch sử order, bỏ liên kết hồ sơ khỏi order, đồng thời ghi nhận trạng thái “Hồ sơ đã bị xóa” để phân biệt với tài khoản chưa từng có hồ sơ. Tài khoản Staff không có hồ sơ vẫn đăng nhập được ở chế độ chỉ xem, không bị buộc đổi mật khẩu tạm và luôn thấy cảnh báo cố định; Admin không bị giới hạn bởi chế độ này và vẫn vào workspace để thao tác/tải dữ liệu. Không còn chức năng khóa/mở khóa tài khoản; xóa tài khoản đánh dấu tài khoản đã bị xóa, chặn đăng nhập và ẩn khỏi danh sách tài khoản nhưng giữ hồ sơ/order/payment; hồ sơ nhân viên liên kết được chuyển sang ngừng hoạt động. Có thể tạo lại hồ sơ từ tài khoản chưa bị xóa. Số điện thoại/email được kiểm tra định dạng ở cả giao diện và service. Giao diện thao tác bàn, thực đơn và order thuộc các giai đoạn sau.

### Giai đoạn D — Bàn và thực đơn

- Xây dựng CRUD bàn, danh mục và món.
- Thêm validation, tìm kiếm/lọc và trạng thái còn/hết.
- Bảo vệ lịch sử bằng cách ngừng kinh doanh thay vì xóa dữ liệu đã sử dụng.

**Kết quả:** dữ liệu bàn và thực đơn được quản lý, lưu bền vững.

**Trạng thái:** Hoàn thành. Admin quản lý khu vực, số hiệu dạng văn bản, số chỗ ngồi, trạng thái phục vụ, danh mục và món ăn; Staff chỉ xem các bàn đang phục vụ cùng tình trạng `Trống`/`Có khách` để thao tác order. Đã bổ sung tìm kiếm/lọc món, kiểm tra dữ liệu, trạng thái bàn/món và bảo vệ dữ liệu đã được order tham chiếu. Chưa có sơ đồ bố trí bàn trực quan. Các nhãn và thông báo của giao diện cùng luồng tài khoản hiện dùng tiếng Việt.

### Giai đoạn E — Order

- Xây dựng màn hình chọn bàn và tạo order.
- Thêm/sửa/xóa món, số lượng và ghi chú.
- Tính tạm tính/tổng tiền và hiển thị trạng thái order/bàn.
- Lưu thông tin snapshot món và giá.
- Hủy order chưa thanh toán với xác nhận và lý do; giải phóng bàn và giữ lại order đã hủy trong lịch sử.

**Kết quả:** hoàn thành được luồng gọi món cơ bản; quản lý số lượng đã gọi, bỏ món và chuyển/gộp bàn là phần bổ sung sau.

**Trạng thái:** Staff có hồ sơ đang hoạt động có thể bắt đầu order, thêm món/số lượng/ghi chú và hủy order có lý do; mỗi bàn chỉ có một order mở. Admin không có màn hình hoặc quyền thao tác order. Đang hoàn thiện các thao tác chỉnh sửa chi tiết món và lịch sử order.

### Giai đoạn F — Thanh toán và tra cứu

- Xây dựng thanh toán tiền mặt/chuyển khoản mô phỏng.
- Chặn order trống, order đã thanh toán và thanh toán trùng.
- Ghi payment và cập nhật order/bàn trong transaction.
- Sau thanh toán thành công, giải phóng bàn; không cho sửa/hủy giao dịch đã thanh toán trong bản đầu.
- Xây dựng xem/tìm kiếm biên nhận.

**Kết quả:** order được thanh toán và tra cứu nhất quán.

**Trạng thái:** Staff có hồ sơ đang hoạt động có thể thanh toán tiền mặt/chuyển khoản mô phỏng; tổng tiền và payment được lưu cùng chuyển trạng thái order trong transaction. Bàn tự chuyển về trạng thái trống khi order thanh toán hoặc hủy. Tra cứu biên nhận chưa hoàn thiện.

### Giai đoạn G — Báo cáo và hoàn thiện giao diện

- Xây dựng dashboard doanh thu và món bán chạy.
- Chuẩn hóa điều hướng, thông báo lỗi, xác nhận thao tác và trạng thái tải.
- Ảnh nền đăng nhập/thiết lập đặt trong `RestaurantManagement.WPF/Assets/Images`; hỗ trợ JPG/JPEG và PNG.
- Chỉ bổ sung PDF/in nếu đã hoàn thành chức năng bắt buộc.

**Kết quả:** báo cáo dùng dữ liệu thật và giao diện thống nhất.

### Giai đoạn H — Kiểm thử và bàn giao

- Kiểm thử đăng nhập, quyền, CRUD, order, thanh toán và báo cáo.
- Kiểm tra dữ liệu sai/thiếu, order trống, thanh toán trùng và lỗi database.
- Viết hướng dẫn cài đặt prerequisites, cấu hình MySQL, cập nhật database và chạy ứng dụng.
- Chuẩn bị dữ liệu demo và kịch bản trình diễn.

**Kết quả:** ứng dụng chạy ổn định trên một máy và có tài liệu bàn giao.

## 9. Kiểm thử và tiêu chí nghiệm thu

### Kiểm thử tối thiểu

- Đăng nhập đúng/sai; tài khoản bị khóa; kiểm tra quyền Admin/Staff.
- Thiết lập Admin lần đầu thành công trên database chưa có tài khoản; không thể chạy lại sau khi đã có tài khoản.
- CRUD nhân viên, bàn, danh mục và món.
- Tạm đình chỉ nhân viên làm họ không thể đăng nhập; bỏ đình chỉ khôi phục khả năng đăng nhập.
- Sửa hồ sơ nhân viên chỉ cập nhật số điện thoại và email; xóa hồ sơ không xóa hoặc khóa tài khoản, cũng không xóa lịch sử order. Ghi rõ hồ sơ đã bị xóa, bỏ liên kết nhân viên khỏi order và cho phép tạo lại hồ sơ.
- Đăng nhập báo ngay trên form khi tài khoản không tồn tại hoặc nhân viên đang bị đình chỉ; tài khoản chưa có hồ sơ được phép vào chế độ chỉ xem.
- Email và số điện thoại tùy chọn phải đúng định dạng khi nhập.
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
- Có thể khởi chạy ứng dụng trên một máy Windows với MySQL Server 8.4 LTS.
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
