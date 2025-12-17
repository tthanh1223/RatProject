import uvicorn
import socket
from fastapi import FastAPI, Request
from fastapi.responses import HTMLResponse, PlainTextResponse
from fastapi.templating import Jinja2Templates
from fastapi.staticfiles import StaticFiles
import os
app = FastAPI()

base_dir = os.path.dirname(os.path.abspath(__file__))

# Tạo đường dẫn đầy đủ tới static và templates
static_dir = os.path.join(base_dir, "static")
templates_dir = os.path.join(base_dir, "templates")

# Mount static files
if os.path.isdir(static_dir):
    app.mount("/static", StaticFiles(directory=static_dir), name="static")
else:
    print(f"⚠️ Cảnh báo: Không tìm thấy thư mục '{static_dir}'")

# Cấu hình Templates với đường dẫn tuyệt đối
templates = Jinja2Templates(directory=templates_dir)

def get_local_ip():
    """Lấy IP LAN của máy hiện tại"""
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
async def root(request: Request, server: str = None):
    """
    Main route:
    - Nếu có query param ?server=IP → render dashboard
    - Nếu không có → render login page
    """
    if server:
        # Đã có server IP → render dashboard
        return templates.TemplateResponse("index.html", {"request": request})
    else:
        # Chưa có server → render login page
        return templates.TemplateResponse("login.html", {"request": request})

@app.get("/dashboard", response_class=HTMLResponse)
async def dashboard(request: Request):
    """
    Dashboard chính - yêu cầu phải có server IP trong session/cookie
    Nếu không có → redirect về login
    """
    return templates.TemplateResponse("index.html", {"request": request})

if __name__ == "__main__":
    local_ip = get_local_ip()
    
    print("=" * 60)
    print(f"✅ Web App đang chạy local tại: http://localhost:3000")
    print(f"🔗 Từ máy khác, truy cập: http://{local_ip}:3000")
    print("=" * 60)
    
    uvicorn.run(app, host="0.0.0.0", port=3000)