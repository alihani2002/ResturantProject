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

        private int? GetActiveBranchId()
        {
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }

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
            ViewBag.Branches = _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToList();
            ViewBag.WebsiteUrl = $"{Request.Scheme}://{Request.Host}";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RestaurantTable restaurantTable, string WebsiteUrl)
        {
            if (ModelState.IsValid)
            {
                // Generate QR code URL with branchId and tableNumber: [WebsiteUrl]/CustomerSession/Scan?branchId=[BranchId]&tableNumber=[Number]
                string scanUrl = $"{WebsiteUrl.TrimEnd('/')}/CustomerSession/Scan?branchId={restaurantTable.BranchId}&tableNumber={restaurantTable.TableNumber}";

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
            ViewBag.Branches = _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToList();
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

                var activeBranchId = GetActiveBranchId();
                var customerData = _context.RestaurantTables
                    .Include(t => t.Branch)
                    .Include(t => t.Waiter)
                    .Include(t => t.Sessions)
                    .AsQueryable();

                if (activeBranchId.HasValue)
                {
                    customerData = customerData.Where(t => t.BranchId == activeBranchId.Value);
                }

                if (!string.IsNullOrEmpty(searchValue))
                {
                    customerData = customerData.Where(m => m.TableNumber.ToString().Contains(searchValue) || m.Branch.Name.Contains(searchValue));
                }

                if (!(string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection)))
                {
                    switch (sortColumn)
                    {
                        case "TableNumber":
                            customerData = sortColumnDirection == "asc" ? customerData.OrderBy(c => c.TableNumber) : customerData.OrderByDescending(c => c.TableNumber);
                            break;
                        case "BranchName":
                            customerData = sortColumnDirection == "asc" ? customerData.OrderBy(c => c.Branch.Name) : customerData.OrderByDescending(c => c.Branch.Name);
                            break;
                        default:
                            customerData = customerData.OrderBy(c => c.TableNumber);
                            break;
                    }
                }

                int recordsTotal = customerData.Count();
                var dataList = customerData.Skip(skip).Take(pageSize).ToList();
                var data = dataList.Select(t => new {
                    t.Id,
                    t.TableNumber,
                    Capacity = 4, // Standard fallback table capacity
                    t.QrCodeImageUrl,
                    WaiterName = t.Waiter?.FullName ?? t.Waiter?.UserName ?? "Unassigned",
                    IsOccupied = t.Sessions.Any(s => s.IsActive),
                    t.BranchId,
                    BranchName = t.Branch?.Name ?? "Unknown"
                }).ToList();

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

