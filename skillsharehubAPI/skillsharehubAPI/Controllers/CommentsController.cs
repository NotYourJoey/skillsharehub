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
    [Route("api/posts/{postId}/comments")]
    [ApiController]
    public class CommentsController(ApplicationDbContext context) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetComments(int postId)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post == null)
                return NotFound();

            // Using a join approach instead of navigation properties
            var comments = await (from c in _context.Comments
                                  join u in _context.Users on c.UserId equals u.Id
                                  where c.PostId == postId
                                  orderby c.CreatedAt
                                  select new
                                  {
                                      id = c.Id,
                                      content = c.Content ?? string.Empty,
                                      createdAt = c.CreatedAt,
                                      user = new
                                      {
                                          id = u.Id,
                                          username = u.Username ?? string.Empty,
                                          profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty
                                      }
                                  }).ToListAsync();

            return Ok(comments);
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int postId, CommentDto commentDto)
        {
            if (commentDto == null)
                return BadRequest("Invalid comment data");

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var post = await _context.Posts.FindAsync(postId);
            if (post == null)
                return NotFound();

            var comment = new Comment
            {
                PostId = postId,
                UserId = currentUserId,
                Content = commentDto.Content ?? string.Empty
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(currentUserId);
            if (user == null)
                return NotFound("User not found");

            return Ok(new
            {
                id = comment.Id,
                content = comment.Content,
                createdAt = comment.CreatedAt,
                user = new
                {
                    id = user.Id,
                    username = user.Username ?? string.Empty,
                    profilePhotoUrl = user.ProfilePhotoUrl ?? string.Empty
                }
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(int postId, int id)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var comment = await _context.Comments.FindAsync(id);

            if (comment == null || comment.PostId != postId)
                return NotFound();

            if (comment.UserId != currentUserId)
                return Forbid();

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
