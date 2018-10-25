using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace HelperSrv_2.Controllers
{
    [Produces("application/json")]
    [Route("api/ImgUpload")]
    public class ImgUploadController : Controller
    {
        //懒得弄到依赖注入里面了，嘤嘤嘤
        private static Dictionary<string, string> pendingAvater = new Dictionary<string, string>();
        [HttpPost("Upload")]
        public string Upload([FromForm] string UserName, [FromForm] IFormFile userAvater)
        {
            try
            {
                using (var stream = userAvater.OpenReadStream())
                {
                    string guid = Guid.NewGuid().ToString() + ".png";
                    string dstPath = Path.Combine("Images", guid);
                    using (var img = Image.Load(stream))
                    {
                        img.Mutate(x => x.Resize(96, 96));
                        img.Save(Path.Combine(dstPath));
                    }
                    pendingAvater.Add(UserName,dstPath);
                }
            }
            catch (Exception e)
            {
                return (new JObject {{"status", "1"},{"error", e.ToString()}}).ToString();
            }
            return (new JObject {{ "status", "0" }}).ToString();
        }

        [HttpGet("List")]
        public string List()
        {
            var obj = new JObject();
            JArray datas = new JArray();
            foreach (var entry in pendingAvater)
            {
                datas.Add(new JObject
                {
                    {"name",entry.Key },
                    {"img",entry.Value }
                });
            }
            obj.Add("datas", datas);
            return obj.ToString();
        }
    }
}