using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using QRCoder;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using Resturant.Infrastructure.Data;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class QrCodesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public QrCodesController(AppDbContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QrCode qrCode, string WebsiteUrl, string BrandName, IFormFile? LogoImage, IFormFile? BottomImage)
        {
            if (ModelState.IsValid)
            {
                // URL is manually entered or constructed
                qrCode.Url = WebsiteUrl;

                // Generate Custom QR Code
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(WebsiteUrl, QRCodeGenerator.ECCLevel.Q);
                    
                    // Use helper method to generate composite image (A5)
                    byte[] qrCodeBytes = GenerateCompositeQrImage(qrCodeData, BrandName, LogoImage, BottomImage);

                    string fileName = $"QR_{Guid.NewGuid()}.png";
                    string qrUrl = await _cloudinaryService.UploadImageAsync(qrCodeBytes, fileName, "qr");
                    qrCode.QrCodeUrl = qrUrl;
                }

                _context.Add(qrCode);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["WebsiteUrl"] = WebsiteUrl;
            ViewData["BrandName"] = BrandName;
            return View(qrCode);
        }

        private byte[] GenerateCompositeQrImage(QRCodeData qrCodeData, string brandName, IFormFile? logoImage, IFormFile? bottomImage)
        {
            // A5 Dimensions @ ~150 DPI -> 874 x 1240 pixels
            int canvasWidth = 874;
            int canvasHeight = 1240;
            int padding = 40;

            // 1. Generate Base QR Code
            using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] baseQrBytes = qrCode.GetGraphic(20);
                using (MemoryStream ms = new MemoryStream(baseQrBytes))
                using (Bitmap qrBitmap = new Bitmap(ms))
                {
                    using (Bitmap finalImage = new Bitmap(canvasWidth, canvasHeight))
                    using (Graphics g = Graphics.FromImage(finalImage))
                    {
                        g.Clear(Color.White);
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                        int currentY = padding;

                        // --- Brand Name ---
                        if (!string.IsNullOrEmpty(brandName))
                        {
                            using (Font font = new Font("Arial", 40, FontStyle.Bold))
                            {
                                SizeF textSize = g.MeasureString(brandName, font);
                                g.DrawString(brandName, font, Brushes.Black, (canvasWidth - textSize.Width) / 2, currentY);
                                currentY += (int)textSize.Height + 20;
                            }
                        }
                        else
                        {
                            currentY += 20;
                        }

                        // --- Logo ---
                        if (logoImage != null && logoImage.Length > 0)
                        {
                            using (var logoStream = logoImage.OpenReadStream())
                            using (Bitmap logoBitmap = new Bitmap(logoStream))
                            {
                                int maxLogoWidth = 120;
                                int maxLogoHeight = 120;
                                
                                float ratioX = (float)maxLogoWidth / logoBitmap.Width;
                                float ratioY = (float)maxLogoHeight / logoBitmap.Height;
                                float ratio = Math.Min(ratioX, ratioY);

                                int newWidth = (int)(logoBitmap.Width * ratio);
                                int newHeight = (int)(logoBitmap.Height * ratio);

                                g.DrawImage(logoBitmap, (canvasWidth - newWidth) / 2, currentY, newWidth, newHeight);
                                currentY += newHeight + 30;
                            }
                        }

                        // --- QR Code ---
                        int qrTargetSize = 500;
                        g.DrawImage(qrBitmap, (canvasWidth - qrTargetSize) / 2, currentY, qrTargetSize, qrTargetSize);
                        currentY += qrTargetSize + 20;

                        // -- Spacer where Table Number was --
                        currentY += 40;

                        // --- Bottom Image ---
                        if (bottomImage != null && bottomImage.Length > 0)
                        {
                            using (var bottomStream = bottomImage.OpenReadStream())
                            using (Bitmap bottomBitmap = new Bitmap(bottomStream))
                            {
                                int maxBottomWidth = 120;
                                int maxBottomHeight = 120;

                                float ratioX = (float)maxBottomWidth / bottomBitmap.Width;
                                float ratioY = (float)maxBottomHeight / bottomBitmap.Height;
                                float ratio = Math.Min(ratioX, ratioY);

                                int newWidth = (int)(bottomBitmap.Width * ratio);
                                int newHeight = (int)(bottomBitmap.Height * ratio);
                                
                                g.DrawImage(bottomBitmap, (canvasWidth - newWidth) / 2, currentY, newWidth, newHeight);
                            }
                        }

                        using (MemoryStream resultStream = new MemoryStream())
                        {
                            finalImage.Save(resultStream, ImageFormat.Png);
                            return resultStream.ToArray();
                        }
                    }
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> LoadData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                var qrcodeData = (from temp in _context.QrCodes select temp);

                if (!string.IsNullOrEmpty(searchValue))
                {
                    qrcodeData = qrcodeData.Where(m => m.Url.Contains(searchValue));
                }

                if (!(string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection)))
                {
                    switch (sortColumn)
                    {
                        case "Url":
                            qrcodeData = sortColumnDirection == "asc" ? qrcodeData.OrderBy(c => c.Url) : qrcodeData.OrderByDescending(c => c.Url);
                            break;
                        default:
                            qrcodeData = qrcodeData.OrderByDescending(c => c.Id);
                            break;
                    }
                }

                int recordsTotal = qrcodeData.Count();
                var data = qrcodeData.Skip(skip).Take(pageSize).ToList();
                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data });
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
