export class WebcamManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.frames = [];
        this.isPlaying = false;
        this.currentFrameIdx = 0;
        this.playInterval = null;
        this.FPS = 30;
        
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
            timeCounter: document.getElementById('time-counter'),
            playBtn: document.getElementById('btn-play-video'),
            pauseBtn: document.getElementById('btn-pause-video'),
            saveBtn: document.getElementById('btn-save-video'),
            startRecBtn: document.getElementById('btn-start-record'),
            stopRecBtn: document.getElementById('btn-stop-record'),
            durationInput: document.getElementById('duration-input'),
            recStatus: document.getElementById('rec-status'),
            recTimer: document.getElementById('rec-timer')
        };
        
        this.setupListeners();
    }

    setupListeners() {
        // Start recording
        this.elements.startRecBtn.addEventListener('click', () => {
            console.log('🔴 [DEBUG] START button clicked');
            this.startRecording();
        });

        // Stop recording
        this.elements.stopRecBtn.addEventListener('click', () => {
            console.log('🔴 [DEBUG] STOP button clicked');
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

        // WebSocket events
        this.ws.on('video_start', (data) => {
            console.log('🔴 [DEBUG] video_start received:', data);
            this.frames = [];
            this.currentFrameIdx = 0;
            this.elements.status.style.display = 'none';
            this.elements.container.classList.add('hidden');
            this.logger.log(`> Receiving video (expecting ${data.count} frames)...`);
        });

        this.ws.on('video_batch', (data) => {
            console.log('🔴 [DEBUG] video_batch received, frames:', data.frames?.length);
            if (data.frames && data.frames.length > 0) {
                data.frames.forEach(frame => {
                    this.frames.push(frame.data);
                });
                
                if (this.isRecording) {
                    const elapsed = Math.floor((Date.now() - this.recordingStartTime) / 1000);
                    this.updateRecTimer(elapsed, this.recordingDuration);
                }
            }
        });

        this.ws.on('video_end', () => {
            console.log('🔴 [DEBUG] video_end received, total frames:', this.frames.length);
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

    startRecording() {
        const duration = parseInt(this.elements.durationInput.value);
        
        console.log('🔴 [DEBUG] startRecording called, duration:', duration);
        
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
        
        // ✅ GỬI COMMAND
        const command = `start_cam ${duration}`;
        console.log('🔴 [DEBUG] Sending command:', command);
        console.log('🔴 [DEBUG] WebSocket connected:', this.ws.isConnected());
        
        const sent = this.ws.send(command);
        console.log('🔴 [DEBUG] Command sent successfully:', sent);
        
        this.logger.log(`> Recording started (${duration}s)`);
    }

    stopRecording() {
        console.log('🔴 [DEBUG] stopRecording called');
        
        if (!this.isRecording) {
            console.log('🔴 [DEBUG] Not recording, ignoring');
            return;
        }
        
        const command = 'stop_cam';
        console.log('🔴 [DEBUG] Sending command:', command);
        
        this.ws.send(command);
        this.logger.log('> Stopping recording...');
    }

    stopRecordingUI() {
        console.log('🔴 [DEBUG] stopRecordingUI called');
        
        this.isRecording = false;
        clearInterval(this.timerInterval);
        
        this.elements.startRecBtn.classList.remove('hidden');
        this.elements.stopRecBtn.classList.add('hidden');
        this.elements.durationInput.disabled = false;
        this.elements.recStatus.classList.add('hidden');
    }

    updateRecTimer(elapsed, total) {
        const formatTime = (sec) => {
            const m = Math.floor(sec / 60);
            const s = sec % 60;
            return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
        };
        
        this.elements.recTimer.textContent = `${formatTime(elapsed)} / ${formatTime(total)}`;
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
        
        const currentTime = index / this.FPS;
        const totalTime = this.frames.length / this.FPS;
        this.elements.timeCounter.textContent = this.formatTime(currentTime) + ' / ' + this.formatTime(totalTime);
        
        this.currentFrameIdx = index;
    }

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