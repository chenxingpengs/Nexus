using QRCoder;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Nexus.Services
{
    /// <summary>
    /// 二维码服务 - 生成二维码图片
    /// </summary>
    public class QRCodeService : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// 生成基础二维码
        /// </summary>
        /// <param name="plainText">二维码内容</param>
        /// <param name="pixelsPerModule">每个模块的像素大小</param>
        /// <returns>二维码图片</returns>
        public Bitmap GenerateBasicQRCode(string plainText, int pixelsPerModule = 20)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            throw new ArgumentException("二维码内容不能为空", nameof(plainText));

            if (pixelsPerModule < 1 || pixelsPerModule > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelsPerModule), "像素大小必须在 1-100 之间");
            }

            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(plainText, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new QRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(pixelsPerModule);

                if (qrCodeImage == null)
                {
                    throw new InvalidOperationException("二维码生成失败：返回 null");
                }

                return qrCodeImage;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException($"生成二维码失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 生成二维码并返回 Base64 字符串
        /// </summary>
        /// <param name="plainText">二维码内容</param>
        /// <param name="pixelsPerModule">每个模块的像素大小</param>
        /// <returns>Base64 编码的二维码图片</returns>
        public (bool Success, string? Base64, string? ErrorMessage) GenerateQRCodeBase64(string plainText, int pixelsPerModule = 20)
        {
            try
            {
                using var bitmap = GenerateBasicQRCode(plainText, pixelsPerModule);
                using var memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, ImageFormat.Png);
                var base64 = Convert.ToBase64String(memoryStream.ToArray());
                return (true, base64, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// 生成带 Logo 的二维码
        /// </summary>
        /// <param name="plainText">二维码内容</param>
        /// <param name="logoImage">Logo 图片</param>
        /// <param name="pixelsPerModule">每个模块的像素大小</param>
        /// <returns>二维码图片</returns>
        public Bitmap GenerateQRCodeWithLogo(string plainText, Bitmap logoImage, int pixelsPerModule = 20)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                throw new ArgumentException("二维码内容不能为空", nameof(plainText));

            if (logoImage == null)
            {
                throw new ArgumentNullException(nameof(logoImage));
            }

            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(plainText, QRCodeGenerator.ECCLevel.H);
                using var qrCode = new QRCode(qrCodeData);

                // Logo 占二维码的 15%
                int iconSizePercent = 15;
                var qrCodeImage = qrCode.GetGraphic(pixelsPerModule, Color.Black, Color.White, logoImage, iconSizePercent);

                if (qrCodeImage == null)
                {
                    throw new InvalidOperationException("带 Logo 的二维码生成失败：返回 null");
                }

                return qrCodeImage;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException($"生成带 Logo 的二维码失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 生成彩色二维码
        /// </summary>
        /// <param name="plainText">二维码内容</param>
        /// <param name="darkColor">深色模块颜色</param>
        /// <param name="lightColor">浅色模块颜色</param>
        /// <param name="pixelsPerModule">每个模块的像素大小</param>
        /// <returns>二维码图片</returns>
        public Bitmap GenerateColoredQRCode(
            string plainText,
            Color darkColor,
            Color lightColor,
            int pixelsPerModule = 20)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                throw new ArgumentException("二维码内容不能为空", nameof(plainText));

            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(plainText, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new QRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(
                    pixelsPerModule: pixelsPerModule,
                    darkColor: darkColor,
                    lightColor: lightColor,
                    drawQuietZones: true);

                if (qrCodeImage == null)
                {
                    throw new InvalidOperationException("彩色二维码生成失败：返回 null");
                }

                return qrCodeImage;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException($"生成彩色二维码失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证二维码内容是否有效
        /// </summary>
        public static bool IsValidContent(string content, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(content))
            {
                errorMessage = "二维码内容不能为空";
                return false;
            }

            // QR Code 最大容量约为 2953 字节（字母数字模式）
            if (content.Length > 2000)
            {
                errorMessage = "二维码内容过长（最大支持 2000 字符）";
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
