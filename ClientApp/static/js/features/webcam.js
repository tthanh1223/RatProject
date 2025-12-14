export class WebcamManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.frames = [];
        this.isPlaying = false;
        this.currentFrameIdx = 0;
        this.playInterval = null;
        this.FPS = 30;
        
        this.elements = {
            status: document.getElementById('cam-status'),
            loading: document.getElementById('loading-cam'),
            container: document.getElementById('video-player-container'),
            screen: document.getElementById('video-screen'),
            overlay: document.getElementById('replay-overlay'),
            seeker: document.getElementById('video-seeker'),
            counter: document.getElementById('frame-counter'),
            playBtn: document.getElementById('btn-play-video'),
            pauseBtn: document.getElementById('btn-pause-video'),
            saveBtn: document.getElementById('btn-save-video')
        };
        
        this.setupListeners();
    }

    setupListeners() {
        document.getElementById('btn-record-webcam').addEventListener('click', () => {
            this.record();
        });

        this.elements.saveBtn.addEventListener('click', () => {
            this.exportAndDownload();
        });

        this.elements.playBtn.addEventListener('click', () => {
            this.play();
        });

        this.elements.pauseBtn.addEventListener('click', () => {
            this.pause();
        });

        this.elements.overlay.addEventListener('click', () => {
            this.play();
        });

        this.elements.seeker.addEventListener('input', () => {
            this.pause();
            this.showFrame(parseInt(this.elements.seeker.value));
        });

        // WebSocket events
        this.ws.on('video_start', (data) => {
            this.frames = [];
            this.currentFrameIdx = 0;
            this.elements.status.style.display = 'none';
            this.elements.container.classList.add('hidden');
            this.elements.loading.style.display = 'block';
            this.logger.log(`> Video start (${data.count} frames)...`);
        });

        this.ws.on('video_batch', (data) => {
            if (data.frames && data.frames.length > 0) {
                data.frames.forEach(frame => {
                    this.frames.push(frame.data);
                });
                this.logger.log(`> Batch received: ${this.frames.length} frames...`);
            }
        });

        this.ws.on('video_end', () => {
            this.elements.loading.style.display = 'none';
            this.elements.status.style.display = 'none';
            
            if (this.frames.length > 0) {
                this.loadPlayer();
                this.logger.log(`> Video complete (${this.frames.length} frames)`);
            } else {
                this.logger.log("> ❌ No frames received");
            }
        });
    }

    record() {
        this.elements.loading.style.display = 'block';
        this.elements.status.style.display = 'none';
        this.ws.send('get_cam');
    }

    loadPlayer() {
        this.currentFrameIdx = 0;
        this.elements.container.classList.remove('hidden');
        this.elements.seeker.max = this.frames.length - 1;
        this.elements.seeker.value = 0;
        this.showFrame(0);
        this.play();
    }

    showFrame(index) {
        if (!this.frames || this.frames.length === 0) return;
        if (index < 0 || index >= this.frames.length) return;
        
        this.elements.screen.src = "data:image/jpeg;base64," + this.frames[index];
        this.elements.seeker.value = index;
        this.elements.counter.innerText = `${String(index + 1).padStart(3, '0')}/${String(this.frames.length).padStart(3, '0')}`;
        this.currentFrameIdx = index;
    }

    play() {
        if (this.isPlaying) return;
        
        this.isPlaying = true;
        this.elements.overlay.style.display = 'none';
        
        if (this.currentFrameIdx >= this.frames.length - 1) {
            this.currentFrameIdx = 0;
        }

        this.playInterval = setInterval(() => {
            this.currentFrameIdx++;
            if (this.currentFrameIdx >= this.frames.length) {
                this.pause();
                this.elements.overlay.style.display = 'flex';
            } else {
                this.showFrame(this.currentFrameIdx);
            }
        }, 1000 / this.FPS);
    }

    pause() {
        this.isPlaying = false;
        clearInterval(this.playInterval);
    }

    async exportAndDownload() {
        if (!this.frames.length) {
            alert("No video data!");
            return;
        }

        const btn = this.elements.saveBtn;
        const oldText = btn.innerText;
        btn.innerText = "⏳ PROCESSING...";
        btn.disabled = true;

        const canvas = document.createElement("canvas");
        const ctx = canvas.getContext("2d");
        const tempImg = new Image();
        tempImg.src = "data:image/jpeg;base64," + this.frames[0];
        
        await new Promise(r => tempImg.onload = r);
        
        canvas.width = tempImg.width;
        canvas.height = tempImg.height;

        const stream = canvas.captureStream(this.FPS);
        const recorder = new MediaRecorder(stream, { mimeType: 'video/webm' });
        const chunks = [];
        
        recorder.ondataavailable = e => chunks.push(e.data);
        recorder.onstop = () => {
            const blob = new Blob(chunks, { type: 'video/webm' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `WEBCAM_${Date.now()}.webm`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            btn.innerText = oldText;
            btn.disabled = false;
        };
        
        recorder.start();
        
        for (let i = 0; i < this.frames.length; i++) {
            await new Promise(resolve => {
                const img = new Image();
                img.onload = () => {
                    ctx.drawImage(img, 0, 0);
                    resolve();
                };
                img.src = "data:image/jpeg;base64," + this.frames[i];
            });
            await new Promise(r => setTimeout(r, 1000 / this.FPS));
        }
        
        recorder.stop();
    }
}