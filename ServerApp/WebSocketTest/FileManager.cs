using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketTest
{
    public class FileEntry
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class FileMetadata
    {
        public long Size { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
        public DateTime LastModified { get; set; }
        public string? HashSha256 { get; set; }
    }

    public class ListOptions
    {
        public string? SearchPattern { get; set; }
        public bool Recursive { get; set; } = false;
    }

    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
        public int Offset { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
    }

    public class FileManager
    {
        private readonly string _root; // allowed root directory (canonical)
        private readonly long _maxStreamFileSizeBytes;

        public FileManager(string rootDirectory, long maxStreamFileSizeBytes = 1024L * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("rootDirectory");
            _root = Path.GetFullPath(rootDirectory);
            if (!Directory.Exists(_root)) throw new DirectoryNotFoundException(_root);
            _maxStreamFileSizeBytes = maxStreamFileSizeBytes;
        }

        // Private helper: resolve path and ensure it's under allowed root
        private string ResolveAndValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(nameof(path));

            string combined = Path.IsPathRooted(path) ? path : Path.Combine(_root, path);
            string full = Path.GetFullPath(combined);

            // Ensure the resolved path is under _root
            if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Access to path is not allowed");

            return full;
        }

        public bool FileExists(string path)
        {
            string full = ResolveAndValidatePath(path);
            return File.Exists(full);
        }

        public async Task<IEnumerable<FileEntry>> ListDirectoryAsync(string path, ListOptions? options = null)
        {
            string full = ResolveAndValidatePath(path);
            if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);

            options ??= new ListOptions();

            var entries = new List<FileEntry>();

            var searchPattern = string.IsNullOrEmpty(options.SearchPattern) ? "*" : options.SearchPattern;

            if (options.Recursive)
            {
                foreach (var dir in Directory.EnumerateDirectories(full, searchPattern, SearchOption.AllDirectories))
                {
                    var di = new DirectoryInfo(dir);
                    entries.Add(new FileEntry
                    {
                        Name = di.Name,
                        FullPath = di.FullName,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = di.LastWriteTimeUtc
                    });
                }

                foreach (var file in Directory.EnumerateFiles(full, searchPattern, SearchOption.AllDirectories))
                {
                    var fi = new FileInfo(file);
                    entries.Add(new FileEntry
                    {
                        Name = fi.Name,
                        FullPath = fi.FullName,
                        IsDirectory = false,
                        Size = fi.Length,
                        LastModified = fi.LastWriteTimeUtc
                    });
                }
            }
            else
            {
                foreach (var dir in Directory.EnumerateDirectories(full, searchPattern, SearchOption.TopDirectoryOnly))
                {
                    var di = new DirectoryInfo(dir);
                    entries.Add(new FileEntry
                    {
                        Name = di.Name,
                        FullPath = di.FullName,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = di.LastWriteTimeUtc
                    });
                }

                foreach (var file in Directory.EnumerateFiles(full, searchPattern, SearchOption.TopDirectoryOnly))
                {
                    var fi = new FileInfo(file);
                    entries.Add(new FileEntry
                    {
                        Name = fi.Name,
                        FullPath = fi.FullName,
                        IsDirectory = false,
                        Size = fi.Length,
                        LastModified = fi.LastWriteTimeUtc
                    });
                }
            }

            // return ordered: directories first then files
            var ordered = entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name).ToList();
            await Task.CompletedTask;
            return ordered;
        }

        public PagedResult<FileEntry> EnumerateDirectoryPaged(string path, int offset = 0, int limit = 100, string? filter = null)
        {
            string full = ResolveAndValidatePath(path);
            if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);

            var pattern = string.IsNullOrEmpty(filter) ? "*" : filter;

            var dirs = Directory.EnumerateDirectories(full, pattern, SearchOption.TopDirectoryOnly)
                .Select(d => new DirectoryInfo(d))
                .Select(di => new FileEntry
                {
                    Name = di.Name,
                    FullPath = di.FullName,
                    IsDirectory = true,
                    Size = 0,
                    LastModified = di.LastWriteTimeUtc
                });

            var files = Directory.EnumerateFiles(full, pattern, SearchOption.TopDirectoryOnly)
                .Select(f => new FileInfo(f))
                .Select(fi => new FileEntry
                {
                    Name = fi.Name,
                    FullPath = fi.FullName,
                    IsDirectory = false,
                    Size = fi.Length,
                    LastModified = fi.LastWriteTimeUtc
                });

            var all = dirs.Concat(files).OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name).ToList();

            var total = all.Count;
            var items = all.Skip(offset).Take(limit).ToList();

            return new PagedResult<FileEntry>
            {
                Items = items,
                Offset = offset,
                Limit = limit,
                Total = total
            };
        }

        public async Task<FileStream> GetFileStreamAsync(string path)
        {
            string full = ResolveAndValidatePath(path);
            if (!File.Exists(full)) throw new FileNotFoundException(full);

            var fi = new FileInfo(full);
            if (fi.Length > _maxStreamFileSizeBytes)
                throw new InvalidOperationException("File too large to stream");

            // Open read-only stream; caller is responsible to dispose
            var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            await Task.CompletedTask;
            return fs;
        }

        public async Task<byte[]?> GetFileBytesAsync(string path, long? maxBytes = null)
        {
            string full = ResolveAndValidatePath(path);
            if (!File.Exists(full)) return null;

            var fi = new FileInfo(full);
            if (maxBytes.HasValue && fi.Length > maxBytes.Value) return null;

            return await File.ReadAllBytesAsync(full);
        }

        public async Task<FileMetadata> GetFileMetadataAsync(string path, bool includeHash = false)
        {
            string full = ResolveAndValidatePath(path);
            if (!File.Exists(full)) throw new FileNotFoundException(full);

            var fi = new FileInfo(full);
            var meta = new FileMetadata
            {
                Size = fi.Length,
                ContentType = GetMimeType(fi.Extension),
                LastModified = fi.LastWriteTimeUtc
            };

            if (includeHash)
            {
                meta.HashSha256 = await CalculateHashAsync(full);
            }

            return meta;
        }

        public async Task<string> CalculateHashAsync(string path)
        {
            string full = ResolveAndValidatePath(path);
            if (!File.Exists(full)) throw new FileNotFoundException(full);

            using var sha = SHA256.Create();
            await using var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = await sha.ComputeHashAsync(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private string GetMimeType(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return "application/octet-stream";
            extension = extension.TrimStart('.').ToLowerInvariant();

            return extension switch
            {
                "txt" => "text/plain",
                "log" => "text/plain",
                "json" => "application/json",
                "html" => "text/html",
                "htm" => "text/html",
                "jpg" => "image/jpeg",
                "jpeg" => "image/jpeg",
                "png" => "image/png",
                "gif" => "image/gif",
                "bmp" => "image/bmp",
                "pdf" => "application/pdf",
                "zip" => "application/zip",
                "csv" => "text/csv",
                "xml" => "application/xml",
                "mp4" => "video/mp4",
                "mp3" => "audio/mpeg",
                _ => "application/octet-stream"
            };
        }
    }
}
