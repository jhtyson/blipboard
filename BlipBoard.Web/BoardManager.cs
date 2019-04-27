using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlipBoard.Web
{
    public class BoardManager
    {
        Settings settings;
        BoardGuardian boardGuardian;
        ILogger<BoardManager> logger;

        ConcurrentDictionary<Guid, BlipRepo> repos = new ConcurrentDictionary<Guid, BlipRepo>();

        Lazy<Task> saveRunner;

        public BlipRepo GetRepo(Guid boardId) => repos.GetOrAdd(boardId, CreateOrLoad);

        public BoardManager(IOptions<Settings> settings, BoardGuardian boardGuardian,ILogger<BoardManager> logger)
        {
            this.settings = settings.Value;

            saveRunner = new Lazy<Task>(() => Task.Factory.StartNew(StartSaveRunner));
            this.boardGuardian = boardGuardian;
            this.logger = logger;
        }

        BlipRepo CreateOrLoad(Guid boardId)
        {
            if (!boardGuardian.Check(boardId)) throw new Exception("No such board or the board is disabled.");

            var repo = new BlipRepo();

            if (settings.BasePath == null) throw new Exception("No save path set");

            var savePath = Path.Combine(settings.BasePath, boardId.ToString());

            if (Directory.Exists(savePath))
            {
                try
                {
                    Persistence.LoadLatest(repo, savePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not load latest persistence: {ex.Message}");
                }
            }

            _ = saveRunner.Value;

            return repo;
        }

        void StartSaveRunner()
        {
            while (true)
            {
                try
                {
                    SaveRun();

                    logger.LogInformation("Save run completed");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in save runner");

                    Thread.Sleep(2000);
                }

                Thread.Sleep(500);
            }
        }

        void SaveRun()
        {
            var allRepos = repos.ToArray();

            foreach (var pair in allRepos)
            {
                SaveRun(pair.Key, pair.Value);
            }
        }

        void SaveRun(Guid boardId, BlipRepo repo)
        {
            var savePath = Path.Combine(settings.BasePath, boardId.ToString());

            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            repo.PoorMansSpool(now);

            Persistence.SaveAnother(repo, savePath);

            var blip = new Blip { };
            blip.TimeBegin = blip.TimeEnd = now;
            blip.Level = Level.Info;
            blip.Lane = "_blipboard.cycle";
            repo.Add(blip);
        }
    }
}
