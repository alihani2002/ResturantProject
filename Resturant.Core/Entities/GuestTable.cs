using Resturant.Core.Common;
using System;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class GuestTable : BaseEntity
    {

        [Display(Name = "Table Number")]
        public int? TableNumber { get; set; }

        [Display(Name = "QR Code URL")]
        public string? QrCodeUrl { get; set; }

        [Display(Name = "Table URL")]
        public string? Url { get; set; }
    }
}
