# Hướng dẫn chạy RAT trên 2 máy khác nhau

## Cấu hình đã thay đổi

Toàn bộ ứng dụng đã được cập nhật để hỗ trợ chạy trên 2 máy khác nhau:

### 1. **Server C# (Máy target - máy bị điều khiển)**
- **File**: `ServerApp/WebSocketTest/Form1.cs`
- **Thay đổi**: `localhost:8080` → `0.0.0.0:8080`
- **Tác dụng**: Server sẽ lắng nghe trên TẤT CẢ các IP của máy, không chỉ localhost

### 2. **Client Web (Máy điều khiển)**
- **File**: `ClientApp/app.py`
- **Thay đổi**: `127.0.0.1:3000` → `0.0.0.0:3000`
- **Tác dụng**: Flask server sẽ lắng nghe trên tất cả các IP

### 3. **HTML Client (Browser)**
- **File**: `ClientApp/templates/index.html`
- **Thay đổi**: Hardcode `localhost:8080` → Dynamic `?server=<IP>`
- **Tác dụng**: Có thể chỉ định IP server qua URL parameter

---

## Hướng dẫn sử dụng

### **Bước 1: Tìm IP của máy Server (máy target)**

**Trên Windows:**
```bash
ipconfig
```
Tìm dòng `IPv4 Address` (thường là `192.168.x.x` hoặc `10.x.x.x`)

**Ví dụ**: `192.168.1.100`

### **Bước 2: Khởi động Server (trên máy target)**

1. Mở `ServerApp/WebSocketTest/WebSocketTest.sln` bằng Visual Studio
2. Nhấn `F5` hoặc `Ctrl+F5` để chạy
3. Nhấn nút "Start" trong ứng dụng
4. Xem log: sẽ in ra `Để kết nối từ máy khác, dùng: ws://<IP_MÁCHNA_NÀY>:8080/`

### **Bước 3: Khởi động Client Web (trên máy điều khiển)**

**Cách 1: Mặc định (local)**
```bash
cd ClientApp
python app.py
```
Truy cập: `http://localhost:3000`

**Cách 2: Từ máy khác**
```bash
cd ClientApp
python app.py
```
Từ máy khác, truy cập: `http://<IP_MÁY_CLIENT>:3000?server=<IP_MÁY_SERVER>`

**Ví dụ:**
```
http://192.168.1.50:3000?server=192.168.1.100
```

---

## Ví dụ cụ thể

**Máy A (Server)**: `192.168.1.100`
- Chạy: `dotnet run` từ `ServerApp/WebSocketTest/`
- Port: `8080`

**Máy B (Client)**: `192.168.1.50`
- Chạy: `python app.py` từ `ClientApp/`
- Port: `3000`
- Truy cập từ browser: `http://192.168.1.50:3000?server=192.168.1.100`

---

## Xử sự cố

### ❌ Không thể kết nối Server

**Nguyên nhân**: Tường lửa (Firewall) chặn
**Giải pháp**: 
1. Tắt Firewall Windows (tạm thời)
   - Settings → Privacy & Security → Firewall → Advanced Settings
   - Hoặc chạy lệnh: `netsh advfirewall set allprofiles state off`

2. Hoặc cho phép cổng 8080 và 3000:
   - Settings → Privacy & Security → Firewall → Allow an app
   - Thêm Python.exe và dotnet.exe

### ❌ Kết nối bị mất (disconnect)

**Nguyên nhân**: Tường lửa hết timeout hoặc network bị gián đoạn
**Giải pháp**: Cập nhật code với heartbeat/ping-pong mechanism

### ❌ Sau khi kết nối vẫn không thấy dữ liệu

**Kiểm tra**:
1. Mở F12 (DevTools) → Console
2. Xem có error không
3. Kiểm tra Network tab → WS tab xem WebSocket status

---

## Lưu ý bảo mật

⚠️ **Cảnh báo**: Ứng dụng này không mã hóa / xác thực
- **Không dùng trên Internet công cộng**
- Chỉ dùng trên mạng LAN nội bộ đáng tin cậy
- Nếu cần sản xuất:
  - Thêm TLS/SSL encryption
  - Thêm authentication (username/password)
  - Thêm logging/audit trail
  - Chỉ allow từ IP whitelist

---

## Tổng hợp cấu hình

| Component | Cũ | Mới | Mục đích |
|-----------|-----|-----|---------|
| WebSocket Server | `localhost:8080` | `0.0.0.0:8080` | Lắng nghe từ tất cả IP |
| Flask Server | `127.0.0.1:3000` | `0.0.0.0:3000` | Lắng nghe từ tất cả IP |
| HTML WS URL | `ws://localhost:8080/` | `ws://<SERVER_IP>:8080/` | Dynamic dựa trên URL param |

---

## Kiểm tra kết nối

**Từ máy Client**, chạy lệnh để test:

```bash
# Test kết nối TCP tới Server
ping 192.168.1.100
telnet 192.168.1.100 8080

# Hoặc dùng PowerShell:
Test-NetConnection -ComputerName 192.168.1.100 -Port 8080
```

Nếu kết nối OK, bạn sẽ thấy `Connected` hoặc không có error.
