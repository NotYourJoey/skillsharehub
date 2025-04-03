using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using skillsharehubAPI.Data;
using skillsharehubAPI.Models;
using System.Security.Claims;

namespace skillsharehubAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController(ApplicationDbContext context, IWebHostEnvironment env) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    id = u.Id,
                    firstName = u.FirstName ?? string.Empty,
                    lastName = u.LastName ?? string.Empty,
                    username = u.Username ?? string.Empty,
                    location = u.Location ?? string.Empty,
                    skills = u.Skills ?? string.Empty,
                    profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty,
                    createdAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] string search = "", [FromQuery] string skill = "")
        {
            var query = _context.Users.AsQueryable();

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u =>
                    u.Username.Contains(search) ||
                    u.FirstName.Contains(search) ||
                    u.LastName.Contains(search));
            }

            // Apply skill filter if provided
            if (!string.IsNullOrEmpty(skill))
            {
                query = query.Where(u => u.Skills.Contains(skill));
            }

            var users = await query
                .Select(u => new
                {
                    id = u.Id,
                    firstName = u.FirstName ?? string.Empty,
                    lastName = u.LastName ?? string.Empty,
                    username = u.Username ?? string.Empty,
                    profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty,
                    skills = u.Skills ?? string.Empty
                })
                .Take(20) // Limit results
                .ToListAsync();

            return Ok(users);
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var user = await _context.Users
                .Where(u => u.Id == currentUserId)
                .Select(u => new
                {
                    id = u.Id,
                    firstName = u.FirstName ?? string.Empty,
                    lastName = u.LastName ?? string.Empty,
                    username = u.Username ?? string.Empty,
                    email = u.Email ?? string.Empty,
                    location = u.Location ?? string.Empty,
                    skills = u.Skills ?? string.Empty,
                    profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty,
                    createdAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto profileDto)
        {
            if (profileDto == null)
                return BadRequest("Invalid profile data");

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var user = await _context.Users.FindAsync(currentUserId);
            if (user == null)
                return NotFound();

            // Update fields if provided
            if (!string.IsNullOrEmpty(profileDto.FirstName))
                user.FirstName = profileDto.FirstName;

            if (!string.IsNullOrEmpty(profileDto.LastName))
                user.LastName = profileDto.LastName;

            if (!string.IsNullOrEmpty(profileDto.Location))
                user.Location = profileDto.Location;

            if (!string.IsNullOrEmpty(profileDto.Skills))
                user.Skills = profileDto.Skills;

            // Handle profile photo update if provided
            if (profileDto.ProfilePhoto != null)
            {
                // Delete old profile photo if it exists and is not the default
                if (!string.IsNullOrEmpty(user.ProfilePhotoUrl) &&
                    !user.ProfilePhotoUrl.Contains("default") &&
                    System.IO.File.Exists(Path.Combine(_env.WebRootPath, user.ProfilePhotoUrl.TrimStart('/'))))
                {
                    System.IO.File.Delete(Path.Combine(_env.WebRootPath, user.ProfilePhotoUrl.TrimStart('/')));
                }

                // Save new profile photo
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(profileDto.ProfilePhoto.FileName);
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await profileDto.ProfilePhoto.CopyToAsync(fileStream);
                }

                user.ProfilePhotoUrl = "/uploads/profiles/" + fileName;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = user.Id,
                firstName = user.FirstName ?? string.Empty,
                lastName = user.LastName ?? string.Empty,
                username = user.Username ?? string.Empty,
                email = user.Email ?? string.Empty,
                location = user.Location ?? string.Empty,
                skills = user.Skills ?? string.Empty,
                profilePhotoUrl = user.ProfilePhotoUrl ?? string.Empty
            });
        }

        [Authorize]
        [HttpGet("suggested-friends")]
        public async Task<IActionResult> GetSuggestedFriends()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Get current user's skills
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser == null)
                return NotFound();

            // Get IDs of existing friends and pending requests
            var existingConnections = await _context.Friends
                .Where(f => f.RequesterId == currentUserId || f.AddresseeId == currentUserId)
                .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            // Add current user ID to filter out
            existingConnections.Add(currentUserId);

            // Find users with similar skills
            var skillsList = currentUser.Skills?.Split(',').Select(s => s.Trim()).ToList() ?? [];

            var suggestedUsers = await _context.Users
                .Where(u => !existingConnections.Contains(u.Id))
                .OrderByDescending(u => skillsList.Count(skill => u.Skills != null && u.Skills.Contains(skill)))
                .Take(10)
                .Select(u => new
                {
                    id = u.Id,
                    firstName = u.FirstName ?? string.Empty,
                    lastName = u.LastName ?? string.Empty,
                    username = u.Username ?? string.Empty,
                    profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty,
                    skills = u.Skills ?? string.Empty
                })
                .ToListAsync();

            return Ok(suggestedUsers);
        }

        [Authorize]
        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Get IDs of friends
            var friendIds = await _context.Friends
                .Where(f => (f.RequesterId == currentUserId || f.AddresseeId == currentUserId) && f.IsAccepted)
                .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            // Include current user to see own posts
            friendIds.Add(currentUserId);

            var posts = await (from p in _context.Posts
                               join u in _context.Users on p.UserId equals u.Id
                               where friendIds.Contains(p.UserId)
                               orderby p.CreatedAt descending
                               select new
                               {
                                   id = p.Id,
                                   content = p.Content ?? string.Empty,
                                   mediaUrl = p.MediaUrl,
                                   mediaType = p.MediaType,
                                   createdAt = p.CreatedAt,
                                   user = new
                                   {
                                       id = u.Id,
                                       username = u.Username ?? string.Empty,
                                       profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty
                                   },
                                   likesCount = _context.Likes.Count(l => l.PostId == p.Id),
                                   commentsCount = _context.Comments.Count(c => c.PostId == p.Id),
                                   isLiked = _context.Likes.Any(l => l.PostId == p.Id && l.UserId == currentUserId)
                               })
                              .Take(20) // Limit results for paging
                              .ToListAsync();

            return Ok(posts);
        }
    }

    public class UpdateProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Location { get; set; }
        public string? Skills { get; set; }
        public IFormFile? ProfilePhoto { get; set; }
    }
}
