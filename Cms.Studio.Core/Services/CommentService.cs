using Microsoft.EntityFrameworkCore;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Core.Services;

public class CommentService
{
    private readonly CmsDbContext _db;

    public CommentService(CmsDbContext db)
    {
        _db = db;
    }

    /// <summary>Approved comments for a post, newest first.</summary>
    public Task<List<Comment>> GetApprovedForPostAsync(int postId) =>
        _db.Comments.AsNoTracking()
            .Where(c => c.PostId == postId && c.IsApproved)
            .OrderByDescending(c => c.CreatedOnUtc)
            .ToListAsync();

    public async Task<Comment> CreateAsync(int postId, int? parentId, string nickName, string email, string content)
    {
        var comment = new Comment
        {
            PostId = postId,
            ParentId = parentId,
            NickName = nickName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Content = content.Trim(),
            CreatedOnUtc = DateTime.UtcNow,
            IsApproved = false
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return comment;
    }

    /// <summary>Duplicate guard (masuit-style): identical content from the same email within the window.</summary>
    public Task<bool> IsDuplicateAsync(int postId, string email, string content, TimeSpan window)
    {
        email = email.Trim().ToLowerInvariant();
        content = content.Trim();
        var since = DateTime.UtcNow - window;
        return _db.Comments.AnyAsync(c => c.PostId == postId && c.Email == email && c.Content == content && c.CreatedOnUtc >= since);
    }

    // ---------- admin ----------

    public async Task<List<Comment>> AdminListAsync(bool onlyPending, int count = 500)
    {
        var query = _db.Comments.AsNoTracking().AsQueryable();
        if (onlyPending)
            query = query.Where(c => !c.IsApproved);
        return await query.OrderByDescending(c => c.CreatedOnUtc).Take(count).ToListAsync();
    }

    public async Task<bool> ApproveAsync(int id)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == id);
        if (comment == null)
            return false;
        comment.IsApproved = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == id);
        if (comment == null)
            return false;
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<int> CountPendingAsync() => _db.Comments.CountAsync(c => !c.IsApproved);

    public Task<int> CountForPostAsync(int postId) => _db.Comments.CountAsync(c => c.PostId == postId && c.IsApproved);
}
