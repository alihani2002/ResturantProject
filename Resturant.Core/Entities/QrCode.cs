using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Resturant.Core.Entities
{
    public class QrCode : BaseEntity
    {
        [Display(Name = "QR Code URL")]
        public string? QrCodeUrl { get; set; }

        [Display(Name = "Target URL")]
        public string? Url { get; set; }
    }
}
