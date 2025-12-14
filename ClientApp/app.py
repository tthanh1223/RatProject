import uvicorn
from fastapi import FastAPI, Request
from fastapi.responses import HTMLResponse
from fastapi.templating import Jinja2Templates
from fastapi.staticfiles import StaticFiles

app = FastAPI()

# Mount static files để serve CSS và JS
app.mount("/static", StaticFiles(directory="static"), name="static")

# Khai báo thư mục chứa file HTML
templates = Jinja2Templates(directory="templates")

@app.get("/", response_class=HTMLResponse)
async def read_root(request: Request):
    """
    Khi người dùng vào localhost:3000, trả về file index.html
    """
    return templates.TemplateResponse("index.html", {"request": request})

if __name__ == "__main__":
    print("=" * 60)
    print("Web App đang chạy tại: http://0.0.0.0:3000")
    print("Từ máy khác, truy cập: http://<IP_CỦA_MÁY_NÀY>:3000")
    print("=" * 60)
    uvicorn.run(app, host="0.0.0.0", port=3000)