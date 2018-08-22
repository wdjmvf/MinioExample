using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel;
using Minio.Exceptions;

namespace LocalCloud.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MinioController : ControllerBase
    {
        private readonly string _bucketName = "finance-reconcile";
        MinioClient _minioClient;
        public MinioController()
        {
            _minioClient = new MinioClient("127.0.0.1:9000",
                "SK9YX2Z9YLUU5BX068D3",
                "02eXl4AROdfm4EX9xhqTQZJlqCkTswEiqlRTLBQR"
                );
        }

        [Route("info")]
        [HttpGet]
        public string get()
        {
            return "ok";
        }

        [HttpPost]
        [Route("createBucket")]
        public async Task<IActionResult> testMinio()
        {
            try
            {
                // Create bucket if it doesn't exist.
                bool found = await _minioClient.BucketExistsAsync(_bucketName);
                if (found)
                {
                    Console.Out.WriteLine("mybucket already exists");
                }
                else
                {
                    // Create bucket 'my-bucketname'.
                    await _minioClient.MakeBucketAsync(_bucketName);
                    Console.Out.WriteLine("mybucket is created successfully");
                }
            }
            catch (MinioException e)
            {
                Console.Out.WriteLine("Error occurred: " + e);
            }

            return Ok(null);
        }

        [HttpGet]
        [Route("listBucket")]
        public async Task<IActionResult> listBucket()
        {
            try
            {
                // List buckets that have read access.
                var list = await _minioClient.ListBucketsAsync();
                foreach (Bucket bucket in list.Buckets)
                {
                    Console.Out.WriteLine("==================================" + bucket.Name + " " + bucket.CreationDateDateTime);
                }
            }
            catch (MinioException e)
            {
                Console.Out.WriteLine("Error occurred: " + e);
            }
            return Ok(null);
        }

        [HttpGet]
        [Route("listItem")]
        public async Task<IActionResult> getItem()
        {
            try
            {
                // Check whether 'mybucket' exists or not.
                bool found = await _minioClient.BucketExistsAsync(_bucketName);
                if (found)
                {
                    // List objects from 'my-bucketname'
                    IObservable<Item> observable = _minioClient.ListObjectsAsync(_bucketName, null, true);
                    IDisposable subscription = observable.Subscribe(
                            item =>
                            {
                                Console.WriteLine(string.Format("================================== OnNext: {0}", item.Key));
                            },
                            ex => Console.WriteLine("================================== OnError: {0}", ex.Message),
                            () => Console.WriteLine("================================== OnComplete: {0}"));
                }
                else
                {
                    Console.Out.WriteLine("mybucket does not exist");
                }
            }
            catch (MinioException e)
            {
                Console.Out.WriteLine("Error occurred: " + e);
            }
            return Ok(null);
        }

        [HttpGet]
        [Route("incomplete")]
        public async Task<IActionResult> incompleteUploadObject()
        {
            try
            {
                // Check whether 'mybucket' exist or not.
                bool found = await _minioClient.BucketExistsAsync(_bucketName);
                if (found)
                {
                    // List all incomplete multipart upload of objects in 'mybucket'
                    IObservable<Upload> observable = _minioClient.ListIncompleteUploads(_bucketName, null, true);
                    IDisposable subscription = observable.Subscribe(
                                        item => Console.WriteLine("========= > OnNext: {0}", item.Key),
                                        ex => Console.WriteLine("========= > OnError: {0}", ex.Message),
                                        () => Console.WriteLine("========= > OnComplete: {0}"));
                }
                else
                {
                    Console.Out.WriteLine("mybucket does not exist");
                }
            }
            catch (MinioException e)
            {
                Console.Out.WriteLine("Error occurred: " + e);
            }
            return Ok(null);
        }

        [HttpGet]
        [Route("getPolicy")]
        public async Task<IActionResult> getPolicy()
        {
            String policyJson = string.Empty;
            try
            {   //ถ้ามี policy set ไว้
                policyJson = await _minioClient.GetPolicyAsync(_bucketName);
                Console.Out.WriteLine("======================== > Current policy: " + policyJson.GetType().ToString());

            }
            catch (MinioException e)
            {
                //ถ้าไม่มีจะเข้า error เลย
                Console.Out.WriteLine("========================== > Error occurred: " + e);
            }
            return Ok(policyJson);
        }

        [HttpGet]
        [Route("setPolicy")]
        public async Task<IActionResult> setPolicy()
        {
            try
            {
                await _minioClient.SetPolicyAsync(_bucketName, null);
            }
            catch (MinioException e)
            {
                Console.Out.WriteLine("Error occurred: " + e);
            }
            return Ok("");
        }

        [HttpPost]
        [Route("putObject")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> putObject(IFormFile file)
        {
            String urlToReturn = "";
            try
            {
                // set up file request body max size asp.net core 2.0 https://stackoverflow.com/questions/46738364/increase-upload-request-length-limit-in-kestrel
                var uploadsFolterPath = Path.Combine(Path.GetTempPath(), "TEMP_FR_Billing");
                if (!Directory.Exists(uploadsFolterPath))
                {
                    Directory.CreateDirectory(uploadsFolterPath);
                }
                var fileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                var filePath = Path.Combine(uploadsFolterPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                byte[] bs = System.IO.File.ReadAllBytes(filePath);
                using (var filestream = new System.IO.MemoryStream(bs))
                {
                    await _minioClient.PutObjectAsync(_bucketName,
                                                fileName,
                                                filestream,
                                                filestream.Length,
                                               "application/octet-stream"); // มีผลกับ icon ใน minio ด้วย
                    Console.Out.WriteLine("========================== > " + fileName + " is uploaded successfully");
                }
                urlToReturn = await _minioClient.PresignedGetObjectAsync(_bucketName, fileName, 60 * 60 * 24);
                Console.Out.WriteLine(urlToReturn);
            }
            catch (MinioException e)
            {
                Console.Out.WriteLine("Error occurred: " + e);
            }
            return Ok(urlToReturn);
        }
    }
}