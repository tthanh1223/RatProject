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
        
        console.log('[DEBUG] Normalized path:', path, '->', normalizedPath);
        
        if (!this.downloadingFiles.has(normalizedPath)) {
            console.warn('[DEBUG] Path not found. Looking for:', normalizedPath);
            console.log('[DEBUG] Available paths:', Array.from(this.downloadingFiles.keys()));
            return;
        }
        
        const fileInfo = this.downloadingFiles.get(normalizedPath);
        fileInfo.chunks[index] = b64Data;
    }

    onFileDownloadEnd(data) {
        const { path } = data;
        const normalizedPath = this.normalizePath(path);
        
        console.log('[DEBUG] file_end received for:', path);
        console.log('[DEBUG] Normalized path:', normalizedPath);
        console.log('[DEBUG] Available keys in map:', Array.from(this.downloadingFiles.keys()));
        
        if (!this.downloadingFiles.has(normalizedPath)) {
            this.logger.log(`[FILES] Download end but file not found: ${path}`);
            console.warn('[DEBUG] Path mismatch! Received:', normalizedPath, 'Available:', Array.from(this.downloadingFiles.keys()));
            return;
        }
        
        const fileInfo = this.downloadingFiles.get(normalizedPath);
        console.log('[DEBUG] File info found:', { fileName: fileInfo.fileName, chunksLength: fileInfo.chunks.length });
        
        // Xây dựng mảng chunks đúng (loại bỏ undefined)
        const chunks = [];
        for (let i = 0; i < fileInfo.chunks.length; i++) {
            if (fileInfo.chunks[i]) {
                chunks.push(fileInfo.chunks[i]);
            } else {
                console.warn(`[DEBUG] Missing chunk at index ${i}`);
            }
        }
        
        console.log('[DEBUG] Final chunks count:', chunks.length, 'raw array length:', fileInfo.chunks.length);
        this.logger.log(`[FILES] Download complete: ${fileInfo.fileName} (${chunks.length} chunks, raw array: ${fileInfo.chunks.length})`);
        
        if (chunks.length === 0) {
            this.logger.log(`[FILES] ERROR: No chunks received! Check server logs.`);
            return;
        }
        
        this.downloadFileFromChunks(fileInfo.fileName, chunks);
        this.downloadingFiles.delete(normalizedPath);
    }

    downloadFileFromChunks(fileName, chunks) {
        try {
            // Concatenate all base64 chunks
            const concatenated = chunks.join('');
            
            // Convert base64 to binary
            const binaryString = atob(concatenated);
            const bytes = new Uint8Array(binaryString.length);
            for (let i = 0; i < binaryString.length; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }

            // Create blob and download
            const blob = new Blob([bytes], { type: 'application/octet-stream' });
            const url = URL.createObjectURL(blob);
            
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            this.logger.log(`[FILES] File downloaded: ${fileName}`);
        } catch (error) {
            this.logger.log(`[FILES] Download error: ${error.message}`);
        }
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
