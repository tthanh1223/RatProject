export class FileManagerManager {
    constructor(ws, logger) {
        this.ws = ws;
        this.logger = logger;
        this.currentPath = 'C:\\';
        this.downloadingFiles = new Map(); // track ongoing downloads
        this.fileChunks = new Map(); // store chunks by path
        
        this.setupEventListeners();
    }

    setupEventListeners() {
        const btnOpenPath = document.getElementById('btn-open-path');
        const btnRefresh = document.getElementById('btn-refresh-files');
        const pathInput = document.getElementById('pathInput');

        if (btnOpenPath) {
            btnOpenPath.addEventListener('click', () => this.openPath());
        }

        if (btnRefresh) {
            btnRefresh.addEventListener('click', () => this.refreshCurrentPath());
        }

        if (pathInput) {
            pathInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    this.openPath();
                }
            });
        }

        // Listen for file list messages from server
        this.ws.on('file_list', (data) => {
            this.onFileListReceived(data);
        });

        // Listen for file download chunks
        this.ws.on('file_start', (data) => {
            this.onFileDownloadStart(data);
        });

        this.ws.on('file_chunk', (data) => {
            this.onFileChunkReceived(data);
        });

        this.ws.on('file_end', (data) => {
            this.onFileDownloadEnd(data);
        });
    }

    openPath() {
        const pathInput = document.getElementById('pathInput');
        const path = pathInput.value.trim();

        if (!path) {
            this.logger.log('[FILES] Path is empty');
            return;
        }

        this.currentPath = path;
        this.listDirectory(path);
    }

    refreshCurrentPath() {
        this.listDirectory(this.currentPath);
    }

    listDirectory(path) {
        this.logger.log(`[FILES] Listing: ${path}`);
        this.ws.send(`list_dir ${path}`);
    }

    onFileListReceived(data) {
        const { path, items } = data;
        this.currentPath = path;
        this.logger.log(`[FILES] Received ${items.length} items from ${path}`);

        // Update display path
        const pathDisplay = document.getElementById('current-path-display');
        if (pathDisplay) {
            pathDisplay.textContent = path;
        }

        // Render file list
        this.renderFileList(items, path);
    }

    renderFileList(items, currentPath) {
        const container = document.getElementById('file-list-container');
        if (!container) return;

        container.innerHTML = '';

        // Add back button if not root
        if (currentPath !== 'C:\\' && currentPath !== 'C:' && !currentPath.match(/^[A-Z]:\\$/i)) {
            const backDiv = document.createElement('div');
            backDiv.className = 'file-item file-folder';
            backDiv.innerHTML = '📁 <span class="file-name">..</span>';
            backDiv.addEventListener('click', () => this.goBack(currentPath));
            container.appendChild(backDiv);
        }

        // Add folders first, then files
        const folders = items.filter(item => item.isDirectory).sort((a, b) => a.name.localeCompare(b.name));
        const files = items.filter(item => !item.isDirectory).sort((a, b) => a.name.localeCompare(b.name));

        [...folders, ...files].forEach(item => {
            const itemDiv = document.createElement('div');
            const isDir = item.isDirectory;
            itemDiv.className = `file-item ${isDir ? 'file-folder' : 'file-file'}`;

            const icon = isDir ? '📁' : '📄';
            const sizeStr = isDir ? '' : ` (${this.formatSize(item.size)})`;
            const name = item.name;

            itemDiv.innerHTML = `<span class="file-name">${icon} ${name}</span><span class="file-size">${sizeStr}</span>`;
            
            if (isDir) {
                itemDiv.addEventListener('click', () => this.listDirectory(item.fullPath));
            } else {
                itemDiv.addEventListener('click', () => this.downloadFile(item.fullPath, item.name));
            }

            container.appendChild(itemDiv);
        });
    }

    goBack(currentPath) {
        // Navigate to parent directory
        const parts = currentPath.split('\\').filter(p => p);
        if (parts.length > 1) {
            parts.pop();
            const parentPath = parts.join('\\') + '\\';
            this.listDirectory(parentPath);
        }
    }

    downloadFile(filePath, fileName) {
        // Normalize path trước khi lưu
        const normalizedPath = this.normalizePath(filePath);
        
        this.logger.log(`[FILES] Downloading: ${fileName} from ${normalizedPath}`);
        this.downloadingFiles.set(normalizedPath, {
            fileName: fileName,
            chunks: [],
            size: 0,
            originalPath: filePath  // Lưu đường dẫn gốc để debug
        });
        this.ws.send(`download_file ${filePath}`);
    }

    normalizePath(path) {
        // Chuẩn hóa: chuyển / → \, trim trailing slash
        if (!path) return '';
        return path
            .replace(/\//g, '\\')
            .replace(/\\+/g, '\\')
            .toUpperCase();  // Windows path case-insensitive
    }

    onFileDownloadStart(data) {
        const { path, size, contentType } = data;
        const normalizedPath = this.normalizePath(path);
        
        console.log('[DEBUG] file_start received:', { path, size, contentType });
        console.log('[DEBUG] Normalized path:', normalizedPath);
        this.logger.log(`[FILES] Download start: ${path} (${this.formatSize(size)})`);
        
        if (this.downloadingFiles.has(normalizedPath)) {
            console.log('[DEBUG] File found in map, updating...');
            this.downloadingFiles.get(normalizedPath).size = size;
            this.downloadingFiles.get(normalizedPath).contentType = contentType;
        } else {
            console.warn('[DEBUG] File NOT found in map:', normalizedPath);
            console.log('[DEBUG] Available keys:', Array.from(this.downloadingFiles.keys()));
        }
    }

    onFileChunkReceived(data) {
        const { path, index, data: b64Data } = data;
        const normalizedPath = this.normalizePath(path);
        
        // ✅ LOG SAMPLE
        if (index % 50 === 0) {
            console.log(`[DEBUG] Chunk ${index} received:`, {
                path: path,
                normalized: normalizedPath,
                dataLength: b64Data.length,
                dataSample: b64Data.substring(0, 20) + '...'
            });
        }
        
        if (!this.downloadingFiles.has(normalizedPath)) {
            console.error('[DEBUG] ❌ Path mismatch!');
            console.error('[DEBUG] Looking for:', normalizedPath);
            console.error('[DEBUG] Available keys:', Array.from(this.downloadingFiles.keys()));
            return;
        }
        
        const fileInfo = this.downloadingFiles.get(normalizedPath);
        
        // ✅ VALIDATE BASE64 CHUNK
        if (!/^[A-Za-z0-9+/=]*$/.test(b64Data)) {
            console.error(`[DEBUG] ❌ Invalid Base64 at chunk ${index}!`);
            console.error('[DEBUG] Invalid chars:', b64Data.match(/[^A-Za-z0-9+/=]/g));
            return;
        }
        
        fileInfo.chunks[index] = b64Data;
    }

    onFileDownloadEnd(data) {
        const { path } = data;
        const normalizedPath = this.normalizePath(path);
        
        if (!this.downloadingFiles.has(normalizedPath)) {
            this.logger.log(`[FILES] ❌ ERROR: File not found in map!`);
            console.error('[DEBUG] Available keys:', Array.from(this.downloadingFiles.keys()));
            return;
        }
        
        const fileInfo = this.downloadingFiles.get(normalizedPath);
        
        // ✅ KIỂM TRA CHUNKS
        const chunks = [];
        const missingIndices = [];
        
        for (let i = 0; i < fileInfo.chunks.length; i++) {
            if (fileInfo.chunks[i]) {
                chunks.push(fileInfo.chunks[i]);
            } else {
                missingIndices.push(i);
            }
        }
        
        // ✅ LOG CHI TIẾT
        console.log('[DEBUG] Total chunks expected:', fileInfo.chunks.length);
        console.log('[DEBUG] Chunks received:', chunks.length);
        console.log('[DEBUG] Missing indices:', missingIndices);
        
        if (missingIndices.length > 0) {
            this.logger.log(`[FILES] ❌ ERROR: Missing ${missingIndices.length} chunks: [${missingIndices.slice(0, 10).join(', ')}...]`);
            return;
        }
        
        if (chunks.length === 0) {
            this.logger.log(`[FILES] ❌ ERROR: No chunks received!`);
            return;
        }
        
        // ✅ VALIDATE BASE64 TRƯỚC KHI DECODE
        const concatenated = chunks.join('');
        if (!this.isValidBase64(concatenated)) {
            this.logger.log(`[FILES] ❌ ERROR: Invalid Base64 encoding!`);
            console.error('[DEBUG] First 100 chars:', concatenated.substring(0, 100));
            console.error('[DEBUG] Last 100 chars:', concatenated.substring(concatenated.length - 100));
            return;
        }
        
        this.downloadFileFromChunks(fileInfo.fileName, chunks);
        this.downloadingFiles.delete(normalizedPath);
    }

    // ✅ THÊM HÀM VALIDATE BASE64
    isValidBase64(str) {
        if (!str || str.length === 0) return false;
        
        // Base64 regex: chỉ chứa A-Z, a-z, 0-9, +, /, =
        const base64Regex = /^[A-Za-z0-9+/]*={0,2}$/;
        return base64Regex.test(str);
    }

    downloadFileFromChunks(fileName, chunks) {
        try {
            const concatenated = chunks.join('');
            
            // ✅ VALIDATE LENGTH
            if (concatenated.length === 0) {
                throw new Error('Empty Base64 string');
            }
            
            console.log('[DEBUG] Total Base64 length:', concatenated.length);
            console.log('[DEBUG] Expected file size:', Math.floor(concatenated.length * 3 / 4), 'bytes');
            
            // ✅ DECODE TỪNG PHẦN NẾU QUÁ LỚN (tránh memory overflow)
            const maxChunkSize = 1024 * 1024; // 1MB per decode
            const byteArrays = [];
            
            if (concatenated.length > maxChunkSize) {
                console.log('[DEBUG] Large file detected, decoding in chunks...');
                
                for (let i = 0; i < concatenated.length; i += maxChunkSize) {
                    const chunk = concatenated.substring(i, i + maxChunkSize);
                    
                    // Padding nếu cần
                    let paddedChunk = chunk;
                    while (paddedChunk.length % 4 !== 0) {
                        paddedChunk += '=';
                    }
                    
                    try {
                        const binaryString = atob(paddedChunk);
                        const bytes = new Uint8Array(binaryString.length);
                        for (let j = 0; j < binaryString.length; j++) {
                            bytes[j] = binaryString.charCodeAt(j);
                        }
                        byteArrays.push(bytes);
                    } catch (err) {
                        console.error(`[DEBUG] Decode error at chunk ${i}:`, err);
                        throw new Error(`Decode failed at position ${i}: ${err.message}`);
                    }
                }
                
                // Merge all byte arrays
                const totalLength = byteArrays.reduce((sum, arr) => sum + arr.length, 0);
                const mergedBytes = new Uint8Array(totalLength);
                let offset = 0;
                for (const arr of byteArrays) {
                    mergedBytes.set(arr, offset);
                    offset += arr.length;
                }
                
                this.triggerDownload(fileName, mergedBytes);
                
            } else {
                // Small file: decode toàn bộ
                const binaryString = atob(concatenated);
                const bytes = new Uint8Array(binaryString.length);
                for (let i = 0; i < binaryString.length; i++) {
                    bytes[i] = binaryString.charCodeAt(i);
                }
                
                this.triggerDownload(fileName, bytes);
            }
            
            this.logger.log(`[FILES] ✅ File downloaded: ${fileName}`);
            
        } catch (error) {
            this.logger.log(`[FILES] ❌ Download error: ${error.message}`);
            console.error('[DEBUG] Full error:', error);
            console.error('[DEBUG] Chunks count:', chunks.length);
            console.error('[DEBUG] First chunk sample:', chunks[0]?.substring(0, 50));
        }
    }

    // ✅ HELPER: TRIGGER DOWNLOAD
    triggerDownload(fileName, bytes) {
        const blob = new Blob([bytes], { type: 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    formatSize(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
    }

    refresh() {
        this.refreshCurrentPath();
    }
}
