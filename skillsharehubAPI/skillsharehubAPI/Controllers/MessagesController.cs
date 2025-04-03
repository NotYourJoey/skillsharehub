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
    public class MessagesController(ApplicationDbContext context) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Find all users the current user has exchanged messages with
            var messagedUsers = await _context.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .ToListAsync();

            // Get friend IDs
            var friendIds = await _context.Friends
                .Where(f => (f.RequesterId == currentUserId || f.AddresseeId == currentUserId) && f.IsAccepted)
                .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            // Combine both lists and remove duplicates
            var contactUserIds = messagedUsers.Union(friendIds).Distinct().ToList();

            var conversations = new List<object>();

            foreach (var contactUserId in contactUserIds)
            {
                var contactUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == contactUserId);

                if (contactUser == null)
                    continue;

                // Get the latest message between users if exists
                var latestMessage = await _context.Messages
                    .Where(m => (m.SenderId == currentUserId && m.ReceiverId == contactUserId) ||
                               (m.SenderId == contactUserId && m.ReceiverId == currentUserId))
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                // Count unread messages
                var unreadCount = await _context.Messages
                    .CountAsync(m => m.SenderId == contactUserId &&
                                    m.ReceiverId == currentUserId &&
                                    !m.IsRead);

                // Check if they are friends
                var areFriends = friendIds.Contains(contactUserId);

                conversations.Add(new
                {
                    user = new
                    {
                        id = contactUser.Id,
                        firstName = contactUser.FirstName ?? string.Empty,
                        lastName = contactUser.LastName ?? string.Empty,
                        username = contactUser.Username ?? string.Empty,
                        profilePhotoUrl = contactUser.ProfilePhotoUrl ?? string.Empty
                    },
                    lastMessage = latestMessage == null ? null : new
                    {
                        id = latestMessage.Id,
                        content = latestMessage.Content ?? string.Empty,
                        isSender = latestMessage.SenderId == currentUserId,
                        isRead = latestMessage.IsRead,
                        createdAt = latestMessage.CreatedAt
                    },
                    unreadCount,
                    isFriend = areFriends
                });
            }

            return Ok(conversations);
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetMessagesWith(int userId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Check if users are friends
            var areFriends = await _context.Friends
                .AnyAsync(f => ((f.RequesterId == currentUserId && f.AddresseeId == userId) ||
                              (f.RequesterId == userId && f.AddresseeId == currentUserId)) &&
                              f.IsAccepted);

            if (!areFriends)
                return BadRequest("You can only message with friends");

            var messages = await _context.Messages
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                           (m.SenderId == userId && m.ReceiverId == currentUserId))
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    id = m.Id,
                    content = m.Content ?? string.Empty,
                    isSender = m.SenderId == currentUserId,
                    isRead = m.IsRead,
                    createdAt = m.CreatedAt
                })
                .ToListAsync();

            // Mark received messages as read
            var unreadMessages = await _context.Messages
                .Where(m => m.SenderId == userId &&
                          m.ReceiverId == currentUserId &&
                          !m.IsRead)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return Ok(messages);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(MessageDto messageDto)
        {
            if (messageDto == null)
                return BadRequest("Invalid message data");

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Check if users are friends
            var areFriends = await _context.Friends
                .AnyAsync(f => ((f.RequesterId == currentUserId && f.AddresseeId == messageDto.ReceiverId) ||
                              (f.RequesterId == messageDto.ReceiverId && f.AddresseeId == currentUserId)) &&
                              f.IsAccepted);

            if (!areFriends)
                return BadRequest("You can only message with friends");

            var message = new Message
            {
                SenderId = currentUserId,
                ReceiverId = messageDto.ReceiverId,
                Content = messageDto.Content ?? string.Empty
            };

            _context.Messages.Add(message);

            // Create notification
            var notification = new Notification
            {
                UserId = messageDto.ReceiverId,
                Type = "message",
                Message = $"You have a new message"
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = message.Id,
                content = message.Content,
                isSender = true,
                isRead = false,
                createdAt = message.CreatedAt
            });
        }
    }
}
