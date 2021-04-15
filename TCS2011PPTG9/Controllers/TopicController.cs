using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TCS2011PPTG9.Data;
using TCS2011PPTG9.Models;
using MimeKit;
using MailKit.Net.Smtp;
using System.Net.Mime;

namespace TCS2011PPTG9.Controllers
{
    public class TopicController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TopicController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Topic
        public async Task<IActionResult> Index()
        {
            return View(await _context.Topic.ToListAsync());
        }

        // GET: Topic/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var topic = await _context.Topic
                .FirstOrDefaultAsync(m => m.Id == id);
            if (topic == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // get user infomation
            var currentContribution = await _context.Contribution.Include(c => c.Files)
                                                                 .FirstOrDefaultAsync(c => c.ContributorId == userId && c.TopicId == id);
            List<Comment> comments = null;
            if (currentContribution != null)
            {
                comments = await _context.Comment.Include(c => c.User)
                                                .Where(c => c.ContributionId == currentContribution.Id)
                                                .OrderBy(c => c.Date)
                                                .ToListAsync();
            }

            bool isAvailable = false;
            if (DateTime.Now <= topic.Deadline_1)
                isAvailable = true;
            else if (DateTime.Now <= topic.Deadline_2 && currentContribution != null)
                isAvailable = true;
            ViewData["isAvailable"] = isAvailable;



            ViewData["currentContribution"] = currentContribution;
            ViewData["coments"] = comments;

            return View(topic);
        }

        // GET: Topic/Create


        // POST: Topic/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Deadline_1")] Topic topic)
        {
            if (id != topic.Id) { return NotFound(); }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(topic);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TopicExists(topic.Id)) { return NotFound(); }
                    else { throw; }
                }

                return RedirectToAction(nameof(Index));
            }

            return View(topic);
        }


        private bool TopicExists(int id)
        {
            return _context.Topic.Any(e => e.Id == id);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadFile(Contribution contribution, IFormFile file)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);


            if (ModelState.IsValid)

            {
                var user = await _context.Users.FindAsync(userId);
                var existContribution = await _context.Contribution.Include(c => c.Topic)
                                                                   .Include(c => c.Contributor)
                                                                   .FirstOrDefaultAsync(c => c.ContributorId == userId
                                                                   && c.TopicId == contribution.TopicId);

                var Topic = await _context.Topic.FindAsync(contribution.TopicId);
                if (DateTime.Now <= Topic.Deadline_2)
                {
                    if ((DateTime.Now <= Topic.Deadline_1) || (DateTime.Now > Topic.Deadline_1 && existContribution != null))
                    {

                        if (existContribution == null)
                        {
                            contribution.ContributorId = userId;
                            contribution.Status = ContributionStatus.Pending;

                            _context.Add(contribution);
                            await _context.SaveChangesAsync();

                            existContribution = contribution;

                        }

                        else
                        {
                            existContribution.Status = ContributionStatus.Pending;

                            _context.Update(existContribution);
                            await _context.SaveChangesAsync();
                        }

                        if (file.Length > 0)
                        {
                            FileType? fileType;
                            string fileExtension = Path.GetExtension(file.FileName).ToLower();

                            switch (fileExtension)
                            {
                                case ".doc": case ".docx": fileType = FileType.Document; break;
                                case ".jpg": case ".png": fileType = FileType.Image; break;
                                default: fileType = null; break;

                            }

                            if (fileType != null)
                            {
                                var path = Path.Combine(_Global.PATH_TOPIC, existContribution.TopicId.ToString(), user.Number);

                                if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }

                                // Upload file
                                path = Path.Combine(path, String.Format("{0}.{1:yyyy-MM-dd.ss-mm-HH}{2}", user.Number, DateTime.Now, fileExtension));
                                var stream = new FileStream(path, FileMode.Create);
                                file.CopyTo(stream);

                                var newFile = new SubmittedFile();
                                newFile.ContributionId = existContribution.Id;
                                newFile.URL = path;
                                newFile.Type = (FileType)fileType;

                                _context.Add(newFile);
                                await _context.SaveChangesAsync();

                                var topic = await _context.Topic.FindAsync(existContribution.TopicId);

                                var contributorFullname = $"{user.FirstName} {user.LastName}";

                                MailboxAddress from = new MailboxAddress("iMarketing System", "systememail8000@gmail.com");
                                MailboxAddress to = new MailboxAddress(contributorFullname, user.Email);

                                BodyBuilder bodyBuilder = new BodyBuilder();
                                bodyBuilder.TextBody = $"Hello {contributorFullname}, \n\n" +
                                                       $"Your contribution for {topic.Title} is uploaded successfully.\n\n" +
                                                       $"Thank you for your contribution,\n\n" +
                                                       $"Best regards,";

                                MimeMessage message = new MimeMessage();
                                message.From.Add(from);
                                message.To.Add(to);
                                message.Subject = $"contribution for {topic.Title} Status";
                                message.Body = bodyBuilder.ToMessageBody();

                                SmtpClient client = new SmtpClient();
                                client.Connect("smtp.gmail.com", 465, true);
                                client.Authenticate("systememail8000", "abcABC@123");

                                client.Send(message);
                                client.Disconnect(true);
                                client.Dispose();
                            }
                        }
                    }
                }
            }
            return RedirectToAction(nameof(Details), new { id = contribution.TopicId });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]




        public async Task<IActionResult> Comment(int topicId, String commentContent)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var existContribution = await _context.Contribution.FirstOrDefaultAsync(c => c.ContributorId == userId && c.TopicId == topicId);

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
            return RedirectToAction(nameof(Details), new { id = topicId });
        }

        public async Task<ActionResult> DownloadFile(int fileId = -1)
        {
            var file = await _context.File.FindAsync(fileId);
            byte[] fileBytes = System.IO.File.ReadAllBytes(file.URL);
            return File(fileBytes, MediaTypeNames.Application.Octet, Path.GetFileName(file.URL));
        }

        public async Task<ActionResult> DeleteFile(int topicId ,int fileId = -1)
        {
            var file = await _context.File.FindAsync(fileId);
            _context.Remove(file);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = topicId });
           
        }
    }
}