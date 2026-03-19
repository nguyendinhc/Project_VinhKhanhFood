# Đồ Án: Ứng Dụng Hướng Dẫn Du Lịch Ẩm Thực Vĩnh Khánh

## 📌 Giới thiệu
Ứng dụng hỗ trợ khách du lịch khám phá phố ẩm thực Vĩnh Khánh với các tính năng:
- Thuyết minh tự động 5 ngôn ngữ (Việt, Anh, Hàn, Trung, Nhật).
- Tự động phát âm thanh khi đến gần địa điểm (GPS).
- Quét mã QR, tìm kiếm và lưu quán yêu thích.

## 📁 Cấu trúc dự án
- **VinhKhanhFood_Mobile**: Mã nguồn App điện thoại (phát triển bằng .NET MAUI).
- **VinhKhanhFood_API**: Mã nguồn Backend xử lý dữ liệu (ASP.NET Core & SQL Server).

## 🚀 Hướng dẫn chạy
1. **Backend**: Mở giải pháp trong thư mục API, cập nhật ConnectionString và nhấn Run để chạy Swagger.
2. **Mobile**: Thay đổi địa chỉ `BaseAddress` trong `ApiService.cs` thành IP máy tính của bạn và chạy trên điện thoại/máy ảo.
