using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BlipBoard.Controllers
{
    public class CrossRequestMessage
    {
        public String Message { get; set; }
        public String Level { get; set; }
    }

    public class BaseController : Controller
    {
        public const String CrossRequestMessageProperty = "CrossRequestMessageMessageProperty";

        protected String UserId => User.FindFirst(ClaimTypes.NameIdentifier).Value;

        void AddMessage(String level, String message)
        {
            TempData[CrossRequestMessageProperty] = JsonConvert.SerializeObject(new CrossRequestMessage { Level = level, Message = message });
        }

        protected void AddSuccessMessage(String message) => AddMessage("success", message);

        protected void AddWarningMessage(String message) => AddMessage("warning", message);

        protected void AddErrorMessage(String message) => AddMessage("error", message);
    }
}
