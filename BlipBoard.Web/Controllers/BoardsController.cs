using BlipBoard.Data;
using BlipBoard.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BlipBoard.Controllers
{
    [Authorize]
    public class BoardsController : BaseController
    {
        private readonly ApplicationDbContext db;

        public BoardsController(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            var boards = db.Boards.Where(b => b.OwnerId == UserId).ToArray();

            return View(boards);
        }

        public class EditPm
        {
            [Required]
            [StringLength(30, MinimumLength = 5)]
            public String Name { get; set; }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(EditPm pm)
        {
            if (!ModelState.IsValid) return View();

            var boardId = Guid.NewGuid();

            db.Boards.Add(new Board
            {
                Id = boardId,
                Name = pm.Name,
                OwnerId = UserId,
                IsEnabled = true
            });

            db.SaveChanges();

            AddSuccessMessage("New Board was created");

            //boardGuardian.Invalidate(boardId);

            return Redirect(nameof(Index));
        }

        [HttpGet]
        public IActionResult Remove(Guid id)
        {
            var board = db.Boards
                .Where(b => b.OwnerId == UserId && b.Id == id)
                .FirstOrDefault();

            if (board == null) return NotFound();

            //boardGuardian.Invalidate(id);

            return View(board);
        }

        [HttpPost]
        public IActionResult Remove(Guid id, Boolean dummy = true)
        {
            var board = db.Boards
                .Where(b => b.OwnerId == UserId && b.Id == id)
                .FirstOrDefault();

            if (board == null) return NotFound();

            db.Boards.Remove(board);

            db.SaveChanges();

            AddSuccessMessage("Board has been removed");

            return Redirect(Url.Action(nameof(Index)));
        }

        [HttpGet]
        public IActionResult Details(Guid id)
        {
            var board = db.Boards
                .Where(b => b.OwnerId == UserId && b.Id == id)
                .FirstOrDefault();

            if (board == null) return NotFound();

            return View(new EditPm { Name = board.Name });
        }

        [HttpPost]
        public IActionResult Details(Guid id, EditPm pm)
        {
            var board = db.Boards
                .Where(b => b.OwnerId == UserId && b.Id == id)
                .FirstOrDefault();

            if (board == null) return NotFound();

            board.Name = pm.Name;

            return Redirect(Url.Action(nameof(Index)));
        }

        [HttpGet("{id}/view")]
        public IActionResult View(Guid id)
        {
            return Redirect($"/index.html?boardId={id:n}");
        }

        [HttpGet("{id}/test")]
        public IActionResult Test(Guid id)
        {
            //var repo = boardManager.GetRepo(id);

            //var rnd = new Random();

            //repo.Add(Level.Error, "test-" + rnd.Next(1, 4), "test42");

            return Redirect(Url.Action(nameof(Index)));
        }
    }
}
