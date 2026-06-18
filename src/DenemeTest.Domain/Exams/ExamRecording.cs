using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams
{
    public class ExamRecording : FullAuditedAggregateRoot<Guid>
    {
        public Guid ExamSessionId { get; protected set; }

        public ExamRecordingKind Kind { get; protected set; }

        public string FileName { get; protected set; } = default!;

        public string StoragePath { get; protected set; } = default!;

        public string MimeType { get; protected set; } = default!;

        public long SizeBytes { get; protected set; }

        public DateTime UploadedAt { get; protected set; }

        public DateTime? ExpiresAt { get; protected set; }

        public bool IsStorageDeleted { get; protected set; }

        public DateTime? StorageDeletedAt { get; protected set; }

        protected ExamRecording()
        {
        }

        public ExamRecording(
            Guid id,
            Guid examSessionId,
            ExamRecordingKind kind,
            string fileName,
            string storagePath,
            string mimeType,
            long sizeBytes,
            DateTime uploadedAt,
            DateTime? expiresAt = null)
            : base(id)
        {
            SetExamSessionId(examSessionId);
            SetKind(kind);
            SetFileName(fileName);
            SetStoragePath(storagePath);
            SetMimeType(mimeType);
            SetSizeBytes(sizeBytes);

            UploadedAt = uploadedAt;
            ExpiresAt = expiresAt;
            IsStorageDeleted = false;
            StorageDeletedAt = null;
        }

        public void UpdateFileInfo(
            string fileName,
            string storagePath,
            string mimeType,
            long sizeBytes,
            DateTime uploadedAt,
            DateTime? expiresAt = null)
        {
            SetFileName(fileName);
            SetStoragePath(storagePath);
            SetMimeType(mimeType);
            SetSizeBytes(sizeBytes);

            UploadedAt = uploadedAt;
            ExpiresAt = expiresAt;
            IsStorageDeleted = false;
            StorageDeletedAt = null;
        }

        public void ExtendRetention(DateTime? expiresAt)
        {
            ExpiresAt = expiresAt;
        }

        public void MarkStorageDeleted(DateTime deletedAt)
        {
            IsStorageDeleted = true;
            StorageDeletedAt = deletedAt;
        }

        private void SetExamSessionId(Guid examSessionId)
        {
            if (examSessionId == Guid.Empty)
            {
                throw new BusinessException("ExamRecording:ExamSessionIdEmpty")
                    .WithData("Message", "Sınav oturumu bilgisi boş olamaz.");
            }

            ExamSessionId = examSessionId;
        }

        private void SetKind(ExamRecordingKind kind)
        {
            if (!Enum.IsDefined(typeof(ExamRecordingKind), kind))
            {
                throw new BusinessException("ExamRecording:InvalidKind")
                    .WithData("Message", "Geçersiz kayıt türü.");
            }

            Kind = kind;
        }

        private void SetFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new BusinessException("ExamRecording:FileNameEmpty")
                    .WithData("Message", "Kayıt dosya adı boş olamaz.");
            }

            FileName = fileName.Trim();
        }

        private void SetStoragePath(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
            {
                throw new BusinessException("ExamRecording:StoragePathEmpty")
                    .WithData("Message", "Kayıt dosya yolu boş olamaz.");
            }

            StoragePath = storagePath.Trim();
        }

        private void SetMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                mimeType = "video/webm";
            }

            MimeType = mimeType.Trim();
        }

        private void SetSizeBytes(long sizeBytes)
        {
            if (sizeBytes < 0)
            {
                throw new BusinessException("ExamRecording:InvalidSize")
                    .WithData("Message", "Kayıt dosya boyutu geçersiz.");
            }

            SizeBytes = sizeBytes;
        }
    }
}