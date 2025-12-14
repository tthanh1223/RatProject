import uvicorn
import socket
from fastapi import FastAPI, Request
from fastapi.responses import HTMLResponse
from fastapi.templating import Jinja2Templates
from fastapi.staticfiles import StaticFiles

app = FastAPI()

# Mount static files để serve CSS và JS
# Lưu ý: Bạn cần tạo thư mục tên "static" cùng cấp với file này nếu chưa có
try:
    app.mount("/static", StaticFiles(directory="static"), name="static")
except RuntimeError:
    print("⚠️ Cảnh báo: Chưa tạo thư mục 'static', web có thể lỗi giao diện.")

# Khai báo thư mục chứa file HTML
# Lưu ý: Bạn cần tạo thư mục tên "templates" cùng cấp với file này
templates = Jinja2Templates(directory="templates")

def get_local_ip():
    """
    Hàm này tạo một kết nối giả đến Google DNS để xác định 
    IP LAN chính xác mà máy đang sử dụng.
    """
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        # Không cần kết nối thực sự, chỉ cần hệ điều hành định tuyến
        s.connect(('8.8.8.8', 80))
        ip = s.getsockname()[0]
    except Exception:
        ip = '127.0.0.1'
    finally:
        s.close()
    return ip

@app.get("/", response_class=HTMLResponse)
async def read_root(request: Request):
    """
    Khi người dùng vào localhost:3000, trả về file index.html
    """
    return templates.TemplateResponse("index.html", {"request": request})

if __name__ == "__main__":
    # Lấy IP tự động
    local_ip = get_local_ip()
    
    print("=" * 60)
    print(f"✅ Web App đang chạy local tại: http://localhost:3000")
    print(f"🔗 Từ máy khác (điện thoại/PC), truy cập: http://{local_ip}:3000")
    print("=" * 60)
    
    # host="0.0.0.0" là bắt buộc để cho phép truy cập từ bên ngoài
    uvicorn.run(app, host="0.0.0.0", port=3000)