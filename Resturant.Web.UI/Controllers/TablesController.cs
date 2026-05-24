/* 
 * NOTE: didn't create migration or database
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public class TablesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public TablesController(AppDbContext context, ICloudinaryService cloudinaryService)
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
        public async Task<IActionResult> Create(RestaurantTable restaurantTable, string WebsiteUrl)
        {
            if (ModelState.IsValid)
            {
                // Generate QR code URL: [WebsiteUrl]/Scan?tableNumber=[Number]
                string scanUrl = $"{WebsiteUrl.TrimEnd('/')}/CustomerSession/Scan?tableNumber={restaurantTable.TableNumber}";

                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(scanUrl, QRCodeGenerator.ECCLevel.Q))
                    {
                        // Generate composite image with Table Number
                        byte[] qrCodeBytes = GenerateTableQrWithNumber(qrCodeData, restaurantTable.TableNumber);
                        
                        string fileName = $"TableQR_{restaurantTable.TableNumber}_{Guid.NewGuid()}.png";
                        restaurantTable.QrCodeImageUrl = await _cloudinaryService.UploadImageAsync(qrCodeBytes, fileName, "tables_qr");
                    }
                }

                _context.Add(restaurantTable);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(restaurantTable);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var table = await _context.RestaurantTables.FindAsync(id);
            if (table == null)
            {
                return Json(new { success = false, message = "Table not found" });
            }

            _context.RestaurantTables.Remove(table);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Table deleted successfully" });
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

                var customerData = (from tempcustomer in _context.RestaurantTables select tempcustomer);

                if (!string.IsNullOrEmpty(searchValue))
                {
                    customerData = customerData.Where(m => m.TableNumber.ToString().Contains(searchValue));
                }

                if (!(string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection)))
                {
                    switch (sortColumn)
                    {
                        case "TableNumber":
                            customerData = sortColumnDirection == "asc" ? customerData.OrderBy(c => c.TableNumber) : customerData.OrderByDescending(c => c.TableNumber);
                            break;
                        default:
                            customerData = customerData.OrderBy(c => c.TableNumber);
                            break;
                    }
                }

                int recordsTotal = customerData.Count();
                var data = customerData.Skip(skip).Take(pageSize).ToList();
                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data });
            }
            catch (Exception)
            {
                throw;
            }
        }
        private byte[] GenerateTableQrWithNumber(QRCodeData qrCodeData, int tableNumber)
        {
            // Set dimensions for the composite image
            int width = 600;
            int height = 750; // Extra height for the table number text

            using (QRCode qrCode = new QRCode(qrCodeData))
            using (Bitmap qrBitmap = qrCode.GetGraphic(20))
            using (Bitmap finalImage = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(finalImage))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                // 1. Draw QR Code
                g.DrawImage(qrBitmap, 50, 20, 500, 500);

                // 2. Draw Table Number Text
                string text = $"TABLE {tableNumber}";
                using (Font font = new Font("Arial", 45, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(text, font);
                    float x = (width - textSize.Width) / 2;
                    float y = 550 + (150 - textSize.Height) / 2;
                    
                    g.DrawString(text, font, Brushes.Black, x, y);
                }

                // 3. Optional: Draw a border or accent
                using (Pen pen = new Pen(Color.Black, 5))
                {
                    g.DrawRectangle(pen, 10, 10, width - 20, height - 20);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    finalImage.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }
    }
}

