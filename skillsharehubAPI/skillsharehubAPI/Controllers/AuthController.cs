using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using skillsharehubAPI.Data;
using skillsharehubAPI.DTOs;
using skillsharehubAPI.Helpers;
using skillsharehubAPI.Models;

namespace skillsharehubAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthHelper _authHelper;
        private readonly IWebHostEnvironment _env;

        public AuthController(ApplicationDbContext context, AuthHelper authHelper, IWebHostEnvironment env)
        {
            _context = context;
            _authHelper = authHelper;
            _env = env;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterDto registerDto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                return BadRequest("Email already exists");

            if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
                return BadRequest("Username already exists");

            if (registerDto.Password != registerDto.ConfirmPassword)
                return BadRequest("Passwords do not match");

            _authHelper.CreatePasswordHash(registerDto.Password, out string passwordHash, out string passwordSalt);

            // Handle profile photo upload
            string profilePhotoPath = "default.jpg";

            if (registerDto.ProfilePhoto != null)
            {
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(registerDto.ProfilePhoto.FileName);
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await registerDto.ProfilePhoto.CopyToAsync(fileStream);
                }

                profilePhotoPath = "/uploads/profiles/" + fileName;
            }

            var user = new User
            {
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Location = registerDto.Location,
                Skills = registerDto.Skills,
                ProfilePhotoUrl = profilePhotoPath
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                token = _authHelper.CreateToken(user),
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    profilePhotoUrl = user.ProfilePhotoUrl
                }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
                return Unauthorized("Invalid email or password");

            if (!_authHelper.VerifyPasswordHash(loginDto.Password, user.PasswordHash, user.PasswordSalt))
                return Unauthorized("Invalid email or password");

            return Ok(new
            {
                token = _authHelper.CreateToken(user),
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    profilePhotoUrl = user.ProfilePhotoUrl
                }
            });
        }
    }
}
