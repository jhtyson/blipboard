using System;
using Microsoft.AspNetCore.Mvc;
using BlipBoard.Web;

namespace BlipBoard.Controllers
{
    [ApiController]
    [Route("api/feed")]
    public class FeedController : ControllerBase
    {
        //private readonly BoardManager boardManager;

        //[HttpGet("all")]
        //public Blip[] GetAllBlips(Guid id) => boardManager.GetRepo(id).GetAllBlips();

        //[HttpGet("latest")]
        //public Blip[] GetLatestBlips(Guid id, Int64 since) => boardManager.GetRepo(id).GetBlipsSince(since);

        //public FeedController(BoardManager boardManager)
        //{
        //    this.boardManager = boardManager;
        //}
    }
}
