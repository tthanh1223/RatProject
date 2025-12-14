import uvicorn
import socket
from fastapi import FastAPI, Request
from fastapi.responses import HTMLResponse
from fastapi.templating import Jinja2Templates
from fastapi.staticfiles import StaticFiles

app = FastAPI()

# Mount static files để serve CSS và JS
try:
    app.mount("/static", StaticFiles(directory="static"), name="static")
except RuntimeError:
    print("⚠️ Cảnh báo: Chưa tạo thư mục 'static', web có thể lỗi giao diện.")

# Khai báo thư mục chứa file HTML
templates = Jinja2Templates(directory="templates")

def get_local_ip():
    """
    Hàm này tạo một kết nối giả đến Google DNS để xác định 
    IP LAN chính xác mà máy đang sử dụng.
    """
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
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
    Main dashboard - hiển thị index.html
    """
    return templates.TemplateResponse("index.html", {"request": request})

@app.get("/login", response_class=HTMLResponse)
async def login_page(request: Request):
    """
    Login page - hiển thị login.html
    """
    return templates.TemplateResponse("login.html", {"request": request})

@app.get("/api/get-local-ip")
async def get_ip():
    """
    API endpoint để lấy local IP
    """
    return get_local_ip()

if __name__ == "__main__":
    local_ip = get_local_ip()
    
    print("=" * 60)
    print(f"✅ Web App đang chạy local tại: http://localhost:3000")
    print(f"🔗 Login page: http://localhost:3000/login")
    print(f"🔗 Từ máy khác (điện thoại/PC), truy cập: http://{local_ip}:3000/login")
    print("=" * 60)
    
    uvicorn.run(app, host="0.0.0.0", port=3000)