using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var tables = _context.GuestTables.OrderBy(t => t.TableNumber).ToList();
            return View(tables);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GuestTable guestTable, string WebsiteUrl)
        {
            if (ModelState.IsValid)
            {
                // Check if table number exists (only if not null)
                if (guestTable.TableNumber.HasValue && _context.GuestTables.Any(t => t.TableNumber == guestTable.TableNumber))
                {
                    ModelState.AddModelError("TableNumber", "Table Number already exists.");
                    return View(guestTable);
                }

                _context.Add(guestTable);
                await _context.SaveChangesAsync();

                // Generate URL
                string url;
                if (!string.IsNullOrEmpty(WebsiteUrl))
                {
                    url = WebsiteUrl.TrimEnd('/');
                    if (!url.EndsWith("/Menu"))
                    {
                         url += "/Menu";
                    }
                }
                else
                {
                    string scheme = Request.Scheme;
                    string host = Request.Host.Value;
                    url = $"{scheme}://{host}/Menu";
                }

                if (guestTable.TableNumber.HasValue)
                {
                    url += $"?tableNumber={guestTable.TableNumber}";
                }
                guestTable.Url = url;

                // Generate QR Code
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                    using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                    {
                        byte[] qrCodeBytes = qrCode.GetGraphic(20);
                        string fileName = $"Table_{(guestTable.TableNumber.HasValue ? guestTable.TableNumber.ToString() : "General_" + guestTable.Id)}_QR.png";
                        string qrUrl = await _cloudinaryService.UploadImageAsync(qrCodeBytes, fileName, "qr");
                        guestTable.QrCodeUrl = qrUrl;
                    }
                }

                _context.Update(guestTable);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(guestTable);
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

                var customerData = (from tempcustomer in _context.GuestTables select tempcustomer);

                if (!string.IsNullOrEmpty(searchValue))
                {
                    customerData = customerData.Where(m => (m.TableNumber != null && m.TableNumber.ToString().Contains(searchValue)) || m.Url.Contains(searchValue));
                }

                // Sorting
                if (!(string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection)))
                {
                    // Simple sorting logic since generic dynamic LINQ might not be available
                    switch (sortColumn)
                    {
                        case "TableNumber":
                            customerData = sortColumnDirection == "asc" ? customerData.OrderBy(c => c.TableNumber) : customerData.OrderByDescending(c => c.TableNumber);
                            break;
                        case "Url":
                            customerData = sortColumnDirection == "asc" ? customerData.OrderBy(c => c.Url) : customerData.OrderByDescending(c => c.Url);
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
    }
}
