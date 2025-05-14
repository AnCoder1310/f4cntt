using System.Security.Claims;
using BACKEND.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;


namespace BACKEND.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DBContext _context;

        public AuthController(DBContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Validate input
            if (string.IsNullOrWhiteSpace(request.Username) || 
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username và Password là bắt buộc." });
            }

            // 2. Tìm user
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu." });
            }

            // 3. Lấy RoleName
            var roleName = await _context.Roles
                .Where(r => r.Id == user.RoleId)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync()
                ?? "user";

            // 4. Tạo claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, roleName)
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            );

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(1)
            };

            // 5. Đăng nhập (tạo cookie)
            await AuthenticationHttpContextExtensions.SignInAsync(
                HttpContext,
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties
            );

            // 6. Trả về thông tin user
            return Ok(new
            {
                message = "Đăng nhập thành công",
                user = new
                {
                    user.Id,
                    user.Username,
                    Role = roleName
                }
            });
        }
         [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // 1. Validate input
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 2. Check duplicate Email / Username
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { message = "Email đã tồn tại." });

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest(new { message = "Username đã tồn tại." });

            // 3. Hash password with BCrypt
            string hashed = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 4. Tạo User mới
            var user = new User
            {
                Username     = request.Username,
                Email        = request.Email,
                PasswordHash = hashed,
                FullName     = request.FullName,
                RoleId       = request.RoleId  // ví dụ: 1=admin, 2=user, 3=candidate...
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 5. Lấy role name
            var roleName = await _context.Roles
                .Where(r => r.Id == user.RoleId)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync()
                ?? "user";

            // 6. Tạo claim + sign-in (cookie)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,           user.Username),
                new Claim(ClaimTypes.Role,           roleName)
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            );
            var props = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(1)
            };
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                props
            );

            // 7. Trả về kết quả
            return Ok(new
            {
                message = "Đăng ký thành công",
                user = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.FullName,
                    Role = roleName
                }
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Đăng xuất thành công" });
        }
    }

    // DTO cho Register
    public class RegisterRequest
    {
        [Required] public string Username { get; set; } = null!;
        [Required] public string Email    { get; set; } = null!;
        [Required] public string Password { get; set; } = null!;
        public string? FullName           { get; set; }
        public int    RoleId              { get; set; } = 2;
    }



    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
