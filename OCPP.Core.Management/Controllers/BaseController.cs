using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public class BaseController : Controller
    {
        protected UserManager UserManager { get; private set; }

        protected ILogger Logger { get; set; }

        protected IConfiguration Config { get; private set; }

        public BaseController(
            UserManager userManager,
            ILoggerFactory loggerFactory,
            IConfiguration config)
        {
            UserManager = userManager;
            Config = config;
        }

    }
}
