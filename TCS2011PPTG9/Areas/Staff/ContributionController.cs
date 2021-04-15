using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TCS2011PPTG9.Data;
using TCS2011PPTG9.Models;
using MimeKit;
using MailKit.Net.Smtp;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;

namespace TCS2011PPTG9.Areas.Staff
{
    [Area("Staff")]
    [Authorize(Roles = "Manager, Coordinator")]
    public class ContributionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContributionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Staff/Contribution
        public async Task<IActionResult> Index(int topicId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = await _context.UserRoles.FirstOrDefaultAsync(u => u.UserId == userId);

            var contribution = await _context.Contribution.Include(c => c.Contributor).Include(c => c.Topic)
                                                          .Where(c => c.TopicId == topicId)
                                                          .ToListAsync();

            if (userRole.RoleId == "Coordinatior")
            {
                var user = await _context.Users.FindAsync(userId);
                contribution = contribution.Where(c => c.Contributor.DepartmentId == user.DepartmentId).ToList();
            }
            else if (userRole.RoleId == "Manager")
            {
                contribution = contribution.Where(c => c.Status == ContributionStatus.Approved).ToList();
            }

            ViewData["TopicId"] = topicId;
            return View(contribution);
        }

        // GET: Staff/Contribution/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contribution = await _context.Contribution
                .Include(c => c.Contributor)
                .Include(c => c.Topic)
                .Include(c => c.Files)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contribution == null)
            {
                return NotFound();
            }

            List<Comment> comments = await _context.Comment.Include(c => c.User)
                                                .Where(c => c.ContributionId == id)
                                                .OrderBy(c => c.Date)
                                                .ToListAsync();

            ViewData["currentContribution"] = contribution;
            ViewData["coments"] = comments;

            return View(contribution);
        }

        // GET: Staff/Contribution/Create
        public IActionResult Create()
        {
            ViewData["ContributorId"] = new SelectList(_context.Users, "Id", "Id");
            ViewData["TopicId"] = new SelectList(_context.Topic, "Id", "Id");
            return View();
        }

        // POST: Staff/Contribution/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Status,ContributorId,TopicId")] Contribution contribution)
        {
            if (ModelState.IsValid)
            {
                _context.Add(contribution);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ContributorId"] = new SelectList(_context.Users, "Id", "Id", contribution.ContributorId);
            ViewData["TopicId"] = new SelectList(_context.Topic, "Id", "Id", contribution.TopicId);
            return View(contribution);
        }

        // GET: Staff/Contribution/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contribution = await _context.Contribution.FindAsync(id);
            if (contribution == null)
            {
                return NotFound();
            }
            ViewData["ContributorId"] = new SelectList(_context.Users, "Id", "Id", contribution.ContributorId);
            ViewData["TopicId"] = new SelectList(_context.Topic, "Id", "Id", contribution.TopicId);
            return View(contribution);
        }

        // POST: Staff/Contribution/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Status,ContributorId,TopicId")] Contribution contribution)
        {
            if (id != contribution.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(contribution);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContributionExists(contribution.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["ContributorId"] = new SelectList(_context.Users, "Id", "Id", contribution.ContributorId);
            ViewData["TopicId"] = new SelectList(_context.Topic, "Id", "Id", contribution.TopicId);
            return View(contribution);
        }

        // GET: Staff/Contribution/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contribution = await _context.Contribution
                .Include(c => c.Contributor)
                .Include(c => c.Topic)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contribution == null)
            {
                return NotFound();
            }

            return View(contribution);
        }

        // POST: Staff/Contribution/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contribution = await _context.Contribution.FindAsync(id);
            _context.Contribution.Remove(contribution);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ContributionExists(int id)
        {
            return _context.Contribution.Any(e => e.Id == id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comment(int contributionId, String commentContent)
        {
            if (ModelState.IsValid)
            {

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var existContribution = await _context.Contribution.FindAsync(contributionId);
                if (existContribution != null && !String.IsNullOrEmpty(commentContent))
                {
                    var comment = new Comment();

                    comment.Content = commentContent;
                    comment.ContributionId = existContribution.Id;
                    comment.UserId = userId;
                    comment.Date = DateTime.Now;
                    _context.Add(comment);
                    await _context.SaveChangesAsync();

                }

            }
            return RedirectToAction(nameof(Details), new { id = contributionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Mark(int contributionId = -1, ContributionStatus contributionStatus = ContributionStatus.Pending)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var contribution = await _context.Contribution.Include(c => c.Contributor)
                                                                       .Include(c => c.Topic)
                                                                        .FirstOrDefaultAsync(c => c.Id == contributionId);

                    contribution.Status = contributionStatus;

                    _context.Update(contribution);
                    await _context.SaveChangesAsync();

                    var contributorFullname = $"{contribution.Contributor.FirstName} {contribution.Contributor.LastName}";

                    MailboxAddress from = new MailboxAddress("iMarketing System", "systememail8000@gmail.com");
                    MailboxAddress to = new MailboxAddress(contributorFullname, contribution.Contributor.Email);

                    BodyBuilder bodyBuilder = new BodyBuilder();
                    bodyBuilder.TextBody = $"Hello {contributorFullname}, \n\n" +
                                           $"Your contribution for {contribution.Topic.Title} is {contributionStatus}.\n\n" +
                                           $"Thank you for your contribution,\n\n" +
                                           $"Best regards,";

                    MimeMessage message = new MimeMessage();
                    message.From.Add(from);
                    message.To.Add(to);
                    message.Subject = $"contribution for {contribution.Topic.Title} Status";
                    message.Body = bodyBuilder.ToMessageBody();

                    SmtpClient client = new SmtpClient();
                    client.Connect("smtp.gmail.com", 465, true);
                    client.Authenticate("systememail8000", "abcABC@123");

                    client.Send(message);
                    client.Disconnect(true);
                    client.Dispose();
                }

                catch (DbUpdateConcurrencyException)
                {
                    if (!ContributionExists(contributionId)) { return NotFound(); }
                    else { throw; }
                }

            }
            return RedirectToAction(nameof(Details), new { id = contributionId });
        }

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DownloadApprovedFile(int topicId = -1)
        {
            var approvedContributions = await _context.Contribution.Include(c => c.Contributor).Include(c => c.Files)
                .Where(c => c.TopicId == topicId && c.Status == ContributionStatus.Approved).ToListAsync();

            if (approvedContributions.Count() > 0)
            {
                var topic = await _context.Topic.FindAsync(topicId);
                var zipPath = Path.Combine(_Global.PATH_TOPIC, topicId.ToString(), topic.Title + ".zip");

                using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        foreach (var contribution in approvedContributions)
                            foreach (var file in contribution.Files)
                                archive.CreateEntryFromFile(file.URL, Path.Combine(contribution.Contributor.Number, Path.GetFileName(file.URL)));
                    }
                }

                byte[] fileBytes = System.IO.File.ReadAllBytes(zipPath);

                System.IO.File.Delete(zipPath);

                return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Zip, Path.GetFileName(zipPath));
            }

            return NoContent();
        }
        public async Task<ActionResult> DownloadFile(int fileId = -1)
        {
            var file = await _context.File.FindAsync(fileId);
            byte[] fileBytes = System.IO.File.ReadAllBytes(file.URL);
            return File(fileBytes, MediaTypeNames.Application.Octet, Path.GetFileName(file.URL));
        }
    }
}