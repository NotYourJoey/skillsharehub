using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using skillsharehubAPI.Data;
using skillsharehubAPI.Models;
using System.Security.Claims;

namespace skillsharehubAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FriendsController(ApplicationDbContext context) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetFriends()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Get friends where current user is requester
            var friendsAsRequester = await (from f in _context.Friends
                                            join u in _context.Users on f.AddresseeId equals u.Id
                                            where f.RequesterId == currentUserId && f.IsAccepted
                                            select new
                                            {
                                                id = f.Id,
                                                user = new
                                                {
                                                    id = u.Id,
                                                    firstName = u.FirstName ?? string.Empty,
                                                    lastName = u.LastName ?? string.Empty,
                                                    username = u.Username ?? string.Empty,
                                                    profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty
                                                }
                                            }).ToListAsync();

            // Get friends where current user is addressee
            var friendsAsAddressee = await (from f in _context.Friends
                                            join u in _context.Users on f.RequesterId equals u.Id
                                            where f.AddresseeId == currentUserId && f.IsAccepted
                                            select new
                                            {
                                                id = f.Id,
                                                user = new
                                                {
                                                    id = u.Id,
                                                    firstName = u.FirstName ?? string.Empty,
                                                    lastName = u.LastName ?? string.Empty,
                                                    username = u.Username ?? string.Empty,
                                                    profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty
                                                }
                                            }).ToListAsync();

            // Combine both lists
            var allFriends = friendsAsRequester.Concat(friendsAsAddressee).ToList();

            return Ok(allFriends);
        }

        [HttpGet("requests")]
        public async Task<IActionResult> GetFriendRequests()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Get pending friend requests received by current user
            var requests = await (from f in _context.Friends
                                  join u in _context.Users on f.RequesterId equals u.Id
                                  where f.AddresseeId == currentUserId && !f.IsAccepted
                                  select new
                                  {
                                      id = f.Id,
                                      createdAt = f.CreatedAt,
                                      user = new
                                      {
                                          id = u.Id,
                                          firstName = u.FirstName ?? string.Empty,
                                          lastName = u.LastName ?? string.Empty,
                                          username = u.Username ?? string.Empty,
                                          profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty
                                      }
                                  }).ToListAsync();

            return Ok(requests);
        }

        [HttpGet("sent")]
        public async Task<IActionResult> GetSentRequests()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Get pending friend requests sent by current user
            var sentRequests = await (from f in _context.Friends
                                      join u in _context.Users on f.AddresseeId equals u.Id
                                      where f.RequesterId == currentUserId && !f.IsAccepted
                                      select new
                                      {
                                          id = f.Id,
                                          createdAt = f.CreatedAt,
                                          user = new
                                          {
                                              id = u.Id,
                                              firstName = u.FirstName ?? string.Empty,
                                              lastName = u.LastName ?? string.Empty,
                                              username = u.Username ?? string.Empty,
                                              profilePhotoUrl = u.ProfilePhotoUrl ?? string.Empty
                                          }
                                      }).ToListAsync();

            return Ok(sentRequests);
        }

        [HttpPost("request/{userId}")]
        public async Task<IActionResult> SendFriendRequest(int userId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (userId == currentUserId)
                return BadRequest("You cannot send a friend request to yourself");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            // Check if there's already a friend request or connection
            var existingFriendship = await _context.Friends
                .FirstOrDefaultAsync(f =>
                    (f.RequesterId == currentUserId && f.AddresseeId == userId) ||
                    (f.RequesterId == userId && f.AddresseeId == currentUserId));

            if (existingFriendship != null)
            {
                if (existingFriendship.IsAccepted)
                    return BadRequest("You are already friends with this user");
                else if (existingFriendship.RequesterId == currentUserId)
                    return BadRequest("You have already sent a friend request to this user");
                else
                    return BadRequest("This user has already sent you a friend request");
            }

            var friendRequest = new Friend
            {
                RequesterId = currentUserId,
                AddresseeId = userId
            };

            _context.Friends.Add(friendRequest);

            // Create notification for recipient
            var notification = new Notification
            {
                UserId = userId,
                Type = "friend_request",
                Message = $"You have a new friend request"
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = friendRequest.Id,
                createdAt = friendRequest.CreatedAt,
                user = new
                {
                    id = user.Id,
                    firstName = user.FirstName ?? string.Empty,
                    lastName = user.LastName ?? string.Empty,
                    username = user.Username ?? string.Empty,
                    profilePhotoUrl = user.ProfilePhotoUrl ?? string.Empty
                }
            });
        }

        [HttpPost("accept/{requestId}")]
        public async Task<IActionResult> AcceptFriendRequest(int requestId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var request = await _context.Friends
                .FirstOrDefaultAsync(f => f.Id == requestId && f.AddresseeId == currentUserId && !f.IsAccepted);

            if (request == null)
                return NotFound("Friend request not found");

            request.IsAccepted = true;

            // Create notification for sender
            var notification = new Notification
            {
                UserId = request.RequesterId,
                Type = "friend_accepted",
                Message = $"Your friend request was accepted"
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Friend request accepted" });
        }

        [HttpDelete("request/{requestId}")]
        public async Task<IActionResult> DeleteFriendRequest(int requestId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var request = await _context.Friends
                .FirstOrDefaultAsync(f => f.Id == requestId &&
                                     ((f.RequesterId == currentUserId && !f.IsAccepted) ||
                                      (f.AddresseeId == currentUserId && !f.IsAccepted)));

            if (request == null)
                return NotFound("Friend request not found");

            _context.Friends.Remove(request);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Friend request deleted" });
        }

        [HttpDelete("{friendshipId}")]
        public async Task<IActionResult> RemoveFriend(int friendshipId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var friendship = await _context.Friends
                .FirstOrDefaultAsync(f => f.Id == friendshipId &&
                                     ((f.RequesterId == currentUserId && f.IsAccepted) ||
                                      (f.AddresseeId == currentUserId && f.IsAccepted)));

            if (friendship == null)
                return NotFound("Friendship not found");

            _context.Friends.Remove(friendship);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Friend removed" });
        }
    }
}
