using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using skillsharehubAPI.Data;
using skillsharehubAPI.DTOs;
using skillsharehubAPI.Models;
using System.Security.Claims;

namespace skillsharehubAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController(ApplicationDbContext context, IWebHostEnvironment env) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;

        [HttpGet]
        public async Task<IActionResult> GetPosts()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var posts = await (from p in _context.Posts
                               join u in _context.Users on p.UserId equals u.Id
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
                               }).ToListAsync();

            return Ok(posts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPost(int id)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var post = await (from p in _context.Posts
                              join u in _context.Users on p.UserId equals u.Id
                              where p.Id == id
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
                              }).FirstOrDefaultAsync();

            if (post == null)
                return NotFound();

            return Ok(post);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromForm] PostCreateDto postDto)
        {
            if (postDto == null)
                return BadRequest("Invalid post data");

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            string? mediaUrl = null;
            string? mediaType = null;

            if (postDto.Media != null)
            {
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(postDto.Media.FileName);
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "posts");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await postDto.Media.CopyToAsync(fileStream);
                }

                mediaUrl = "/uploads/posts/" + fileName;

                // Determine media type based on file extension
                string ext = Path.GetExtension(fileName).ToLower();
                mediaType = (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif")
                    ? "image"
                    : "video";
            }

            var post = new Post
            {
                UserId = currentUserId,
                Content = postDto.Content ?? string.Empty,
                MediaUrl = mediaUrl ?? string.Empty, 
                MediaType = mediaType ?? string.Empty 
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(currentUserId);

            if (user == null)
                return NotFound("User not found");

            return Ok(new
            {
                id = post.Id,
                content = post.Content,
                mediaUrl = post.MediaUrl,
                mediaType = post.MediaType,
                createdAt = post.CreatedAt,
                user = new
                {
                    id = user.Id,
                    username = user.Username ?? string.Empty,
                    profilePhotoUrl = user.ProfilePhotoUrl ?? string.Empty
                },
                likesCount = 0,
                commentsCount = 0,
                isLiked = false
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var post = await _context.Posts.FindAsync(id);

            if (post == null)
                return NotFound();

            if (post.UserId != currentUserId)
                return Forbid();

            // Delete media file if exists
            if (!string.IsNullOrEmpty(post.MediaUrl))
            {
                string filePath = Path.Combine(_env.WebRootPath, post.MediaUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("{id}/like")]
        public async Task<IActionResult> LikePost(int id)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return NotFound();

            var existingLike = await _context.Likes
                .FirstOrDefaultAsync(l => l.PostId == id && l.UserId == currentUserId);

            if (existingLike != null)
                return BadRequest("Post already liked");

            var like = new Like
            {
                PostId = id,
                UserId = currentUserId
            };

            _context.Likes.Add(like);
            await _context.SaveChangesAsync();

            return Ok(new { likesCount = await _context.Likes.CountAsync(l => l.PostId == id) });
        }

        [HttpDelete("{id}/like")]
        public async Task<IActionResult> UnlikePost(int id)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var like = await _context.Likes
                .FirstOrDefaultAsync(l => l.PostId == id && l.UserId == currentUserId);

            if (like == null)
                return BadRequest("Post not liked");

            _context.Likes.Remove(like);
            await _context.SaveChangesAsync();

            return Ok(new { likesCount = await _context.Likes.CountAsync(l => l.PostId == id) });
        }
    }
}
