using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlipBoard.Web
{
    public class Ingres
    {
        private readonly Application application;

        public Ingres(Application application)
        {
            this.application = application;
        }

        static readonly Char[] Ws = { ' ', '\n', '\r' };
        static readonly Char[] Br = { '\n', '\r' };

        public async Task Handle(HttpContext context)
        {
            var reader = new StreamReader(context.Request.Body);

            var body = await reader.ReadToEndAsync();

            await Handle2(context, body);
        }

        Task Handle2(HttpContext context, String body)
        {
            var pathSegments = context.Request.Path.Value.Split('/');

            if (pathSegments.Length < 2) return GetResponse(context, 404, "Expected a path length of 2, the second part being the board id");

            var boardIdString = pathSegments[1];

            if (!Guid.TryParse(boardIdString, out var boardId)) return GetResponse(context, 404, "Invalid or missing board id in path");

            var firstSpace = body.IndexOfAny(Ws);

            if (firstSpace < 0) firstSpace = body.Length;

            var firstBr = body.IndexOfAny(Br);

            if (firstBr < 0) firstBr = body.Length;

            String headerLevel = context.Request.Headers["X-Level"];
            String queryLevel = context.Request.Query["level"];

            var levelString =
                !String.IsNullOrEmpty(headerLevel) ? headerLevel
                : !String.IsNullOrEmpty(queryLevel) ? queryLevel
                : null;

            String line;

            if (levelString == null && firstSpace < firstBr)
            {
                levelString = body.Substring(0, firstSpace);
                line = body.Substring(firstSpace + 1, firstBr - firstSpace - 1);
            }
            else
            {
                line = body.Substring(0, firstBr);
            }

            if (!Enum.TryParse<Level>(levelString, true, out var level)) return GetResponse(context, 400, $"Can't parse level '{levelString}'");

            if (String.IsNullOrWhiteSpace(line)) return GetResponse(context, 400, "No body present");

            application.Hit(boardId, )
            boardManager.GetRepo(boardId).Add(level, line, body);

            return GetResponse(context, 200);
        }

        async Task GetResponse(HttpContext context, Int32 code, String message = "")
        {
            context.Response.StatusCode = code;

            await context.Response.WriteAsync(message);
        }
    }
}
