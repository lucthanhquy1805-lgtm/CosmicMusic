using Amazon.S3;
using Amazon.S3.Transfer;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CosmicMusic.Services
{
    public class S3Service
    {
        // 👇 1. BẠN HÃY ĐIỀN LẠI MÃ AWS MỚI CỦA BẠN VÀO ĐÂY NHÉ 👇
        // (LƯU Ý: Nhớ xóa trắng 2 dòng Key này trước khi push lên GitHub lần sau nhé!)
        private readonly string _bucketName = "cosmic-music-store-admin";
        private readonly string _accessKey = "MÃ_ACCESS_KEY_CỦA_BẠN";
        private readonly string _secretKey = "MÃ_SECRET_KEY_CỦA_BẠN";

        // Vùng máy chủ Singapore của bạn
        private readonly Amazon.RegionEndpoint _region = Amazon.RegionEndpoint.APSoutheast1;

        public async Task<string> UploadMp3Async(Stream fileStream, string fileName)
        {
            if (fileStream == null) return null;

            try
            {
                // BỘ LỌC HỦY DIỆT: Xóa sổ 100% ký tự tiếng Việt, dấu cách ẩn, khoảng trắng do Copy/Paste sai
                string safeAccess = Regex.Replace(_accessKey, @"[^\x20-\x7E]", "").Trim();
                string safeSecret = Regex.Replace(_secretKey, @"[^\x20-\x7E]", "").Trim();
                string safeBucket = Regex.Replace(_bucketName, @"[^a-z0-9\-]", "").Trim();

                // Lọc tên file để đảm bảo URL tải lên luôn hợp lệ
                string safeFileName = Regex.Replace(fileName, @"[^a-zA-Z0-9_\-\.]", "");
                string uniqueFileName = $"songs/{Guid.NewGuid()}_{safeFileName}";

                // Tiến hành Upload
                var credentials = new Amazon.Runtime.BasicAWSCredentials(safeAccess, safeSecret);
                using var client = new AmazonS3Client(credentials, _region);
                using var transferUtility = new TransferUtility(client);

                // Đã gỡ bỏ CannedACL vì Bucket của bạn đã tự động Public bằng Policy
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = fileStream,
                    Key = uniqueFileName,
                    BucketName = safeBucket
                };

                await transferUtility.UploadAsync(uploadRequest);

                // Trả về link nhạc chuẩn xác để lưu vào Firebase
                return $"https://{safeBucket}.s3.{_region.SystemName}.amazonaws.com/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi Upload S3: {ex.Message}");
                return null;
            }
        }
    }
}