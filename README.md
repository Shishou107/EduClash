<div align="center">

# ⚔️ EduClash (StudyArena)
### Nền Tảng Quản Lý Đề Cương Ôn Tập & Đấu Trường Tri Thức Trực Tuyến

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API%20%7C%20MVC-5C2D91?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet)
[![SignalR](https://img.shields.io/badge/SignalR-Realtime%20PvP-blueviolet?style=for-the-badge&logo=socketdotio&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/signalr)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-Database-CC292B?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

*Học phần: Quản lý dự án CNTT · Đề tài 6 · Nhóm thực hiện: **Qua Môn Two***

</div>

---

## 📖 Giới Thiệu Tổng Quan

**EduClash** là giải pháp số hóa tài liệu học tập kết hợp yếu tố trò chơi hóa (**Gamification**), giải quyết bài toán ôn thi nhàm chán của sinh viên. Hệ thống không chỉ cung cấp không gian lưu trữ và tra cứu đề cương trực quan kiểu *Studocu*, mà còn biến quá trình ôn tập thành các trận so tài **PvP Real-time** kịch tính.

---

## ✨ Tính Năng Nổi Bật

| Tính năng | Mô tả chi tiết |
|---|---|
| 📚 **Quản lý đề cương** | Tạo, chỉnh sửa, phân loại đề cương theo môn học, kỳ thi và giảng viên. |
| 📄 **Smart Document Viewer** | Trình đọc tài liệu phong cách Studocu, hỗ trợ parse nội dung tự động từ Word (`.docx`) và PDF (`PdfPig`). |
| ⚔️ **PvP Arena (Đấu trường 1vs1)** | Ghép trận đấu thời gian thực qua **SignalR**, tranh tài trả lời trắc nghiệm nhanh giành cúp và điểm thưởng. |
| 🎯 **Luyện đề thông minh** | Chế độ làm bài cá nhân, tính điểm tức thì và phân tích câu đúng/sai kèm giải thích chi tiết. |
| 🪙 **Coin & Shop Hệ thống** | Tích lũy Xu khi thắng trận/hoàn thành đề cương, đổi vật phẩm hoặc mở khóa tài liệu VIP. |
| 🏆 **Bảng xếp hạng (Leaderboard)** | Vinh danh top người học xuất sắc theo tuần, tháng và chuỗi thắng liên tục (Streak). |
| 🔐 **Bảo mật & Xác thực** | Đăng ký, đăng nhập bảo mật với JWT, mã hóa mật khẩu và gửi mã xác thực qua Email. |

---

## 🛠️ Công Nghệ Sử Dụng

### 🖥️ Backend (`api_Outline management`)
* **Core:** .NET 10 Web API
* **ORM & Database:** Entity Framework Core, Microsoft SQL Server
* **Realtime Engine:** ASP.NET Core SignalR (Xử lý phòng đấu PvP)
* **Xử lý tài liệu:** `DocumentFormat.OpenXml` (Word), `PdfPig` (PDF)
* **Bảo mật & Tiện ích:** JWT Bearer, FluentValidation, AutoMapper, MailKit

### 🌐 Frontend (`ui_quamonhai`)
* **Framework:** ASP.NET Core MVC (Razor Engine)
* **Giao diện:** HTML5, Modern CSS3 (Design Tokens, Dark/Light accents), JavaScript (Vanilla + Viewer Components)
* **Tích hợp:** SignalR Client, RESTful API Consumer

---

## 📂 Cấu Trúc Dự Án

```plaintext
EduClash/
├── api_Outline management/      # 🚀 Backend Web API (.NET 10)
│   ├── Controllers/             # API Endpoints (Auth, Outlines, Quizzes, Arena)
│   ├── Entities/ & Models/      # Entity Framework Data Models
│   ├── Hubs/                    # SignalR Hubs phục vụ đấu PvP trực tuyến
│   ├── Services/                # Nghiệp vụ xử lý (Tài liệu, Mail, Xếp hạng)
│   └── appsettings.json         # Cấu hình kết nối Database & Secret Key
│
├── ui_quamonhai/                # 🎨 Frontend Web App (ASP.NET Core MVC)
│   ├── Controllers/             # Xử lý luồng View và điều phối dữ liệu
│   ├── Views/                   # Giao diện người dùng Razor (.cshtml)
│   │   ├── Home/                # Trang chủ, Arena, Leaderboard
│   │   └── Outline/             # Trình xem & Import đề cương
│   ├── wwwroot/                 # Tài nguyên tĩnh (CSS, JS, Fonts, Icons)
│   └── Services/                # HttpClient tương tác với Backend API
│
└── .gitignore                   # Loại trừ file rác (bin, obj, .vs)
