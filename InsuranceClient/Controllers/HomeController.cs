using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using InsuranceClient.Models;
using InsuranceClient.Models.ViewModel;
using System.IO;
using InsuranceClient.Helpers;
using Microsoft.Extensions.Configuration;

namespace InsuranceClient.Controllers
{
    public class HomeController : Controller
    {
        private IConfiguration configuration;
        public HomeController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(CustomerViewModel model)
        {
            if (ModelState.IsValid)
            {
                var customerId = Guid.NewGuid();
                StorageHelper storageHelper = new StorageHelper();
                storageHelper.ConnectionString = configuration.GetConnectionString("StorageConnection");

                //save customer image to azure blob
                var tempFile = Path.GetTempFileName();
                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                {
                    await model.Image.CopyToAsync(fs);
                }
                var fileName = Path.GetFileName(model.Image.FileName);
                var tempPath = Path.GetDirectoryName(tempFile);
                var imagePath = Path.Combine(tempPath, string.Concat(customerId, "_" + fileName));
                System.IO.File.Move(tempFile, imagePath); //rename temp file
                var imageUrl = await storageHelper.UploadCustomerImageAsync("images", imagePath);
                //End

                //save cutomer data to azure table
                Customer customer = new Customer(customerId.ToString(), model.InsuranceType);
                customer.FullName = model.FullName;
                customer.Email = model.Email;
                customer.Amount = model.Amount;
                customer.AppDate = model.AppDate;
                customer.EndDate = model.EndDate;
                customer.Premium = model.Premium;
                customer.ImageUrl = imageUrl;
                await storageHelper.InsuranceCustomerAsync("customers", customer);
                //End
                //add a confirmation message to azure queue
                await storageHelper.AddMessageAsync("insurance-requests",customer);
                //End
                return RedirectToAction("index");
            }
            else
            {
                return View();
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
