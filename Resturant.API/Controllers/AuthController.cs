using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Resturant.API.Models;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Resturant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Invalid credentials request");
            }

            var user = await _userManager.FindByEmailAsync(request.Email) 
                       ?? await _userManager.FindByNameAsync(request.Email);

            if (user == null || user.IsDeleted || !user.IsActive)
            {
                return Unauthorized("Invalid email/username or password, or user is inactive.");
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
            {
                return Unauthorized("Invalid email/username or password.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            string userRole = roles.FirstOrDefault() ?? AppRoles.User;

            // Get driver profile if the user is a Driver
            int? driverId = null;
            if (userRole == AppRoles.Driver)
            {
                var driver = await _context.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == user.Id && !d.IsDeleted);
                if (driver != null)
                {
                    driverId = driver.Id;
                }
            }

            // Generate JWT Token
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtSecret = _configuration["Jwt:Secret"] ?? "SuperSecretKeyForResturantAppTokenAuthentication2026!#ResturantKeyForSigningSecurityKeyNeedsToBeVeryLong";
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName ?? user.UserName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Role, userRole),
                new Claim("BranchId", user.BranchId?.ToString() ?? "0")
            };

            if (driverId.HasValue)
            {
                authClaims.Add(new Claim("DriverId", driverId.Value.ToString()));
            }

            var expiryDays = int.TryParse(_configuration["Jwt:ExpiryInDays"], out int days) ? days : 30;
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(authClaims),
                Expires = DateTime.UtcNow.AddDays(expiryDays),
                Issuer = _configuration["Jwt:Issuer"] ?? "ResturantAPI",
                Audience = _configuration["Jwt:Audience"] ?? "ResturantMobileApp",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new LoginResponse
            {
                Token = tokenString,
                Expiration = tokenDescriptor.Expires.Value,
                UserId = user.Id,
                FullName = user.FullName ?? user.UserName ?? "User",
                Email = user.Email ?? "",
                Role = userRole,
                BranchId = user.BranchId,
                DriverId = driverId
            });
        }

        // GET: api/auth/profile
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.Users
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound("User profile not found");
            }

            var roles = await _userManager.GetRolesAsync(user);
            string userRole = roles.FirstOrDefault() ?? AppRoles.User;

            int? driverId = null;
            if (userRole == AppRoles.Driver)
            {
                var driver = await _context.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == user.Id && !d.IsDeleted);
                driverId = driver?.Id;
            }

            return Ok(new UserProfileResponse
            {
                Id = user.Id,
                FullName = user.FullName ?? "N/A",
                Email = user.Email ?? "",
                Role = userRole,
                BranchId = user.BranchId,
                BranchName = user.Branch?.Name ?? "All Branches / Admin",
                IsActive = user.IsActive,
                DriverId = driverId
            });
        }

        // POST: api/auth/change-password
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest($"Password change failed: {errors}");
            }

            return Ok(new { message = "Password updated successfully" });
        }
    }
}
