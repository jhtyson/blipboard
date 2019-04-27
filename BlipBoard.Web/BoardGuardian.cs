using BlipBoard.Data;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlipBoard.Web
{
    public class BoardGuardian
    {
        ConcurrentDictionary<Guid, Boolean> states = new ConcurrentDictionary<Guid, Boolean>();
        IServiceProvider services;

        public BoardGuardian(IServiceProvider services)
        {
            this.services = services;
        }

        public Boolean Check(Guid boardId)
        {
            return states.GetOrAdd(boardId, CheckStorage);
        }

        public void Invalidate(Guid boardId)
        {
            states.TryRemove(boardId, out _);
        }

        Boolean CheckStorage(Guid id)
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var board = db.Boards.FirstOrDefault(b => b.Id == id);
                
                return board?.IsEnabled ?? false;
            }
        }
    }
}
