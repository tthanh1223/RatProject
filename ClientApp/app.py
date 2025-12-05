import uvicorn
from fastapi import FastAPI, Request
from fastapi.responses import HTMLResponse
from fastapi.templating import Jinja2Templates

app = FastAPI()

# Khai báo thư mục chứa file HTML
templates = Jinja2Templates(directory="templates")

@app.get("/", response_class=HTMLResponse)
async def read_root(request: Request):
    """
    Khi người dùng vào localhost:3000, trả về file index.html
    """
    return templates.TemplateResponse("index.html", {"request": request})

if __name__ == "__main__":
    # Chạy Web Server ở cổng 3000
    print("Web App đang chạy tại: http://localhost:3000")
    uvicorn.run(app, host="127.0.0.1", port=3000)