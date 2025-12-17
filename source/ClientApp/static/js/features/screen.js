export class ScreenCaptureManager {
    constructor(ws, logger, modal) {
        this.ws = ws;
        this.logger = logger;
        this.modal = modal;
        this.img = document.getElementById('screen-img');
        this.loadingIndicator = document.getElementById('loading-screen');
        
        this.setupListeners();
    }

    setupListeners() {
        document.getElementById('btn-capture-screen').addEventListener('click', () => {
            this.capture();
        });

        document.getElementById('btn-save-screenshot').addEventListener('click', () => {
            this.saveScreenshot();
        });

        this.img.addEventListener('click', () => {
            this.modal.open(this.img.src);
        });

        this.ws.on('screen_capture', (data) => {
            this.loadingIndicator.style.display = 'none';
            this.img.src = "data:image/jpeg;base64," + data.data;
            this.logger.log("> Screen capture received.");
        });
    }

    capture() {
        this.loadingIndicator.style.display = 'block';
        this.ws.send('get_screen');
    }

    saveScreenshot() {
        if (!this.img.src || !this.img.src.startsWith("data:image")) {
            alert("No image captured!");
            return;
        }

        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        const tempImg = new Image();
        
        tempImg.onload = () => {
            canvas.width = tempImg.width;
            canvas.height = tempImg.height;
            ctx.drawImage(tempImg, 0, 0);
            
            canvas.toBlob((blob) => {
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'SCREEN_' + new Date().getTime() + '.png';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
                this.logger.log("✓ Saved PNG.");
            }, 'image/png');
        };
        
        tempImg.src = this.img.src;
    }
}