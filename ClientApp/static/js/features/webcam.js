export class WebcamManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.frames = [];
        this.isPlaying = false;
        this.currentFrameIdx = 0;
        this.playInterval = null;
        this.FPS = 30; // Server gửi 30fps
        
        // ✅ Recording state
        this.isRecording = false;
        this.recordingStartTime = null;
        this.recordingDuration = 20;
        this.timerInterval = null;
        
        this.elements = {
            status: document.getElementById('cam-status'),
            loading: document.getElementById('loading-cam'),
            container: document.getElementById('video-player-container'),
            screen: document.getElementById('video-screen'),
            overlay: document.getElementById('replay-overlay'),
            seeker: document.getElementById('video-seeker'),
            timeCounter: document.getElementById('time-counter'), // ✅ THAY ĐỔI
            playBtn: document.getElementById('btn-play-video'),
            pauseBtn: document.getElementById('btn-pause-video'),
            saveBtn: document.getElementById('btn-save-video'),
            
            // Recording controls
            startRecBtn: document.getElementById('btn-start-record'),
            stopRecBtn: document.getElementById('btn-stop-record'),
            durationInput: document.getElementById('duration-input'),
            recStatus: document.getElementById('rec-status'),
            recTimer: document.getElementById('rec-timer')
        };
        
        this.setupListeners();
    }

    setupListeners() {
        // ✅ Start/Stop recording
        this.elements.startRecBtn.addEventListener('click', () => {
            this.startRecording();
        });

        this.elements.stopRecBtn.addEventListener('click', () => {
            this.stopRecording();
        });

        // Save, play, pause
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

        // ✅ WebSocket events
        this.ws.on('video_start', (data) => {
            this.frames = [];
            this.currentFrameIdx = 0;
            this.elements.status.style.display = 'none';
            this.elements.container.classList.add('hidden');
            this.logger.log(`> Receiving video (expecting ${data.count} frames)...`);
        });

        this.ws.on('video_batch', (data) => {
            if (data.frames && data.frames.length > 0) {
                data.frames.forEach(frame => {
                    this.frames.push(frame.data);
                });
                
                // Update timer while receiving
                if (this.isRecording) {
                    const elapsed = Math.floor((Date.now() - this.recordingStartTime) / 1000);
                    this.updateRecTimer(elapsed, this.recordingDuration);
                }
            }
        });

        this.ws.on('video_end', () => {
            this.stopRecordingUI();
            
            if (this.frames.length > 0) {
                this.loadPlayer();
                const duration = (this.frames.length / this.FPS).toFixed(1);
                this.logger.log(`> Video complete: ${this.frames.length} frames (${duration}s)`);
            } else {
                this.logger.log("> ❌ No frames received");
            }
        });
    }

    // ✅ Start recording
    startRecording() {
        const duration = parseInt(this.elements.durationInput.value);
        
        if (duration < 5 || duration > 300) {
            alert('⚠️ Duration phải từ 5-300 giây!');
            return;
        }
        
        this.recordingDuration = duration;
        this.isRecording = true;
        this.recordingStartTime = Date.now();
        
        // Update UI
        this.elements.startRecBtn.classList.add('hidden');
        this.elements.stopRecBtn.classList.remove('hidden');
        this.elements.durationInput.disabled = true;
        this.elements.recStatus.classList.remove('hidden');
        this.elements.status.style.display = 'none';
        this.elements.container.classList.add('hidden');
        this.elements.loading.style.display = 'none';
        
        // Start timer
        this.updateRecTimer(0, duration);
        this.timerInterval = setInterval(() => {
            const elapsed = Math.floor((Date.now() - this.recordingStartTime) / 1000);
            this.updateRecTimer(elapsed, duration);
        }, 100);
        
        // Send command to server
        this.ws.send(`start_cam ${duration}`);
        this.logger.log(`> Recording started (${duration}s)`);
    }

    // ✅ Stop recording
    stopRecording() {
        if (!this.isRecording) return;
        
        this.ws.send('stop_cam');
        this.logger.log('> Stopping recording...');
    }

    // ✅ Stop recording UI (called when video_end received)
    stopRecordingUI() {
        this.isRecording = false;
        clearInterval(this.timerInterval);
        
        // Reset UI
        this.elements.startRecBtn.classList.remove('hidden');
        this.elements.stopRecBtn.classList.add('hidden');
        this.elements.durationInput.disabled = false;
        this.elements.recStatus.classList.add('hidden');
    }

    // ✅ Update recording timer (00:12 / 00:20)
    updateRecTimer(elapsed, total) {
        const formatTime = (sec) => {
            const m = Math.floor(sec / 60);
            const s = sec % 60;
            return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
        };
        
        this.elements.recTimer.textContent = `${formatTime(elapsed)} / ${formatTime(total)}`;
    }

    // ✅ Load player after recording
    loadPlayer() {
        this.currentFrameIdx = 0;
        this.elements.container.classList.remove('hidden');
        this.elements.seeker.max = this.frames.length - 1;
        this.elements.seeker.value = 0;
        this.showFrame(0);
        this.play();
    }

    // ✅ Show frame (với time display)
    showFrame(index) {
        if (!this.frames || this.frames.length === 0) return;
        if (index < 0 || index >= this.frames.length) return;
        
        this.elements.screen.src = "data:image/jpeg;base64," + this.frames[index];
        this.elements.seeker.value = index;
        
        // ✅ THAY ĐỔI: Hiển thị thời gian thay vì frame
        const currentTime = index / this.FPS;
        const totalTime = this.frames.length / this.FPS;
        this.elements.timeCounter.textContent = this.formatTime(currentTime) + ' / ' + this.formatTime(totalTime);
        
        this.currentFrameIdx = index;
    }

    // ✅ Format time helper
    formatTime(seconds) {
        const m = Math.floor(seconds / 60);
        const s = Math.floor(seconds % 60);
        return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
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
            const duration = (this.frames.length / this.FPS).toFixed(0);
            a.download = `WEBCAM_${duration}s_${Date.now()}.webm`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            btn.innerText = oldText;
            btn.disabled = false;
            this.logger.log(`> Video saved: ${duration}s`);
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